namespace ARCHive.Core;

public enum JobAction
{
    Copy,
    CreateArchive,
    ExtractArchive
}

public enum JobStatus
{
    Completed,
    CompletedWithWarnings,
    Cancelled,
    Failed
}

public enum ValidationSeverity
{
    Warning,
    Error
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message);

public sealed record CopySourceSpec(
    string SourcePath,
    bool IsDirectory,
    string OutputName,
    long TotalBytes,
    long TotalFiles,
    long LargestFileBytes,
    DateTime? LastWriteTimeUtc = null);

public sealed record JobSpec(
    Guid JobId,
    JobAction Action,
    string SourcePath,
    string DestinationRoot,
    string OutputPath,
    bool SourceIsDirectory,
    long TotalBytes,
    long TotalFiles,
    DateTimeOffset CreatedAt,
    long LargestFileBytes = 0,
    DateTime? SourceLastWriteTimeUtc = null,
    IReadOnlyList<CopySourceSpec>? CopySources = null);

public sealed record PreflightResult(
    JobSpec? Job,
    IReadOnlyList<ValidationIssue> Issues,
    long? DestinationFreeBytes)
{
    public bool IsValid =>
        Job is not null &&
        Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}

public sealed record JobProgress(
    string Stage,
    string Message,
    long BytesCompleted,
    long TotalBytes,
    long FilesCompleted,
    long TotalFiles,
    double? Percent,
    bool IsIndeterminate);

public sealed record JobResult(
    Guid JobId,
    JobStatus Status,
    string OutputPath,
    long BytesProcessed,
    long FilesProcessed,
    TimeSpan Duration,
    int? EngineExitCode,
    string Summary,
    string? EngineDetails = null);
