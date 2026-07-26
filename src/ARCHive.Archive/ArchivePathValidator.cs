using ARCHive.Core;

namespace ARCHive.Archive;

public static class ArchivePathValidator
{
    public static ValidationIssue? Validate(
        ArchiveEntryInfo entry,
        string extractionRoot)
    {
        if (string.IsNullOrWhiteSpace(entry.Path))
        {
            return Error("archive.empty_path", "The archive contains an empty path.");
        }

        if (entry.IsEncrypted)
        {
            return Error(
                "archive.password_required",
                "Password-protected archives are not enabled in this build.");
        }

        if (entry.Attributes?.StartsWith('l') == true ||
            entry.Attributes?.Contains("Reparse", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Error(
                "archive.link_entry",
                "The archive contains a link entry that is not safe to extract.");
        }

        var normalizedEntry = entry.Path.Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(normalizedEntry) ||
            normalizedEntry.StartsWith(Path.DirectorySeparatorChar) ||
            normalizedEntry.Contains(':'))
        {
            return Error(
                "archive.absolute_path",
                "The archive contains an absolute or device-qualified path.");
        }

        var segments = normalizedEntry.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            return Error(
                "archive.parent_traversal",
                "The archive contains a path that tries to leave the destination.");
        }

        string resolvedPath;
        try
        {
            resolvedPath = Path.GetFullPath(Path.Combine(extractionRoot, normalizedEntry));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Error("archive.invalid_path", "The archive contains an invalid path.");
        }

        return PathSafety.IsSameOrDescendant(resolvedPath, extractionRoot)
            ? null
            : Error(
                "archive.destination_escape",
                "The archive contains a path that escapes the destination.");
    }

    private static ValidationIssue Error(string code, string message) =>
        new(ValidationSeverity.Error, code, message);
}
