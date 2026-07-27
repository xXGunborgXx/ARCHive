using ARCHive.Core;

namespace ARCHive.Archive;

public sealed class ArchiveJobPlanner
{
    public async Task<ArchivePlanResult<ArchiveCreateSpec>> PlanCreateAsync(
        string sourceInput,
        string destinationInput,
        ArchiveFormat format,
        CompressionPreset compression,
        DateTimeOffset createdAt,
        bool verifyAfterCreate = true,
        CancellationToken cancellationToken = default) =>
        await PlanCreateAsync(
            [sourceInput],
            destinationInput,
            format,
            compression,
            createdAt,
            verifyAfterCreate,
            cancellationToken);

    public async Task<ArchivePlanResult<ArchiveCreateSpec>> PlanCreateAsync(
        IReadOnlyCollection<string> sourceInputs,
        string destinationInput,
        ArchiveFormat format,
        CompressionPreset compression,
        DateTimeOffset createdAt,
        bool verifyAfterCreate = true,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();
        string destinationRoot;
        try
        {
            destinationRoot = PathSafety.Normalize(destinationInput);
        }
        catch (Exception ex) when (
            ex is ArgumentException or NotSupportedException or
                PathTooLongException)
        {
            issues.Add(Error(
                "destination.invalid",
                "The destination path is not valid."));
            return new ArchivePlanResult<ArchiveCreateSpec>(
                null,
                issues,
                null);
        }

        if (sourceInputs.Count == 0)
        {
            issues.Add(Error("source.missing", "Choose at least one source."));
        }

        if (File.Exists(destinationRoot))
        {
            issues.Add(Error(
                "destination.is_file",
                "Choose a destination folder, not a file."));
        }
        else if (!Directory.Exists(destinationRoot))
        {
            issues.Add(Error(
                "destination.missing",
                "The destination folder does not exist."));
        }

        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new ArchivePlanResult<ArchiveCreateSpec>(null, issues, null);
        }

        var normalizedSources = new List<string>(sourceInputs.Count);
        foreach (var sourceInput in sourceInputs)
        {
            try
            {
                normalizedSources.Add(PathSafety.Normalize(sourceInput));
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or
                    PathTooLongException)
            {
                issues.Add(Error(
                    "source.invalid",
                    "One of the selected source paths is not valid."));
                return new ArchivePlanResult<ArchiveCreateSpec>(
                    null,
                    issues,
                    null);
            }
        }

        if (normalizedSources.Distinct(
                StringComparer.OrdinalIgnoreCase).Count() !=
            normalizedSources.Count)
        {
            issues.Add(Error(
                "source.duplicate",
                "The same source was selected more than once."));
            return new ArchivePlanResult<ArchiveCreateSpec>(
                null,
                issues,
                null);
        }

