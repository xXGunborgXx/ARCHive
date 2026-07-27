using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ARCHive.Core;

namespace ARCHive.Archive;

public sealed partial class SevenZipArchiveRunner(string? executablePath = null)
    : IArchiveJobRunner
{
    private readonly string _executablePath = executablePath ??
        Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "7za.exe");

    public async Task<JobResult> CreateAsync(
        ArchiveCreateSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var temporaryPath = $"{job.OutputPath}.{job.JobId:N}.partial";
        var output = new ToolOutput();

        if (!File.Exists(_executablePath))
        {
            return MissingToolResult(job.JobId, job.OutputPath, stopwatch.Elapsed);
        }

        var sources = job.Sources is { Count: > 0 }
            ? job.Sources
            : [
                new ArchiveSourceSpec(
                    job.SourcePath,
                    job.SourceIsDirectory,
                    job.SourceIsDirectory
                        ? new DirectoryInfo(job.SourcePath).Name
                        : Path.GetFileName(job.SourcePath),
                    job.TotalBytes,
                    job.TotalFiles)
            ];
        progress?.Report(new JobProgress(
            "Archiving",
            sources.Count > 1
                ? $"Preparing {sources.Count:N0} selected items..."
                : sources[0].EntryName,
            0,
            job.TotalBytes,
            0,
            job.TotalFiles,
            null,
            true));

        try
        {
            var batches = CreateSourceBatches(sources);
            long completedBytes = 0;
            long completedFiles = 0;
            var completedSources = 0;
            for (var index = 0; index < batches.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = batches[index];

                var createArguments = new List<string>
                {
                    "a",
                    job.Format == ArchiveFormat.SevenZip ? "-t7z" : "-tzip",
                    "-mmt=on",
                    "-y",
                    "-bso1",
                    "-bse1",
                    "-bsp1",
                    "-sccUTF-8"
                };
                createArguments.AddRange(
                    CompressionArguments(job.Compression, job.Format));
                createArguments.Add(temporaryPath);
                createArguments.Add("--");
                createArguments.AddRange(
                    batch.Sources.Select(source => source.EntryName));
                var bytesBeforeSource = completedBytes;
                var filesBeforeSource = completedFiles;
                var sourcesBeforeBatch = completedSources;
                var createResult = await RunToolAsync(
                    createArguments,
                    output,
                    line => ReportArchiveBatchPercentage(
                        batch,
                        sourcesBeforeBatch,
                        sources.Count,
                        bytesBeforeSource,
                        filesBeforeSource,
                        job.TotalBytes,
                        job.TotalFiles,
                        line,
                        progress),
                    cancellationToken,
                    batch.WorkingDirectory);

                if (createResult.Cancelled)
                {
                    var cleanup = TryDeleteOwnedFile(temporaryPath);
                    stopwatch.Stop();
                    return new JobResult(
                        job.JobId,
                        JobStatus.Cancelled,
                        job.OutputPath,
                        completedBytes,
                        completedFiles,
                        stopwatch.Elapsed,
                        null,
                        cleanup.Removed
                            ? "Archive creation cancelled. The incomplete archive was removed. The source was not changed."
                            : "Archive creation cancelled. An incomplete temporary archive remains and must not be treated as completed. The source was not changed.",
                        AppendDetails(output, cleanup.Details));
                }

                if (createResult.ExitCode != 0 ||
                    !File.Exists(temporaryPath))
                {
                    TryDeleteOwnedFile(temporaryPath);
                    stopwatch.Stop();
                    return new JobResult(
                        job.JobId,
                        JobStatus.Failed,
                        job.OutputPath,
                        completedBytes,
                        completedFiles,
                        stopwatch.Elapsed,
                        createResult.ExitCode,
                        "7-Zip could not add one group of selected items to the archive. The incomplete archive was removed.",
                        output.ToString());
                }

                completedBytes = checked(
                    completedBytes + batch.TotalBytes);
                completedFiles = checked(
                    completedFiles + batch.TotalFiles);
                completedSources += batch.Sources.Count;
                var completedPercent = job.TotalBytes > 0
                    ? Math.Min(
                        99,
                        completedBytes * 100d / job.TotalBytes)
                    : completedSources * 99d / sources.Count;
                progress?.Report(new JobProgress(
                    "Archiving",
                    batch.Sources.Count == 1
                        ? $"Added {batch.Sources[0].EntryName}"
                        : $"Added {batch.Sources.Count:N0} selected items",
                    completedBytes,
                    job.TotalBytes,
                    completedFiles,
                    job.TotalFiles,
                    completedPercent,
                    false));
            }

            progress?.Report(new JobProgress(
                "Verifying",
                "Testing the completed archive...",
                job.TotalBytes,
                job.TotalBytes,
                job.TotalFiles,
                job.TotalFiles,
                null,
                true));

            var testResult = await RunToolAsync(
                ["t", temporaryPath, "-bso1", "-bse1", "-bsp0", "-sccUTF-8"],
                output,
                progressLine: null,
                cancellationToken);

            if (testResult.Cancelled)
            {
                TryDeleteOwnedFile(temporaryPath);
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Cancelled,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    "Archive verification cancelled. No final archive was published.",
                    output.ToString());
            }

            if (testResult.ExitCode != 0)
            {
                TryDeleteOwnedFile(temporaryPath);
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    testResult.ExitCode,
                    "The archive was created but failed verification. The incomplete archive was removed.",
                    output.ToString());
            }

            File.Move(temporaryPath, job.OutputPath);
            var archiveBytes = new FileInfo(job.OutputPath).Length;
            stopwatch.Stop();

            return new JobResult(
                job.JobId,
                JobStatus.Completed,
                job.OutputPath,
                archiveBytes,
                job.TotalFiles,
                stopwatch.Elapsed,
                0,
                "The archive was created and verified successfully.",
                output.ToString());
        }
        catch (OperationCanceledException)
        {
            var cleanup = TryDeleteOwnedFile(temporaryPath);
            stopwatch.Stop();
            return new JobResult(
                job.JobId,
                JobStatus.Cancelled,
                job.OutputPath,
                0,
                0,
                stopwatch.Elapsed,
                null,
                cleanup.Removed
                    ? "Archive creation cancelled. The incomplete archive was removed. The source was not changed."
                    : "Archive creation cancelled. An incomplete temporary archive remains and must not be treated as completed. The source was not changed.",
                AppendDetails(output, cleanup.Details));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
                InvalidOperationException or OverflowException)
        {
            TryDeleteOwnedFile(temporaryPath);
            stopwatch.Stop();
            output.Add(ex.ToString());
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                0,
                0,
                stopwatch.Elapsed,
                null,
                $"Archive creation failed: {ex.Message}. The incomplete archive was removed.",
                output.ToString());
        }
    }

    public async Task<JobResult> ExtractAsync(
        ArchiveExtractSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var output = new ToolOutput();

        if (!File.Exists(_executablePath))
        {
            return MissingToolResult(job.JobId, job.OutputPath, stopwatch.Elapsed);
        }

        progress?.Report(new JobProgress(
            "Inspecting",
            "Checking archive paths before extraction...",
            0,
            0,
            0,
            0,
            null,
            true));

        try
        {
            var listResult = await RunToolAsync(
                ["l", "-slt", "-ba", job.ArchivePath, "-sccUTF-8"],
                output,
                progressLine: null,
                cancellationToken);

            if (listResult.Cancelled)
            {
                var cleanup = TryDeleteOwnedOutputDirectory(job);
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Cancelled,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    cleanup.Removed
                        ? "Extraction cancelled. The incomplete dated output was removed."
                        : "Extraction cancelled. Incomplete output remains and must not be treated as completed.",
                    AppendDetails(output, cleanup.Details));
            }

            if (listResult.ExitCode != 0)
            {
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    listResult.ExitCode,
                    "The archive could not be read.",
                    output.ToString());
            }

            var entries = ParseEntries(listResult.Lines);
            var fileEntries = entries.Where(entry => !entry.IsDirectory).ToArray();
            long totalBytes;
            try
            {
                totalBytes = fileEntries.Aggregate(
                    0L,
                    (total, entry) => checked(total + entry.Size));
            }
            catch (OverflowException)
            {
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    "The archive could not be read. No files were extracted.",
                    output.ToString());
            }

            foreach (var entry in entries)
            {
                var issue = ArchivePathValidator.Validate(entry, job.OutputPath);
                if (issue is null)
                {
                    continue;
                }

                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    issue.Message,
                    output.ToString());
            }

            var freeBytes = PathUtilities.TryGetAvailableFreeSpace(job.DestinationRoot);
            if (freeBytes.HasValue && totalBytes > freeBytes.Value)
            {
                stopwatch.Stop();
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    "The destination does not have enough space to extract this archive.",
                    output.ToString());
            }

            progress?.Report(new JobProgress(
                "Extracting",
                Path.GetFileName(job.ArchivePath),
                0,
                totalBytes,
                0,
                fileEntries.Length,
                0,
                totalBytes == 0));

            var extractResult = await RunToolAsync(
                [
                    "x",
                    job.ArchivePath,
                    $"-o{job.OutputPath}",
                    "-y",
                    "-bso1",
                    "-bse1",
                    "-bsp1",
                    "-sccUTF-8"
                ],
                output,
                line => ReportPercentage(
                    "Extracting",
                    Path.GetFileName(job.ArchivePath),
                    totalBytes,
                    fileEntries.Length,
                    line,
                    progress),
                cancellationToken);

            stopwatch.Stop();
            if (extractResult.Cancelled)
            {
                var cleanup = TryDeleteOwnedOutputDirectory(job);
                return new JobResult(
                    job.JobId,
                    JobStatus.Cancelled,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    cleanup.Removed
                        ? "Extraction cancelled. The incomplete dated output was removed."
                        : "Extraction cancelled. Incomplete output remains and must not be treated as completed.",
                    AppendDetails(output, cleanup.Details));
            }

            if (extractResult.ExitCode != 0)
            {
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    extractResult.ExitCode,
                    "7-Zip could not extract the archive. Any files written before the failure were preserved at the destination.",
                    output.ToString());
            }

            return new JobResult(
                job.JobId,
                JobStatus.Completed,
                job.OutputPath,
                totalBytes,
                fileEntries.Length,
                stopwatch.Elapsed,
                0,
                "The archive was extracted successfully.",
                output.ToString());
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            stopwatch.Stop();
            output.Add(ex.ToString());
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                0,
                0,
                stopwatch.Elapsed,
                null,
                $"Extraction failed: {ex.Message}. Any files written before the failure were preserved at the destination.",
                output.ToString());
        }
    }

    private async Task<ToolRunResult> RunToolAsync(
        IReadOnlyList<string> arguments,
        ToolOutput output,
        Action<string>? progressLine,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        output.Add($"Executable: {_executablePath}");
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            output.Add($"Working directory: {workingDirectory}");
        }
        output.Add($"Arguments: {PathUtilities.FormatArgumentsForLog(startInfo.ArgumentList)}");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new IOException("Windows could not start 7-Zip.");
        }

        process.StandardInput.Close();
        var standardOutput = CaptureLinesAsync(
            process.StandardOutput,
            output,
            progressLine,
            cancellationToken);
        var standardError = CaptureLinesAsync(
            process.StandardError,
            output,
            progressLine,
            cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardOutput, standardError);
            return new ToolRunResult(false, process.ExitCode, output.SnapshotLines());
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            return new ToolRunResult(true, null, output.SnapshotLines());
        }
    }

    private static async Task CaptureLinesAsync(
        StreamReader reader,
        ToolOutput output,
        Action<string>? progressLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }

            output.Add(line);
            progressLine?.Invoke(line);
        }
    }

    private static IReadOnlyList<ArchiveEntryInfo> ParseEntries(
        IReadOnlyList<string> lines)
    {
        var entries = new List<ArchiveEntryInfo>();
        var current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void FinishEntry()
        {
            if (!current.TryGetValue("Path", out var path))
            {
                current.Clear();
                return;
            }

            current.TryGetValue("Attributes", out var attributes);
            var encrypted = current.TryGetValue("Encrypted", out var encryptedValue) &&
                encryptedValue == "+";
            var size = current.TryGetValue("Size", out var sizeValue) &&
                long.TryParse(sizeValue, out var parsedSize)
                    ? Math.Max(0, parsedSize)
                    : 0;
            var isDirectory =
                current.TryGetValue("Folder", out var folderValue) &&
                folderValue == "+";
            entries.Add(new ArchiveEntryInfo(
                path,
                attributes,
                encrypted,
                size,
                isDirectory));
            current.Clear();
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                FinishEntry();
                continue;
            }

            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 3)..].Trim();
            if (key.Equals("Path", StringComparison.OrdinalIgnoreCase) &&
                current.ContainsKey("Path"))
            {
                FinishEntry();
            }

            current[key] = value;
        }

        FinishEntry();
        return entries;
    }

    private static void ReportPercentage(
        string stage,
        string message,
        long totalBytes,
        long totalFiles,
        string line,
        IProgress<JobProgress>? progress)
    {
        var match = PercentageRegex().Match(line);
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value, out var percent))
        {
            return;
        }

        percent = Math.Clamp(percent, 0, 100);
        var completedBytes = totalBytes > 0
            ? (long)(totalBytes * (percent / 100d))
            : 0;
        progress?.Report(new JobProgress(
            stage,
            message,
            completedBytes,
            totalBytes,
            0,
            totalFiles,
            percent,
            false));
    }

    private static void ReportArchiveBatchPercentage(
        ArchiveSourceBatch batch,
        int sourcesBeforeBatch,
        int sourceCount,
        long bytesBeforeSource,
        long filesBeforeSource,
        long totalBytes,
        long totalFiles,
        string line,
        IProgress<JobProgress>? progress)
    {
        var match = PercentageRegex().Match(line);
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value, out var sourcePercent))
        {
            return;
        }

        sourcePercent = Math.Clamp(sourcePercent, 0, 100);
        var completedBytes = bytesBeforeSource +
            (long)(batch.TotalBytes * (sourcePercent / 100d));
        var completedFiles = filesBeforeSource +
            (long)(batch.TotalFiles * (sourcePercent / 100d));
        var overallPercent = totalBytes > 0
            ? Math.Min(99, completedBytes * 100d / totalBytes)
            : Math.Min(
                99,
                (sourcesBeforeBatch +
                 batch.Sources.Count * sourcePercent / 100d) *
                100d /
                sourceCount);
        progress?.Report(new JobProgress(
            "Archiving",
            ArchiveBatchMessage(batch, sourcesBeforeBatch, sourceCount),
            completedBytes,
            totalBytes,
            completedFiles,
            totalFiles,
            overallPercent,
            false));
    }

    private static IReadOnlyList<ArchiveSourceBatch> CreateSourceBatches(
        IReadOnlyList<ArchiveSourceSpec> sources)
    {
        var groupedSources = new List<(
            string WorkingDirectory,
            List<ArchiveSourceSpec> Sources)>();
        var groupIndexes = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var workingDirectory = Path.GetDirectoryName(
                source.SourcePath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new InvalidOperationException(
                    $"ARCHive could not determine the parent folder for {source.EntryName}.");
            }

            if (!groupIndexes.TryGetValue(
                    workingDirectory,
                    out var groupIndex))
            {
                groupIndex = groupedSources.Count;
                groupIndexes.Add(workingDirectory, groupIndex);
                groupedSources.Add((
                    workingDirectory,
                    []));
            }

            groupedSources[groupIndex].Sources.Add(source);
        }

        var batches = new List<ArchiveSourceBatch>(groupedSources.Count);
        foreach (var group in groupedSources)
        {
            var totalBytes = group.Sources.Aggregate(
                0L,
                (total, source) => checked(total + source.TotalBytes));
            var totalFiles = group.Sources.Aggregate(
                0L,
                (total, source) => checked(total + source.TotalFiles));
            batches.Add(new ArchiveSourceBatch(
                group.WorkingDirectory,
                group.Sources,
                totalBytes,
                totalFiles));
        }

        return batches;
    }

    private static string ArchiveBatchMessage(
        ArchiveSourceBatch batch,
        int sourcesBeforeBatch,
        int sourceCount)
    {
        if (sourceCount == 1)
        {
            return batch.Sources[0].EntryName;
        }

        if (batch.Sources.Count == 1)
        {
            return $"Adding {batch.Sources[0].EntryName} ({sourcesBeforeBatch + 1:N0} of {sourceCount:N0})";
        }

        var first = sourcesBeforeBatch + 1;
        var last = sourcesBeforeBatch + batch.Sources.Count;
        return $"Adding {batch.Sources.Count:N0} selected items ({first:N0}-{last:N0} of {sourceCount:N0})";
    }

    private static string[] CompressionArguments(CompressionPreset preset, ArchiveFormat format) =>
        preset switch
        {
            CompressionPreset.Fast => ["-mx=1"],
            CompressionPreset.Smallest => format == ArchiveFormat.Zip
                ? ["-mx=9", "-md=27"]
                : ["-mx=9"],
            _ => ["-mx=5"]
        };

    private static JobResult MissingToolResult(
        Guid jobId,
        string outputPath,
        TimeSpan duration) =>
        new(
            jobId,
            JobStatus.Failed,
            outputPath,
            0,
            0,
            duration,
            null,
            "The bundled 7-Zip component is missing.");

    private static CleanupResult TryDeleteOwnedFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return new CleanupResult(
                true,
                "Cancellation cleanup: removed the application-owned incomplete archive.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return new CleanupResult(
                false,
                $"Cancellation cleanup could not remove the incomplete archive: {ex.Message}");
        }
    }

    private static CleanupResult TryDeleteOwnedOutputDirectory(
        ArchiveExtractSpec job)
    {
        try
        {
            if (!Directory.Exists(job.OutputPath))
            {
                return new CleanupResult(
                    true,
                    "Cancellation cleanup: no incomplete extraction folder remained.");
            }

            if (PathSafety.IsSamePath(job.OutputPath, job.DestinationRoot) ||
                !PathSafety.IsSameOrDescendant(
                    job.OutputPath,
                    job.DestinationRoot))
            {
                return new CleanupResult(
                    false,
                    "Cancellation cleanup refused: the extraction output was not an application-owned child of the selected destination.");
            }

            Directory.Delete(job.OutputPath, recursive: true);
            return new CleanupResult(
                true,
                "Cancellation cleanup: removed the application-owned incomplete extraction folder.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new CleanupResult(
                false,
                $"Cancellation cleanup could not remove the incomplete extraction folder: {ex.Message}");
        }
    }

    private static string AppendDetails(ToolOutput output, string detail)
    {
        var existing = output.ToString();
        return string.IsNullOrWhiteSpace(existing)
            ? detail
            : $"{existing}{Environment.NewLine}{detail}";
    }

    [GeneratedRegex(@"(?<!\d)(\d{1,3})%")]
    private static partial Regex PercentageRegex();

    private sealed record ArchiveSourceBatch(
        string WorkingDirectory,
        IReadOnlyList<ArchiveSourceSpec> Sources,
        long TotalBytes,
        long TotalFiles);

    private sealed record ToolRunResult(
        bool Cancelled,
        int? ExitCode,
        IReadOnlyList<string> Lines);

    private readonly record struct CleanupResult(
        bool Removed,
        string Details);

    private sealed class ToolOutput
    {
        private const int MaximumLines = 100_000;
        private const int DetailLines = 500;
        private readonly List<string> _allLines = [];
        private readonly Queue<string> _detailLines = new(DetailLines);
        private readonly object _gate = new();

        public void Add(string line)
        {
            lock (_gate)
            {
                if (_allLines.Count < MaximumLines)
                {
                    _allLines.Add(line);
                }

                if (_detailLines.Count == DetailLines)
                {
                    _detailLines.Dequeue();
                }

                _detailLines.Enqueue(line);
            }
        }

        public IReadOnlyList<string> SnapshotLines()
        {
            lock (_gate)
            {
                return _allLines.ToArray();
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return string.Join(Environment.NewLine, _detailLines);
            }
        }
    }
}
