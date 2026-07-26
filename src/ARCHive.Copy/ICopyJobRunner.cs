using ARCHive.Core;

namespace ARCHive.Copy;

public interface ICopyJobRunner
{
    Task<JobResult> RunAsync(
        JobSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default);
}
