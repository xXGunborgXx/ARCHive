using ARCHive.Core;

namespace ARCHive.Archive;

public enum ArchiveFormat
{
    SevenZip,
    Zip
}

public enum CompressionPreset
{
    Fast,
    Balanced,
    Smallest
}

public sealed record ArchiveSourceSpec(
    string SourcePath,
    bool IsDirectory,
    string EntryName,
    long TotalBytes,
    long TotalFiles);

public sealed record ArchiveCreateSpec(
    Guid JobId,
    string SourcePath,
    string DestinationRoot,
    string OutputPath,
    bool SourceIsDirectory,
    ArchiveFormat Format,
    CompressionPreset Compression,
    long TotalBytes,
    long TotalFiles,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ArchiveSourceSpec>? Sources = null);

public sealed record ArchiveExtractSpec(
    Guid JobId,
    string ArchivePath,
    string DestinationRoot,
    string OutputPath,
    DateTimeOffset CreatedAt);

public sealed record ArchivePlanResult<TSpec>(
    TSpec? Job,
    IReadOnlyList<ValidationIssue> Issues,
    long? DestinationFreeBytes)
    where TSpec : class
{
    public bool IsValid =>
        Job is not null &&
        Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}

public sealed record ArchiveEntryInfo(
    string Path,
    string? Attributes,
    bool IsEncrypted,
    long Size = 0,
    bool IsDirectory = false);
