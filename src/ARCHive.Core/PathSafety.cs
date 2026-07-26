namespace ARCHive.Core;

public static class PathSafety
{
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded));
    }

    public static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Normalize(left),
            Normalize(right),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSameOrDescendant(string candidate, string root)
    {
        var normalizedCandidate = Normalize(candidate);
        var normalizedRoot = Normalize(root);

        if (string.Equals(
                normalizedCandidate,
                normalizedRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }
}
