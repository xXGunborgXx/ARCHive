namespace ARCHive.Core;

public sealed record DestinationNavigationPlan(string DirectoryPath);

public static class DestinationNavigation
{
    public static DestinationNavigationPlan? Plan(string outputPath)
    {
        if (File.Exists(outputPath))
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            return string.IsNullOrWhiteSpace(parent)
                ? null
                : new DestinationNavigationPlan(parent);
        }

        if (Directory.Exists(outputPath))
        {
            return new DestinationNavigationPlan(
                Path.GetFullPath(outputPath));
        }

        return null;
    }
}
