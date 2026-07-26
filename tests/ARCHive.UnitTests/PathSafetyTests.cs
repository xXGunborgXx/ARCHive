using ARCHive.Core;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class PathSafetyTests
{
    [TestMethod]
    public void IsSameOrDescendant_RecognizesNestedPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "ARCHive-root");
        var child = Path.Combine(root, "nested", "child");

        Assert.IsTrue(PathSafety.IsSameOrDescendant(child, root));
    }

    [TestMethod]
    public void IsSameOrDescendant_DoesNotMatchSimilarPrefix()
    {
        var parent = Path.Combine(Path.GetTempPath(), "ARCHive-data");
        var sibling = parent + "-old";

        Assert.IsFalse(PathSafety.IsSameOrDescendant(sibling, parent));
    }

    [TestMethod]
    public void Normalize_RemovesWrappingQuotes()
    {
        var path = Path.Combine(Path.GetTempPath(), "ARCHive test");

        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
            PathSafety.Normalize($"\"{path}\""));
    }
}
