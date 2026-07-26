using System.Diagnostics;
using ARCHive.Core;

namespace ARCHive.Copy;

internal sealed class PausableFolderCopyRunner(CopyPauseController pauseController)
{
    private const int BufferSize = 1024 * 1024;
    private static readonly TimeSpan ProgressInterval =
        TimeSpan.FromMilliseconds(250);

    public async Task<JobResult> RunAsync(
        JobSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var details = new List<string>();
        var active = new List<Task<FileCopyOutcome>>();
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationToken = operationCancellation.Token;
        long transferredBytes = 0;
        long completedFiles = 0;
        var lastProgress = TimeSpan.Zero;
        var progressGate = new object();

        try
        {
            pauseController.BeginSession(job.TotalFiles > 1);
            var plan = await Task.Run(
                () => BuildPlan(job, operationToken),
                operationToken);
            if (plan.TotalBytes != job.TotalBytes ||
                plan.Files.Count != job.TotalFiles)
            {
                return Failed(
                    job,
                    stopwatch,
                    "The source changed after preflight. Select it again before copying.",
                    "Pause coordinator refused a source whose file count or total size changed.");
            }

            var concurrency = SelectConcurrency(job);
            details.Add("Copy engine: cooperative pause coordinator");
            details.Add($"Concurrent files: {concurrency}");
            details.Add("Integrity: temporary file per active item; publish after completion");
            CreateDestinationDirectories(job, plan.Directories);
            var manifest = new CopySessionManifest(plan.Files);
            var nextFile = 0;

            void Report(string stage, string message, bool force = false)
            {
                lock (progressGate)
                {
                    if (!force &&
                        stopwatch.Elapsed - lastProgress < ProgressInterval)
                    {
                        return;
                    }

                    lastProgress = stopwatch.Elapsed;
                    var bytes = Math.Min(
                        Interlocked.Read(ref transferredBytes),
                        job.TotalBytes);
                    var files = Math.Min(
                        Interlocked.Read(ref completedFiles),
                        job.TotalFiles);
                    var percent = job.TotalBytes == 0
                        ? 100
                        : Math.Min(99, bytes * 100d / job.TotalBytes);
                    progress?.Report(new JobProgress(
                        stage,
                        message,
                        bytes,
                        job.TotalBytes,
                        files,
                        job.TotalFiles,
                        percent,
                        false));
                }
            }

            Report(
                "Copying",
                job.CopySources is { Count: > 1 }
                    ? "Copying selected items..."
                    : "Copying folder contents...",
                force: true);

            while (nextFile < plan.Files.Count || active.Count > 0)
            {
                operationToken.ThrowIfCancellationRequested();

                if (pauseController.IsPauseRequested)
                {
                    if (active.Count > 0)
                    {
                        Report(
                            "Pausing",
                            "Finishing active files before pausing...",
                            force: true);
                        var outcomes = await Task.WhenAll(active);
                        active.Clear();
                        RecordOutcomes(outcomes, manifest, ref completedFiles);
                    }

                    Report(
                        "Paused",
                        "Paused safely between files. Completed files are preserved.",
                        force: true);
                    await pauseController.WaitForResumeAsync(operationToken);
                    manifest.ValidateCompletedSources();
                    Report(
                        "Copying",
                        "Resuming with the next file...",
                        force: true);
                    continue;
                }

                while (nextFile < plan.Files.Count &&
                       active.Count < concurrency &&
                       !pauseController.IsPauseRequested)
                {
                    var entry = plan.Files[nextFile++];
                    active.Add(CopyFileWithRetryAsync(
                        job,
                        entry,
                        bytes =>
                        {
                            Interlocked.Add(ref transferredBytes, bytes);
                            Report(
                                pauseController.IsPauseRequested
                                    ? "Pausing"
                                    : "Copying",
                                pauseController.IsPauseRequested
                                    ? "Finishing active files before pausing..."
                                    : $"Copying {entry.RelativePath}");
                        },
                        operationToken));
                }

                if (active.Count == 0)
                {
                    continue;
                }

                var completedTask = await Task.WhenAny(active);
                active.Remove(completedTask);
                var outcome = await completedTask;
                RecordOutcomes([outcome], manifest, ref completedFiles);
                Report("Copying", $"Completed {outcome.RelativePath}", force: true);
            }

            ApplyDirectoryMetadata(job, plan.Directories);
            progress?.Report(new JobProgress(
                "Verifying",
                "Checking copied file counts and sizes...",
                job.TotalBytes,
                job.TotalBytes,
                job.TotalFiles,
                job.TotalFiles,
                null,
                true));

            var verification = Verify(job);
            details.Add($"Verification: {verification.Message}");
            stopwatch.Stop();
            if (!verification.Verified)
            {
                return new JobResult(
                    job.JobId,
                    JobStatus.Failed,
                    job.OutputPath,
                    0,
                    0,
                    stopwatch.Elapsed,
                    null,
                    verification.Message,
                    string.Join(Environment.NewLine, details));
            }

            progress?.Report(new JobProgress(
                "Copying",
                job.CopySources is { Count: > 1 }
                    ? "Selected items copied."
                    : "Folder copy complete.",
                job.TotalBytes,
                job.TotalBytes,
                job.TotalFiles,
                job.TotalFiles,
                100,
                false));
            return new JobResult(
                job.JobId,
                JobStatus.Completed,
                job.OutputPath,
                job.TotalBytes,
                job.TotalFiles,
                stopwatch.Elapsed,
                0,
                job.CopySources is { Count: > 1 }
                    ? "The selected items were copied and their file counts and sizes were verified."
                    : "The folder was copied and its file counts and sizes were verified.",
                string.Join(Environment.NewLine, details));
        }
        catch (OperationCanceledException)
        {
            operationCancellation.Cancel();
            await DrainAsync(active);
            var cleanup = DeleteOwnedOutput(job);
            stopwatch.Stop();
            details.Add(cleanup.Details);
            return new JobResult(
                job.JobId,
                JobStatus.Cancelled,
                job.OutputPath,
                Math.Min(transferredBytes, job.TotalBytes),
                completedFiles,
                stopwatch.Elapsed,
                null,
                cleanup.Removed
                    ? "Copy cancelled. The entire dated output was removed. The source was not changed."
                    : "Copy cancelled. Incomplete output remains and must not be treated as completed. The source was not changed.",
                string.Join(Environment.NewLine, details));
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
                SourceChangedException or OverflowException)
        {
            operationCancellation.Cancel();
            await DrainAsync(active);
            var cleanup = DeleteOwnedOutput(job);
            stopwatch.Stop();
            details.Add(ex.ToString());
            details.Add(cleanup.Details);
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                Math.Min(transferredBytes, job.TotalBytes),
                completedFiles,
                stopwatch.Elapsed,
                null,
                $"Copy stopped safely: {ex.Message}",
                string.Join(Environment.NewLine, details));
        }
        finally
        {
            pauseController.EndSession();
        }
    }

    private static FolderCopyPlan BuildPlan(
        JobSpec job,
        CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var files = new List<FileCopyEntry>();
        var directories = new List<DirectoryCopyEntry>();
        long totalBytes = 0;

        var sources = job.CopySources is { Count: > 0 }
            ? job.CopySources
            : [
                new CopySourceSpec(
                    job.SourcePath,
                    job.SourceIsDirectory,
                    job.SourceIsDirectory
                        ? new DirectoryInfo(job.SourcePath).Name
                        : Path.GetFileName(job.SourcePath),
                    job.TotalBytes,
                    job.TotalFiles,
                    job.LargestFileBytes,
                    job.SourceLastWriteTimeUtc)
            ];
        var preserveSingleFolderLayout =
            sources.Count == 1 && sources[0].IsDirectory;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!source.IsDirectory)
            {
                var info = new FileInfo(source.SourcePath);
                var entry = FileCopyEntry.From(
                    source.SourcePath,
                    source.OutputName);
                files.Add(entry);
                totalBytes = checked(totalBytes + info.Length);
                continue;
            }

            var rootRelative = preserveSingleFolderLayout
                ? string.Empty
                : source.OutputName;
            directories.Add(DirectoryCopyEntry.From(
                source.SourcePath,
                rootRelative));

            foreach (var directory in Directory.EnumerateDirectories(
                source.SourcePath,
                "*",
                options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                directories.Add(DirectoryCopyEntry.From(
                    directory,
                    CombineRelative(
                        rootRelative,
                        Path.GetRelativePath(
                            source.SourcePath,
                            directory))));
            }

            foreach (var path in Directory.EnumerateFiles(
                source.SourcePath,
                "*",
                options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = FileCopyEntry.From(
                    path,
                    CombineRelative(
                        rootRelative,
                        Path.GetRelativePath(source.SourcePath, path)));
                files.Add(entry);
                totalBytes = checked(totalBytes + entry.Length);
            }
        }

        return new FolderCopyPlan(files, directories, totalBytes);
    }

    private static string CombineRelative(string parent, string child) =>
        string.IsNullOrEmpty(parent)
            ? child
            : Path.Combine(parent, child);

    private static void CreateDestinationDirectories(
        JobSpec job,
        IReadOnlyList<DirectoryCopyEntry> directories)
    {
        foreach (var directory in directories.OrderBy(item => item.Depth))
        {
            var destination = string.IsNullOrEmpty(directory.RelativePath)
                ? job.OutputPath
                : Path.Combine(job.OutputPath, directory.RelativePath);
            Directory.CreateDirectory(destination);
        }
    }

    private static async Task<FileCopyOutcome> CopyFileWithRetryAsync(
        JobSpec job,
        FileCopyEntry entry,
        Action<long> transferred,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long attemptBytes = 0;
            try
            {
                return await CopyFileAsync(
                    job,
                    entry,
                    bytes =>
                    {
                        attemptBytes += bytes;
                        transferred(bytes);
                    },
                    cancellationToken);
            }
            catch (IOException ex) when (
                ex is not SourceChangedException &&
                attempt < 2)
            {
                transferred(-attemptBytes);
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        throw lastError ?? new IOException(
            $"Windows could not copy {entry.RelativePath}.");
    }

    private static async Task<FileCopyOutcome> CopyFileAsync(
        JobSpec job,
        FileCopyEntry entry,
        Action<long> transferred,
        CancellationToken cancellationToken)
    {
        entry.ValidateSource();
        var destination = Path.Combine(job.OutputPath, entry.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = $"{destination}.{job.JobId:N}.partial";

        try
        {
            await using var source = new FileStream(
                entry.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var target = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[BufferSize];

            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await target.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                transferred(read);
            }

            await target.FlushAsync(cancellationToken);
            await target.DisposeAsync();
            entry.ValidateSource();
            var destinationInfo = new FileInfo(temporary);
            if (destinationInfo.Length != entry.Length)
            {
                throw new IOException(
                    $"The copied length did not match {entry.RelativePath}.");
            }

            File.SetCreationTimeUtc(temporary, entry.CreationTimeUtc);
            File.SetLastAccessTimeUtc(temporary, entry.LastAccessTimeUtc);
            File.SetLastWriteTimeUtc(temporary, entry.LastWriteTimeUtc);
            File.SetAttributes(temporary, entry.Attributes);
            File.Move(temporary, destination);
            return new FileCopyOutcome(
                entry.RelativePath,
                entry.SourcePath,
                entry.Length,
                entry.LastWriteTimeUtc);
        }
        catch
        {
            TryDeleteFile(temporary);
            throw;
        }
    }

    private static void RecordOutcomes(
        IEnumerable<FileCopyOutcome> outcomes,
        CopySessionManifest manifest,
        ref long completedFiles)
    {
        foreach (var outcome in outcomes)
        {
            manifest.Record(outcome);
            completedFiles++;
        }
    }

    private static void ApplyDirectoryMetadata(
        JobSpec job,
        IReadOnlyList<DirectoryCopyEntry> directories)
    {
        foreach (var directory in directories.OrderByDescending(item => item.Depth))
        {
            var destination = string.IsNullOrEmpty(directory.RelativePath)
                ? job.OutputPath
                : Path.Combine(job.OutputPath, directory.RelativePath);
            Directory.SetCreationTimeUtc(destination, directory.CreationTimeUtc);
            Directory.SetLastAccessTimeUtc(destination, directory.LastAccessTimeUtc);
            Directory.SetLastWriteTimeUtc(destination, directory.LastWriteTimeUtc);
            File.SetAttributes(destination, directory.Attributes);
        }
    }

    private static VerificationResult Verify(JobSpec job)
    {
        var measured = Measure(job.OutputPath);
        return measured.Files == job.TotalFiles &&
            measured.Bytes == job.TotalBytes
                ? new VerificationResult(true, "Folder structure verified.")
                : new VerificationResult(
                    false,
                    "The destination file count or total size did not match the planned copy.");
    }

    private static (long Bytes, long Files) Measure(string root)
    {
        long bytes = 0;
        long files = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            bytes = checked(bytes + new FileInfo(path).Length);
            files++;
        }

        return (bytes, files);
    }

    private static int SelectConcurrency(JobSpec job)
    {
        try
        {
            var root = Path.GetPathRoot(job.DestinationRoot);
            var type = string.IsNullOrWhiteSpace(root)
                ? (DriveType?)null
                : new DriveInfo(root).DriveType;
            if (type is DriveType.Network or DriveType.Removable ||
                job.DestinationRoot.StartsWith(
                    @"\\",
                    StringComparison.Ordinal))
            {
                return 2;
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 2;
        }

        return job.LargestFileBytes >= 256L * 1024 * 1024 ? 2 : 8;
    }

    private static async Task DrainAsync(
        IReadOnlyCollection<Task<FileCopyOutcome>> active)
    {
        try
        {
            await Task.WhenAll(active);
        }
        catch
        {
            // The authoritative job result is produced by the caller.
        }
    }

    private static CleanupResult DeleteOwnedOutput(JobSpec job)
    {
        try
        {
            if (!Directory.Exists(job.OutputPath))
            {
                return new CleanupResult(
                    true,
                    "Cleanup: no job output remained.");
            }

            if (PathSafety.IsSamePath(job.OutputPath, job.DestinationRoot) ||
                !PathSafety.IsSameOrDescendant(
                    job.OutputPath,
                    job.DestinationRoot))
            {
                return new CleanupResult(
                    false,
                    "Cleanup refused because the output was not an application-owned child of the destination.");
            }

            NormalizeAttributes(job.OutputPath);
            Directory.Delete(job.OutputPath, recursive: true);
            return new CleanupResult(
                true,
                "Cleanup: removed the entire application-owned dated output.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new CleanupResult(
                false,
                $"Cleanup could not remove the dated output: {ex.Message}");
        }
    }

    private static void NormalizeAttributes(string root)
    {
        foreach (var file in Directory.EnumerateFiles(
            root,
            "*",
            SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var directory in Directory.EnumerateDirectories(
            root,
            "*",
            SearchOption.AllDirectories))
        {
            File.SetAttributes(directory, FileAttributes.Directory);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
            // Whole-job cleanup remains the final cancellation safeguard.
        }
    }

    private static JobResult Failed(
        JobSpec job,
        Stopwatch stopwatch,
        string summary,
        string details)
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
            summary,
            details);
    }

    private sealed record FolderCopyPlan(
        IReadOnlyList<FileCopyEntry> Files,
        IReadOnlyList<DirectoryCopyEntry> Directories,
        long TotalBytes);

    private sealed record FileCopyEntry(
        string SourcePath,
        string RelativePath,
        long Length,
        DateTime CreationTimeUtc,
        DateTime LastAccessTimeUtc,
        DateTime LastWriteTimeUtc,
        FileAttributes Attributes)
    {
        public static FileCopyEntry From(
            string sourcePath,
            string relativePath)
        {
            var info = new FileInfo(sourcePath);
            return new FileCopyEntry(
                sourcePath,
                relativePath,
                info.Length,
                info.CreationTimeUtc,
                info.LastAccessTimeUtc,
                info.LastWriteTimeUtc,
                info.Attributes);
        }

        public void ValidateSource()
        {
            var current = new FileInfo(SourcePath);
            if (!current.Exists ||
                current.Length != Length ||
                current.LastWriteTimeUtc != LastWriteTimeUtc)
            {
                throw new SourceChangedException(
                    $"The source changed during Pause or copy: {RelativePath}");
            }
        }
    }

    private sealed record DirectoryCopyEntry(
        string RelativePath,
        DateTime CreationTimeUtc,
        DateTime LastAccessTimeUtc,
        DateTime LastWriteTimeUtc,
        FileAttributes Attributes,
        int Depth)
    {
        public static DirectoryCopyEntry From(
            string source,
            string relativePath)
        {
            var info = new DirectoryInfo(source);
            var depth = string.IsNullOrEmpty(relativePath)
                ? 0
                : relativePath.Count(character =>
                    character is '\\' or '/') + 1;
            return new DirectoryCopyEntry(
                relativePath,
                info.CreationTimeUtc,
                info.LastAccessTimeUtc,
                info.LastWriteTimeUtc,
                info.Attributes,
                depth);
        }
    }

    private sealed record FileCopyOutcome(
        string RelativePath,
        string SourcePath,
        long Length,
        DateTime LastWriteTimeUtc);

    private sealed class CopySessionManifest(
        IReadOnlyCollection<FileCopyEntry> plannedFiles)
    {
        private readonly Dictionary<string, FileCopyOutcome> _completed =
            new(StringComparer.OrdinalIgnoreCase);

        public int PlannedFiles { get; } = plannedFiles.Count;

        public void Record(FileCopyOutcome outcome) =>
            _completed[outcome.RelativePath] = outcome;

        public void ValidateCompletedSources()
        {
            foreach (var item in _completed.Values)
            {
                var info = new FileInfo(item.SourcePath);
                if (!info.Exists ||
                    info.Length != item.Length ||
                    info.LastWriteTimeUtc != item.LastWriteTimeUtc)
                {
                    throw new SourceChangedException(
                        $"A completed source file changed while paused: {item.RelativePath}");
                }
            }
        }
    }

    private sealed class SourceChangedException(string message)
        : IOException(message);

    private readonly record struct VerificationResult(
        bool Verified,
        string Message);

    private readonly record struct CleanupResult(
        bool Removed,
        string Details);
}
