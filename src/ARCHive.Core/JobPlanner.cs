namespace ARCHive.Core;

public sealed class JobPlanner
{
    private const long Fat32MaximumFileBytes = 4L * 1024 * 1024 * 1024 - 1;

    public async Task<PreflightResult> PlanCopyAsync(
        string sourceInput,
        string destinationInput,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default) =>
        await PlanCopyAsync(
            [sourceInput],
            destinationInput,
            createdAt,
            cancellationToken);

    public async Task<PreflightResult> PlanCopyAsync(
        IReadOnlyCollection<string> sourceInputs,
        string destinationInput,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();
        string destinationRoot;

        try
        {
            destinationRoot = PathSafety.Normalize(destinationInput);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(Error("destination.invalid", "The destination path is not valid."));
            return new PreflightResult(null, issues, null);
        }

        if (sourceInputs.Count == 0)
        {
            issues.Add(Error("source.missing", "Choose at least one source."));
            return new PreflightResult(null, issues, null);
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
            return new PreflightResult(null, issues, null);
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
                return new PreflightResult(null, issues, null);
            }
        }

        if (normalizedSources.Distinct(
                StringComparer.OrdinalIgnoreCase).Count() !=
            normalizedSources.Count)
        {
            issues.Add(Error(
                "source.duplicate",
                "The same source was selected more than once."));
            return new PreflightResult(null, issues, null);
        }

        foreach (var sourcePath in normalizedSources)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                issues.Add(Error(
                    "source.missing",
                    "One of the selected sources no longer exists."));
            }
        }

        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new PreflightResult(null, issues, null);
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
                return new PreflightResult(null, issues, null);
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
                    return new PreflightResult(null, issues, null);
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
                    $"Two selected items are named \"{duplicateName.Key}\". Rename one or copy them separately."));
                return new PreflightResult(null, issues, null);
            }
        }

        var sourceSpecs = new List<CopySourceSpec>(normalizedSources.Count);
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
                sourceSpecs.Add(new CopySourceSpec(
                    sourcePath,
                    sourceIsDirectory,
                    GetTopLevelName(sourcePath),
                    statistics.TotalBytes,
                    statistics.TotalFiles,
                    statistics.LargestFileBytes,
                    sourceIsDirectory
                        ? null
                        : File.GetLastWriteTimeUtc(sourcePath)));
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
            return new PreflightResult(null, issues, null);
        }
        catch (IOException ex)
        {
            issues.Add(Error(
                "source.scan_failed",
                $"ARCHive could not inspect the source: {ex.Message}"));
            return new PreflightResult(null, issues, null);
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
            return new PreflightResult(null, issues, null);
        }

        var largestFileBytes = sourceSpecs.Count == 0
            ? 0
            : sourceSpecs.Max(source => source.LargestFileBytes);
        var isMultiSource = sourceSpecs.Count > 1;
        var firstSource = sourceSpecs[0];
        var outputPath = CreateAvailableOutputPath(
            firstSource.SourcePath,
            destinationRoot,
            firstSource.IsDirectory,
            createdAt,
            isMultiSource);

        long? freeBytes = TryGetAvailableFreeSpace(destinationRoot);
        if (freeBytes.HasValue && totalBytes > freeBytes.Value)
        {
            issues.Add(Error(
                "destination.insufficient_space",
                "The destination does not have enough available space."));
        }

        var destinationFormat = TryGetDriveFormat(destinationRoot);
        if (string.Equals(destinationFormat, "FAT32", StringComparison.OrdinalIgnoreCase) &&
            largestFileBytes > Fat32MaximumFileBytes)
        {
            issues.Add(Error(
                "destination.fat32_limit",
                "The source contains a file larger than the FAT32 file-size limit."));
        }

        var spec = new JobSpec(
            Guid.NewGuid(),
            JobAction.Copy,
            firstSource.SourcePath,
            destinationRoot,
            outputPath,
            firstSource.IsDirectory || isMultiSource,
            totalBytes,
            totalFiles,
            createdAt,
            largestFileBytes,
            firstSource.LastWriteTimeUtc,
            sourceSpecs);

        return new PreflightResult(spec, issues, freeBytes);
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
            return new SourceStatistics(file.Length, 1, file.Length);
        }

        long totalBytes = 0;
        long totalFiles = 0;
        long largestFileBytes = 0;

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
            var file = new FileInfo(filePath);
            totalBytes = checked(totalBytes + file.Length);
            totalFiles++;
            largestFileBytes = Math.Max(largestFileBytes, file.Length);
        }

        return new SourceStatistics(totalBytes, totalFiles, largestFileBytes);
    }

    private static string CreateAvailableOutputPath(
        string sourcePath,
        string destinationRoot,
        bool sourceIsDirectory,
        DateTimeOffset createdAt,
        bool isMultiSource)
    {
        var dateSuffix = createdAt.ToLocalTime().ToString("yyyy-MM-dd HHmm");
        string baseName;
        string extension;

        if (isMultiSource)
        {
            baseName = "ARCHive Copy";
            extension = string.Empty;
        }
        else if (sourceIsDirectory)
        {
            baseName = new DirectoryInfo(sourcePath).Name;
            extension = string.Empty;
        }
        else
        {
            baseName = Path.GetFileNameWithoutExtension(sourcePath);
            extension = Path.GetExtension(sourcePath);
        }

        var candidate = Path.Combine(
            destinationRoot,
            $"{baseName} - {dateSuffix}{extension}");

        var suffix = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(
                destinationRoot,
                $"{baseName} - {dateSuffix} ({suffix}){extension}");
            suffix++;
        }

        return candidate;
    }

    private static string GetTopLevelName(string sourcePath) =>
        Directory.Exists(sourcePath)
            ? new DirectoryInfo(sourcePath).Name
            : Path.GetFileName(sourcePath);

    private static long? TryGetAvailableFreeSpace(string destinationRoot)
    {
        try
        {
            var root = Path.GetPathRoot(destinationRoot);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? TryGetDriveFormat(string destinationRoot)
    {
        try
        {
            var root = Path.GetPathRoot(destinationRoot);
            return string.IsNullOrWhiteSpace(root)
                ? null
                : new DriveInfo(root).DriveFormat;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static ValidationIssue Error(string code, string message) =>
        new(ValidationSeverity.Error, code, message);

    private sealed record SourceStatistics(
        long TotalBytes,
        long TotalFiles,
        long LargestFileBytes);
}
