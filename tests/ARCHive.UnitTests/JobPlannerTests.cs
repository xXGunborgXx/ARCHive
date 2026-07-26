using ARCHive.Core;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class JobPlannerTests
{
    [TestMethod]
    public async Task PlanCopyAsync_CreatesDatedFolderOutput()
    {
        using var fixture = new PlannerFixture();
        var source = fixture.CreateDirectory("Source Project");
        fixture.CreateFile(Path.Combine("Source Project", "hello.txt"), "hello");
        var destination = fixture.CreateDirectory("Destination");
        var createdAt = new DateTimeOffset(2026, 7, 26, 14, 30, 0, TimeSpan.Zero);

        var result = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            createdAt);

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Job);
        StringAssert.Contains(result.Job.OutputPath, "Source Project - 2026-07-26");
        Assert.AreEqual(1, result.Job.TotalFiles);
        Assert.AreEqual(5, result.Job.TotalBytes);
        Assert.AreEqual(5, result.Job.LargestFileBytes);
    }

    [TestMethod]
    public async Task PlanCopyAsync_RejectsDestinationInsideSource()
    {
        using var fixture = new PlannerFixture();
        var source = fixture.CreateDirectory("Source");
        var destination = fixture.CreateDirectory(Path.Combine("Source", "Backups"));

        var result = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "destination.inside_source"));
    }

    [TestMethod]
    public async Task PlanCopyAsync_RejectsMissingSource()
    {
        using var fixture = new PlannerFixture();
        var destination = fixture.CreateDirectory("Destination");

        var result = await new JobPlanner().PlanCopyAsync(
            Path.Combine(fixture.Root, "missing"),
            destination,
            DateTimeOffset.Now);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "source.missing"));
    }

    [TestMethod]
    public async Task PlanCopyAsync_MixedSourcesCreateOneBatchOutput()
    {
        using var fixture = new PlannerFixture();
        var first = fixture.CreateFile("first.txt", "first");
        var second = fixture.CreateFile("second.bin", "second");
        var folder = fixture.CreateDirectory("Pictures");
        fixture.CreateFile(Path.Combine("Pictures", "nested.jpg"), "image");
        var destination = fixture.CreateDirectory("Destination");
        var createdAt = new DateTimeOffset(
            2026,
            7,
            27,
            9,
            15,
            0,
            TimeSpan.Zero);

        var result = await new JobPlanner().PlanCopyAsync(
            [first, folder, second],
            destination,
            createdAt);

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.Job);
        StringAssert.Contains(result.Job.OutputPath, "ARCHive Copy - 2026-07-27");
        Assert.IsTrue(result.Job.SourceIsDirectory);
        Assert.IsNotNull(result.Job.CopySources);
        Assert.HasCount(3, result.Job.CopySources);
        Assert.AreEqual(3, result.Job.TotalFiles);
        Assert.AreEqual(16, result.Job.TotalBytes);
    }

    [TestMethod]
    public async Task PlanCopyAsync_RejectsFolderAndItsChild()
    {
        using var fixture = new PlannerFixture();
        var folder = fixture.CreateDirectory("Parent");
        var child = fixture.CreateFile(
            Path.Combine("Parent", "child.txt"),
            "child");
        var destination = fixture.CreateDirectory("Destination");

        var result = await new JobPlanner().PlanCopyAsync(
            [folder, child],
            destination,
            DateTimeOffset.Now);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Issues.Any(issue =>
            issue.Code == "source.overlap"));
    }

    [TestMethod]
    public async Task PlanCopyAsync_RejectsTopLevelNameCollision()
    {
        using var fixture = new PlannerFixture();
        var first = fixture.CreateFile(
            Path.Combine("One", "same.txt"),
            "one");
        var second = fixture.CreateFile(
            Path.Combine("Two", "same.txt"),
            "two");
        var destination = fixture.CreateDirectory("Destination");

        var result = await new JobPlanner().PlanCopyAsync(
            [first, second],
            destination,
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
                $"ARCHive-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

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
