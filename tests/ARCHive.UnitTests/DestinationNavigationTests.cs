using ARCHive.Core;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class DestinationNavigationTests
{
    [TestMethod]
    public void Plan_ForFolder_OpensFolderDirectly()
    {
        var folder = CreateTemporaryDirectory();

        try
        {
            var result = DestinationNavigation.Plan(folder);

            Assert.IsNotNull(result);
            Assert.AreEqual(Path.GetFullPath(folder), result.DirectoryPath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Plan_ForFile_OpensContainingFolder()
    {
        var folder = CreateTemporaryDirectory();
        var file = Path.Combine(folder, "completed archive.zip");
        File.WriteAllText(file, "test");

        try
        {
            var result = DestinationNavigation.Plan(file);

            Assert.IsNotNull(result);
            Assert.AreEqual(Path.GetFullPath(folder), result.DirectoryPath);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void Plan_ForMissingOutput_ReturnsNull()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            $"ARCHive-missing-{Guid.NewGuid():N}");

        Assert.IsNull(DestinationNavigation.Plan(missing));
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"ARCHive-navigation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
