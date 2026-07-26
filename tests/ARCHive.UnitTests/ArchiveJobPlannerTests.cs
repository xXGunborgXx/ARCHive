using ARCHive.Archive;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class ArchiveJobPlannerTests
{
    [TestMethod]
    public async Task PlanCreateAsync_AcceptsMixedSourcesAndAggregatesStatistics()
    {
        using var fixture = new PlannerFixture();
        var firstFile = fixture.CreateFile(
            Path.Combine("first", "readme.txt"),
            "hello");
        var folder = fixture.CreateDirectory(
            Path.Combine("second", "Photos"));
        fixture.CreateFile(
            Path.Combine("second", "Photos", "nested", "image.bin"),
            "1234567");
        var destination = fixture.CreateDirectory("destination");

        var result = await new ArchiveJobPlanner().PlanCreateAsync(
            [firstFile, folder],
            destination,
            ArchiveFormat.SevenZip,
            CompressionPreset.Balanced,
            new DateTimeOffset(2026, 7, 27, 10, 15, 0, TimeSpan.Zero));

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Job);
        Assert.IsNotNull(result.Job.Sources);
        var sources = result.Job.Sources;
        Assert.HasCount(2, sources);
        Assert.AreEqual(12L, result.Job.TotalBytes);
        Assert.AreEqual(2L, result.Job.TotalFiles);
        Assert.AreEqual("readme.txt", sources[0].EntryName);
        Assert.AreEqual("Photos", sources[1].EntryName);
        StringAssert.StartsWith(
            Path.GetFileName(result.Job.OutputPath),
            "ARCHive Collection - ");
    }

    [TestMethod]
    public async Task PlanCreateAsync_RejectsParentAndChildSelection()
    {
        using var fixture = new PlannerFixture();
        var folder = fixture.CreateDirectory("source");
        var child = fixture.CreateFile(
            Path.Combine("source", "child.txt"),
            "content");
        var destination = fixture.CreateDirectory("destination");

        var result = await new ArchiveJobPlanner().PlanCreateAsync(
            [folder, child],
            destination,
            ArchiveFormat.SevenZip,
            CompressionPreset.Fast,
            DateTimeOffset.Now);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "source.overlap"));
    }

    [TestMethod]
    public async Task PlanCreateAsync_RejectsDuplicateTopLevelNames()
    {
        using var fixture = new PlannerFixture();
        var first = fixture.CreateFile(
            Path.Combine("first", "notes.txt"),
            "first");
        var second = fixture.CreateFile(
            Path.Combine("second", "notes.txt"),
            "second");
        var destination = fixture.CreateDirectory("destination");

        var result = await new ArchiveJobPlanner().PlanCreateAsync(
            [first, second],
            destination,
            ArchiveFormat.Zip,
            CompressionPreset.Balanced,
            DateTimeOffset.Now);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "source.name_collision"));
    }

    private sealed class PlannerFixture : IDisposable
    {
        public PlannerFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"ARCHive-planner-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        private string Root { get; }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
