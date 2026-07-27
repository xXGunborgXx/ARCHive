using System.Buffers;
using System.Diagnostics;
using ARCHive.Core;

namespace ARCHive.Copy;

public sealed class CopyJobRunner : ICopyJobRunner
{
    private const int MinBufferSize = 256 * 1024;
    private const int DefaultBufferSize = 1024 * 1024;
    private const int MaxBufferSize = 4 * 1024 * 1024;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
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
        var bufferSize = SelectBufferSize(job.TotalBytes);
        byte[]? bufferA = null;
        byte[]? bufferB = null;

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
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            bufferA = ArrayPool<byte>.Shared.Rent(bufferSize);
            bufferB = ArrayPool<byte>.Shared.Rent(bufferSize);
            var lastProgress = TimeSpan.Zero;

            var initialRead = await source.ReadAsync(
                bufferA.AsMemory(0, bufferSize), cancellationToken);
            if (initialRead > 0)
            {
                copiedBytes += initialRead;
                var currentBuffer = bufferA;
                var currentSize = initialRead;
                var otherBuffer = bufferB;

                while (true)
                {
                    var readTask = source.ReadAsync(
                        otherBuffer.AsMemory(0, bufferSize), cancellationToken).AsTask();
                    var writeTask = destination.WriteAsync(
                        currentBuffer.AsMemory(0, currentSize), cancellationToken).AsTask();

                    await Task.WhenAll(readTask, writeTask);

                    var bytesRead = await readTask;
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    copiedBytes += bytesRead;

                    (currentBuffer, otherBuffer) = (otherBuffer, currentBuffer);
                    currentSize = bytesRead;

                    if (stopwatch.Elapsed - lastProgress >= ProgressInterval)
                    {
                        lastProgress = stopwatch.Elapsed;
                        ReportFileProgress(job, progress, copiedBytes);
                    }
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
            var cleanup = TryDeleteOwnedPartialFile(temporaryPath);
            stopwatch.Stop();
            return new JobResult(
                job.JobId,
                JobStatus.Failed,
                job.OutputPath,
                copiedBytes,
                0,
                stopwatch.Elapsed,
                null,
                $"Copy failed: {ex.Message}. {cleanup.Details}",
                ex.ToString());
        }
        finally
        {
            if (bufferA is not null)
            {
                ArrayPool<byte>.Shared.Return(bufferA);
            }

            if (bufferB is not null)
            {
                ArrayPool<byte>.Shared.Return(bufferB);
            }
        }
    }

    private static int SelectBufferSize(long totalBytes) =>
        totalBytes > 100L * 1024 * 1024
            ? MaxBufferSize
            : totalBytes > 1L * 1024 * 1024
                ? DefaultBufferSize
                : MinBufferSize;

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

    private readonly record struct CleanupResult(
        bool Removed,
        string Details);
}