        foreach (var sourcePath in normalizedSources)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                issues.Add(Error(
                    "source.missing",
                    "One of the selected sources no longer exists."));
            }
            else if (Directory.Exists(sourcePath) &&
                     PathSafety.IsSamePath(
                         sourcePath,
                         Path.GetPathRoot(sourcePath) ?? sourcePath))
            {
                issues.Add(Error(
                    "source.drive_root",
                    "Choose folders within a drive rather than the entire drive."));
            }
        }

        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new ArchivePlanResult<ArchiveCreateSpec>(
                null,
                issues,
                null);
        }

        for (var outer = 0; outer < normalizedSources.Count; outer++)
        {
            var folder = normalizedSources[outer];
            if (!Directory.Exists(folder))
            {
                continue;
            }

            if (PathSafety.IsSameOrDescendant(destinationRoot, folder))
            {
                issues.Add(Error(
                    "destination.inside_source",
                    "The destination cannot be inside a selected source folder."));
                return new ArchivePlanResult<ArchiveCreateSpec>(
                    null,
                    issues,
                    null);
            }

            for (var inner = 0; inner < normalizedSources.Count; inner++)
            {
                if (inner != outer &&
                    PathSafety.IsSameOrDescendant(
                        normalizedSources[inner],
                        folder))
                {
                    issues.Add(Error(
                        "source.overlap",
                        "Do not select both a folder and an item already inside it."));
                    return new ArchivePlanResult<ArchiveCreateSpec>(
                        null,
                        issues,
                        null);
                }
            }
        }

        if (normalizedSources.Count > 1)
        {
            var duplicateName = normalizedSources
                .Select(GetTopLevelName)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateName is not null)
            {
                issues.Add(Error(
                    "source.name_collision",
                    $"Two selected items are named \"{duplicateName.Key}\". Rename one or archive them separately."));
                return new ArchivePlanResult<ArchiveCreateSpec>(
                    null,
                    issues,
                    null);
            }
        }

        var sourceSpecs = new List<ArchiveSourceSpec>(
            normalizedSources.Count);
        try
        {
            foreach (var sourcePath in normalizedSources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceIsDirectory = Directory.Exists(sourcePath);
                var statistics = await Task.Run(
                    () => ScanSource(
                        sourcePath,
                        sourceIsDirectory,
                        cancellationToken),
                    cancellationToken);
                sourceSpecs.Add(new ArchiveSourceSpec(
                    sourcePath,
                    sourceIsDirectory,
                    GetTopLevelName(sourcePath),
                    statistics.TotalBytes,
                    statistics.TotalFiles));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            issues.Add(Error(
                "source.unreadable",
                "ARCHive cannot read part of the selected source."));
            return new ArchivePlanResult<ArchiveCreateSpec>(null, issues, null);
        }
        catch (IOException ex)
        {
            issues.Add(Error(
                "source.scan_failed",
                $"ARCHive could not inspect the source: {ex.Message}"));
            return new ArchivePlanResult<ArchiveCreateSpec>(null, issues, null);
        }
        catch (OverflowException)
        {
            issues.Add(Error(
                "source.too_large",
                "The selected sources are too large to measure safely."));
            return new ArchivePlanResult<ArchiveCreateSpec>(null, issues, null);
        }

        long totalBytes;
        long totalFiles;
        try
        {
            totalBytes = sourceSpecs.Aggregate(
                0L,
                (total, source) => checked(total + source.TotalBytes));
            totalFiles = sourceSpecs.Aggregate(
                0L,
                (total, source) => checked(total + source.TotalFiles));
        }
        catch (OverflowException)
        {
            issues.Add(Error(
                "source.too_large",
                "The selected sources are too large to measure safely."));
            return new ArchivePlanResult<ArchiveCreateSpec>(null, issues, null);
        }

        var outputPath = CreateAvailableArchivePath(
            sourceSpecs[0].SourcePath,
            destinationRoot,
            format,
            createdAt,
            sourceSpecs.Count > 1);
        var freeBytes = PathUtilities.TryGetAvailableFreeSpace(destinationRoot);
        long conservativeRequired;
        try
        {
            conservativeRequired = checked(
                totalBytes +
                Math.Max(1024 * 1024, totalBytes / 100));
        }
        catch (OverflowException)
        {
            conservativeRequired = long.MaxValue;
        }

        if (freeBytes.HasValue && conservativeRequired > freeBytes.Value)
        {
            issues.Add(Error(
                "destination.insufficient_space",
                "The destination may not have enough space for this archive."));
        }

        var spec = new ArchiveCreateSpec(
            Guid.NewGuid(),
            sourceSpecs[0].SourcePath,
            destinationRoot,
            outputPath,
            sourceSpecs[0].IsDirectory,
            format,
            compression,
            totalBytes,
            totalFiles,
            createdAt,
            verifyAfterCreate,
            sourceSpecs);

        return new ArchivePlanResult<ArchiveCreateSpec>(spec, issues, freeBytes);
    }

    public Task<ArchivePlanResult<ArchiveExtractSpec>> PlanExtractAsync(
        string archiveInput,
        string destinationInput,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = new List<ValidationIssue>();
        string archivePath;
        string destinationRoot;

        try
        {
            archivePath = PathSafety.Normalize(archiveInput);
            destinationRoot = PathSafety.Normalize(destinationInput);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(Error("path.invalid", "The selected path is not valid."));
            return Task.FromResult(
                new ArchivePlanResult<ArchiveExtractSpec>(null, issues, null));
        }

        if (!File.Exists(archivePath))
        {
            issues.Add(Error("archive.missing", "The selected archive does not exist."));
        }

        if (File.Exists(destinationRoot))
        {
            issues.Add(Error(
                "destination.is_file",
                "Choose a destination folder, not a file."));
        }
        else if (!Directory.Exists(destinationRoot))
        {
            issues.Add(Error(
                "destination.missing",
                "The destination folder does not exist."));
        }

        var extension = Path.GetExtension(archivePath);
        if (!extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error(
                "archive.unsupported",
                "Version 1 currently accepts 7z and ZIP archives."));
        }

        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return Task.FromResult(
                new ArchivePlanResult<ArchiveExtractSpec>(null, issues, null));
        }

        var baseName = Path.GetFileNameWithoutExtension(archivePath);
        var outputPath = CreateAvailableDirectoryPath(
            destinationRoot,
            $"{baseName} - Extracted {createdAt.ToLocalTime():yyyy-MM-dd HHmm}");
        var freeBytes = PathUtilities.TryGetAvailableFreeSpace(destinationRoot);
        var spec = new ArchiveExtractSpec(
            Guid.NewGuid(),
            archivePath,
            destinationRoot,
            outputPath,
            createdAt);

        return Task.FromResult(
            new ArchivePlanResult<ArchiveExtractSpec>(spec, issues, freeBytes));
    }

    private static SourceStatistics ScanSource(
        string sourcePath,
        bool sourceIsDirectory,
        CancellationToken cancellationToken)
    {
        if (!sourceIsDirectory)
        {
            var file = new FileInfo(sourcePath);
            using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new SourceStatistics(file.Length, 1);
        }

        long totalBytes = 0;
        long totalFiles = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var filePath in Directory.EnumerateFiles(sourcePath, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalBytes = checked(totalBytes + new FileInfo(filePath).Length);
            totalFiles++;
        }

        return new SourceStatistics(totalBytes, totalFiles);
    }

    private static string CreateAvailableArchivePath(
        string sourcePath,
        string destinationRoot,
        ArchiveFormat format,
        DateTimeOffset createdAt,
        bool isMultiSource)
    {
        var sourceName = isMultiSource
            ? "ARCHive Collection"
            : Directory.Exists(sourcePath)
                ? new DirectoryInfo(sourcePath).Name
                : Path.GetFileNameWithoutExtension(sourcePath);
        var extension = format == ArchiveFormat.SevenZip ? ".7z" : ".zip";
        var baseName = $"{sourceName} - {createdAt.ToLocalTime():yyyy-MM-dd HHmm}";
        var candidate = Path.Combine(destinationRoot, baseName + extension);
        var suffix = 2;

        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(destinationRoot, $"{baseName} ({suffix}){extension}");
            suffix++;
        }

        return candidate;
    }

    private static string GetTopLevelName(string sourcePath) =>
        PathUtilities.GetTopLevelName(sourcePath);

    private static string CreateAvailableDirectoryPath(
        string destinationRoot,
        string baseName)
    {
        var candidate = Path.Combine(destinationRoot, baseName);
        var suffix = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(destinationRoot, $"{baseName} ({suffix})");
            suffix++;
        }

        return candidate;
    }

    private static ValidationIssue Error(string code, string message) =>
        new(ValidationSeverity.Error, code, message);

    private sealed record SourceStatistics(long TotalBytes, long TotalFiles);
}
