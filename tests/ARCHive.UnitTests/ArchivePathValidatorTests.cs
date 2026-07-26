using ARCHive.Archive;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class ArchivePathValidatorTests
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ARCHive-extract-root");

    [TestMethod]
    public void Validate_AcceptsNormalNestedPath()
    {
        var issue = ArchivePathValidator.Validate(
            new ArchiveEntryInfo("folder\\file.txt", "A", false),
            _root);

        Assert.IsNull(issue);
    }

    [TestMethod]
    [DataRow("..\\outside.txt")]
    [DataRow("folder\\..\\outside.txt")]
    [DataRow("C:\\outside.txt")]
    [DataRow("\\\\server\\share\\outside.txt")]
    [DataRow("file.txt:secret")]
    public void Validate_RejectsEscapingOrDevicePath(string entryPath)
    {
        var issue = ArchivePathValidator.Validate(
            new ArchiveEntryInfo(entryPath, "A", false),
            _root);

        Assert.IsNotNull(issue);
    }

    [TestMethod]
    public void Validate_RejectsEncryptedArchiveUntilSafePasswordSupportExists()
    {
        var issue = ArchivePathValidator.Validate(
            new ArchiveEntryInfo("folder\\file.txt", "A", true),
            _root);

        Assert.IsNotNull(issue);
        Assert.AreEqual("archive.password_required", issue.Code);
    }

    [TestMethod]
    public void Validate_RejectsLinkEntry()
    {
        var issue = ArchivePathValidator.Validate(
            new ArchiveEntryInfo("folder\\link", "lrwxrwxrwx", false),
            _root);

        Assert.IsNotNull(issue);
        Assert.AreEqual("archive.link_entry", issue.Code);
    }
}
