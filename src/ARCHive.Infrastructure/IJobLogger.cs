using ARCHive.Core;

namespace ARCHive.Infrastructure;

public interface IJobLogger
{
    Task<string> WriteAsync<TJob>(
        TJob job,
        JobResult result,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
