using ARCHive.Core;

namespace ARCHive.Archive;

public interface IArchiveJobRunner
{
    Task<JobResult> CreateAsync(
        ArchiveCreateSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default);

    Task<JobResult> ExtractAsync(
        ArchiveExtractSpec job,
        IProgress<JobProgress>? progress,
        CancellationToken cancellationToken = default);
}
