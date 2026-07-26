using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ARCHive.Core;

namespace ARCHive.Copy;

public sealed class CopyJobRunner : ICopyJobRunner
{
    private const int BufferSize = 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FolderProgressInterval = TimeSpan.FromMilliseconds(250);
    private readonly CopyPauseController _pauseController;

    public CopyJobRunner(CopyPauseController? pauseController = null)
    {
        _pauseController = pauseController ?? new CopyPauseController();
    }

    public Task<JobResult> RunAsync(
        JobSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (job.Action != JobAction.Copy)
        {
            throw new ArgumentException("The job is not a Copy job.", nameof(job));
        }

        return job.SourceIsDirectory
            ? new PausableFolderCopyRunner(_pauseController).RunAsync(
                job,
                progress,
                cancellationToken)
            : CopySingleFileAsync(job, progress, cancellationToken);
    }

    private static async Task<JobResult> CopySingleFileAsync(
        JobSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var temporaryPath = job.OutputPath + ".partial";
        long copiedBytes = 0;

        progress?.Report(new JobProgress(
            "Copying",
            Path.GetFileName(job.SourcePath),
            0,
            job.TotalBytes,
            0,
            1,
            0,
            false));

        try
        {
            await using var source = new FileStream(
                job.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[BufferSize];
            var lastProgress = TimeSpan.Zero;

            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copiedBytes += read;

                if (stopwatch.Elapsed - lastProgress >= ProgressInterval)
                {
                    lastProgress = stopwatch.Elapsed;
                    ReportFileProgress(job, progress, copiedBytes);
                }
            }

            await destination.FlushAsync(cancellationToken);
            await destination.DisposeAsync();
            File.Move(temporaryPath, job.OutputPath);
            File.SetLastWriteTimeUtc(
                job.OutputPath,
                job.SourceLastWriteTimeUtc ??
                    File.GetLastWriteTimeUtc(job.SourcePath));

            stopwatch.Stop();
            ReportFileProgress(job, progress, job.TotalBytes);
            var destinationLength = new FileInfo(job.OutputPath).Length;
            var sourceInfo = new FileInfo(job.SourcePath);
            var sourceChanged =
                !sourceInfo.Exists ||
                sourceInfo.Exists && sourceInfo.Length != job.TotalBytes ||
                job.SourceLastWriteTimeUtc.HasValue &&
                sourceInfo.Exists &&
                sourceInfo.LastWriteTimeUtc != job.SourceLastWriteTimeUtc.Value;
            var status = destinationLength == job.TotalBytes
                ? sourceChanged
                    ? JobStatus.CompletedWithWarnings
                    : JobStatus.Completed
                : JobStatus.Failed;
            var summary = status switch
            {
                JobStatus.Completed =>
                    "The file was copied and its length was verified.",
                JobStatus.CompletedWithWarnings =>
                    "The file was copied, but the source changed during the operation.",
                _ => "The destination file length did not match the source."
            };

            return new JobResult(
                job.JobId,
                status,
                job.OutputPath,
                destinationLength,
                status == JobStatus.Failed ? 0 : 1,
                stopwatch.Elapsed,
                0,
                summary);
        }
        catch (OperationCanceledException)
        {
            var cleanup = TryDeleteOwnedPartialFile(temporaryPath);
            stopwatch.Stop();
            return new JobResult(
                job.JobId,
                JobStatus.Cancelled,
                job.OutputPath,
                copiedBytes,
                0,
                stopwatch.Elapsed,
                null,
                cleanup.Removed
                    ? "Copy cancelled. The incomplete destination file was removed. The source was not changed."
                    : "Copy cancelled. An incomplete temporary file remains and must not be treated as completed. The source was not changed.",
                cleanup.Details);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteOwnedPartialFile(temporaryPath);
            stopwatch.Stop();
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                copiedBytes,
                0,
                stopwatch.Elapsed,
                null,
                $"Copy failed: {ex.Message}",
                ex.ToString());
        }
    }

    private static async Task<JobResult> RunRobocopyAsync(
        JobSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var details = new FixedLineBuffer(250);
        var robocopyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "robocopy.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = robocopyPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var profile = SelectCopyProfile(job);
        foreach (var argument in BuildRobocopyArguments(job, profile))
        {
            startInfo.ArgumentList.Add(argument);
        }

        details.Add($"Executable: {robocopyPath}");
        details.Add($"Copy profile: {profile.Name}");
        details.Add($"Arguments: {FormatArgumentsForLog(startInfo.ArgumentList)}");

        progress?.Report(new JobProgress(
            "Copying",
            "Copying folder contents...",
            0,
            job.TotalBytes,
            0,
            job.TotalFiles,
            0,
            false));

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new IOException("Windows could not start Robocopy.");
            }

            using var progressCancellation = new CancellationTokenSource();
            var progressState = new RobocopyProgressState();
            var folderProgress = MonitorRobocopyProgressAsync(
                process,
                job,
                progress,
                progressState,
                progressCancellation.Token);
            var standardOutput = CaptureLinesAsync(
                process.StandardOutput,
                details,
                cancellationToken);
            var standardError = CaptureLinesAsync(
                process.StandardError,
                details,
                cancellationToken);

            try
            {
                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                    await Task.WhenAll(standardOutput, standardError);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None);
                    }

                    var cleanup = TryDeleteOwnedOutputDirectory(job);
                    details.Add(cleanup.Details);
                    stopwatch.Stop();
                    return new JobResult(
                        job.JobId,
                        JobStatus.Cancelled,
                        job.OutputPath,
                        progressState.BytesCompleted,
                        0,
                        stopwatch.Elapsed,
                        null,
                        cleanup.Removed
                            ? "Copy cancelled. The incomplete destination copy was removed. The source was not changed."
                            : "Copy cancelled. Incomplete output remains and must not be treated as completed. The source was not changed.",
                        details.ToString());
                }
            }
            finally
            {
                progressCancellation.Cancel();
                await IgnoreCancellationAsync(folderProgress);
            }

            var status = MapRobocopyStatus(process.ExitCode);
            FolderVerificationResult? verification = null;
            if (status != JobStatus.Failed)
            {
                progress?.Report(new JobProgress(
                    "Verifying",
                    "Checking copied file counts and sizes...",
                    job.TotalBytes,
                    job.TotalBytes,
                    job.TotalFiles,
                    job.TotalFiles,
                    null,
                    true));

                verification = VerifyFolderCopy(job);
                details.Add($"Verification: {verification.Value.Message}");
                status = verification.Value.Status switch
                {
                    FolderVerificationStatus.Failed => JobStatus.Failed,
                    FolderVerificationStatus.SourceChanged =>
                        JobStatus.CompletedWithWarnings,
                    _ => status
                };
            }

            stopwatch.Stop();
            var summary = status switch
            {
                JobStatus.Completed =>
                    "The folder was copied and its file counts and sizes were verified.",
                JobStatus.CompletedWithWarnings =>
                    verification?.Message ??
                    "The folder was copied with differences or warnings. Review the details.",
                _ => verification?.Message ??
                    "Robocopy reported a failure. Review the details."
            };

            if (status != JobStatus.Failed)
            {
                ReportFolderProgress(
                    job,
                    progress,
                    job.TotalBytes,
                    job.TotalFiles,
                    100);
            }

            return new JobResult(
                job.JobId,
                status,
                job.OutputPath,
                status == JobStatus.Failed ? 0 : job.TotalBytes,
                status == JobStatus.Failed ? 0 : job.TotalFiles,
                stopwatch.Elapsed,
                process.ExitCode,
                summary,
                details.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stopwatch.Stop();
            details.Add(ex.ToString());
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                0,
                0,
                stopwatch.Elapsed,
                null,
                $"Copy failed: {ex.Message}",
                details.ToString());
        }
    }

    private static async Task MonitorRobocopyProgressAsync(
        Process process,
        JobSpec job,
        IProgress<JobProgress>? progress,
        RobocopyProgressState state,
        CancellationToken cancellationToken)
    {
        ulong initialReadBytes = 0;
        var hasInitialCounters = TryGetReadTransferBytes(
            process,
            out initialReadBytes);

        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hasInitialCounters &&
                TryGetReadTransferBytes(process, out var currentReadBytes))
            {
                var transferred = currentReadBytes >= initialReadBytes
                    ? currentReadBytes - initialReadBytes
                    : 0;
                var bytes = transferred > long.MaxValue
                    ? long.MaxValue
                    : (long)transferred;
                Interlocked.Exchange(ref state.BytesCompleted, bytes);
                var percent = job.TotalBytes == 0
                    ? 0
                    : Math.Min(99, bytes * 100d / job.TotalBytes);
                ReportFolderProgress(job, progress, bytes, 0, percent);
            }

            await Task.Delay(FolderProgressInterval, cancellationToken);
        }
    }

    private static bool TryGetReadTransferBytes(
        Process process,
        out ulong readTransferBytes)
    {
        readTransferBytes = 0;

        try
        {
            if (!GetProcessIoCounters(process.Handle, out var counters))
            {
                return false;
            }

            readTransferBytes = counters.ReadTransferCount;
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void ReportFolderProgress(
        JobSpec job,
        IProgress<JobProgress>? progress,
        long copiedBytes,
        long copiedFiles,
        double percent)
    {
        progress?.Report(new JobProgress(
            "Copying",
            "Copying folder contents...",
            Math.Min(copiedBytes, job.TotalBytes),
            job.TotalBytes,
            Math.Min(copiedFiles, job.TotalFiles),
            job.TotalFiles,
            Math.Clamp(percent, 0, 100),
            false));
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // The monitor is expected to stop when Robocopy exits.
        }
    }

    private static IEnumerable<string> BuildRobocopyArguments(
        JobSpec job,
        CopyProfile profile)
    {
        yield return job.SourcePath;
        yield return job.OutputPath;
        yield return "/E";
        yield return "/COPY:DAT";
        yield return "/DCOPY:DAT";
        yield return "/R:2";
        yield return "/W:5";
        yield return "/XJ";
        yield return "/MT:8";
        if (profile.UseRestartableMode)
        {
            yield return "/Z";
        }
        else if (profile.UseUnbufferedIo)
        {
            yield return "/J";
        }

        yield return "/BYTES";
        yield return "/FP";
    }

    private static CopyProfile SelectCopyProfile(JobSpec job)
    {
        var driveType = TryGetDriveType(job.DestinationRoot);
        var restartable =
            driveType is DriveType.Network or DriveType.Removable ||
            job.DestinationRoot.StartsWith(@"\\", StringComparison.Ordinal);

        if (restartable)
        {
            return new CopyProfile(
                "Restartable network/removable",
                UseRestartableMode: true,
                UseUnbufferedIo: false);
        }

        var useUnbufferedIo = job.LargestFileBytes >= 256L * 1024 * 1024;
        return new CopyProfile(
            useUnbufferedIo
                ? "Large-file local"
                : "Buffered multithreaded",
            UseRestartableMode: false,
            UseUnbufferedIo: useUnbufferedIo);
    }

    private static DriveType? TryGetDriveType(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).DriveType;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static FolderVerificationResult VerifyFolderCopy(JobSpec job)
    {
        try
        {
            var source = MeasureTree(job.SourcePath);
            var destination = MeasureTree(job.OutputPath);

            if (destination.Files != job.TotalFiles ||
                destination.Bytes != job.TotalBytes)
            {
                return new FolderVerificationResult(
                    FolderVerificationStatus.Failed,
                    "The destination file count or total size did not match the planned copy.");
            }

            if (source.Files != job.TotalFiles ||
                source.Bytes != job.TotalBytes)
            {
                return new FolderVerificationResult(
                    FolderVerificationStatus.SourceChanged,
                    "The folder was copied, but the source changed during the operation.");
            }

            return new FolderVerificationResult(
                FolderVerificationStatus.Verified,
                "Folder structure verified.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or
                DirectoryNotFoundException or OverflowException)
        {
            return new FolderVerificationResult(
                FolderVerificationStatus.SourceChanged,
                $"The copy completed, but the verification scan could not finish: {ex.Message}");
        }
    }

    private static (long Bytes, long Files) MeasureTree(string root)
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

    private static JobStatus MapRobocopyStatus(int exitCode)
    {
        if (exitCode >= 8)
        {
            return JobStatus.Failed;
        }

        return exitCode >= 4
            ? JobStatus.CompletedWithWarnings
            : JobStatus.Completed;
    }

    private static async Task CaptureLinesAsync(
        StreamReader reader,
        FixedLineBuffer details,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return;
            }

            details.Add(line);
        }
    }

    private static void ReportFileProgress(
        JobSpec job,
        IProgress<JobProgress>? progress,
        long copiedBytes)
    {
        var percent = job.TotalBytes == 0
            ? 100
            : copiedBytes * 100d / job.TotalBytes;

        progress?.Report(new JobProgress(
            "Copying",
            Path.GetFileName(job.SourcePath),
            copiedBytes,
            job.TotalBytes,
            copiedBytes >= job.TotalBytes ? 1 : 0,
            1,
            Math.Clamp(percent, 0, 100),
            false));
    }

    private static CleanupResult TryDeleteOwnedPartialFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return new CleanupResult(
                true,
                "Cancellation cleanup: removed the application-owned incomplete file.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return new CleanupResult(
                false,
                $"Cancellation cleanup could not remove the incomplete temporary file: {ex.Message}");
        }
    }

    private static CleanupResult TryDeleteOwnedOutputDirectory(JobSpec job)
    {
        try
        {
            if (!Directory.Exists(job.OutputPath))
            {
                return new CleanupResult(
                    true,
                    "Cancellation cleanup: no incomplete output folder remained.");
            }

            if (PathSafety.IsSamePath(job.OutputPath, job.DestinationRoot) ||
                !PathSafety.IsSameOrDescendant(
                    job.OutputPath,
                    job.DestinationRoot))
            {
                return new CleanupResult(
                    false,
                    "Cancellation cleanup refused: the output was not an application-owned child of the selected destination.");
            }

            Directory.Delete(job.OutputPath, recursive: true);
            return new CleanupResult(
                true,
                "Cancellation cleanup: removed the application-owned incomplete output folder.");
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new CleanupResult(
                false,
                $"Cancellation cleanup could not remove the incomplete output: {ex.Message}");
        }
    }

    private static string FormatArgumentsForLog(
        System.Collections.ObjectModel.Collection<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append('"').Append(argument.Replace("\"", "\\\"")).Append('"');
        }

        return builder.ToString();
    }

    private sealed class FixedLineBuffer(int capacity)
    {
        private readonly Queue<string> _lines = new(capacity);
        private readonly object _gate = new();

        public void Add(string line)
        {
            lock (_gate)
            {
                if (_lines.Count == capacity)
                {
                    _lines.Dequeue();
                }

                _lines.Enqueue(line);
            }
        }

        public override string ToString()
        {
            lock (_gate)
            {
                return string.Join(Environment.NewLine, _lines);
            }
        }
    }

    private sealed class RobocopyProgressState
    {
        public long BytesCompleted;
    }

    private readonly record struct CopyProfile(
        string Name,
        bool UseRestartableMode,
        bool UseUnbufferedIo);

    private readonly record struct CleanupResult(
        bool Removed,
        string Details);

    private readonly record struct FolderVerificationResult(
        FolderVerificationStatus Status,
        string Message);

    private enum FolderVerificationStatus
    {
        Verified,
        SourceChanged,
        Failed
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(
        IntPtr processHandle,
        out IoCounters counters);
}
