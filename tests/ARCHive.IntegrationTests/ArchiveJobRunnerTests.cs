using ARCHive.Archive;
using ARCHive.Core;

namespace ARCHive.IntegrationTests;

[TestClass]
public sealed class ArchiveJobRunnerTests
{
    [TestMethod]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.Zip)]
    public async Task CreateAndExtract_RoundTripsFixture(ArchiveFormat format)
    {
        using var fixture = new ArchiveFixture();
        var source = fixture.CreateDirectory("source-folder");
        fixture.CreateDirectory(Path.Combine("source-folder", "empty"));
        fixture.CreateFile(
            Path.Combine("source-folder", "nested", "hello.txt"),
            "ARCHive round trip");
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();

        var createPlan = await planner.PlanCreateAsync(
            source,
            destination,
            format,
            CompressionPreset.Balanced,
            DateTimeOffset.Now);

        Assert.IsTrue(createPlan.IsValid);
        Assert.IsNotNull(createPlan.Job);
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            progress: null);

        Assert.AreEqual(
            JobStatus.Completed,
            createResult.Status,
            createResult.EngineDetails);
        Assert.IsTrue(File.Exists(createResult.OutputPath));

        var extractPlan = await planner.PlanExtractAsync(
            createResult.OutputPath,
            destination,
            DateTimeOffset.Now);

        Assert.IsTrue(extractPlan.IsValid);
        Assert.IsNotNull(extractPlan.Job);
        var progressUpdates = new List<JobProgress>();
        var extractResult = await runner.ExtractAsync(
            extractPlan.Job,
            new InlineProgress<JobProgress>(progressUpdates.Add));

        Assert.AreEqual(
            JobStatus.Completed,
            extractResult.Status,
            extractResult.EngineDetails);
        var restoredFile = Path.Combine(
            extractResult.OutputPath,
            "source-folder",
            "nested",
            "hello.txt");
        Assert.IsTrue(File.Exists(restoredFile));
        Assert.AreEqual("ARCHive round trip", await File.ReadAllTextAsync(restoredFile));
        Assert.IsTrue(Directory.Exists(Path.Combine(
            extractResult.OutputPath,
            "source-folder",
            "empty")));
        Assert.IsGreaterThan(0, extractResult.BytesProcessed);
        Assert.IsGreaterThan(0, extractResult.FilesProcessed);
        Assert.IsTrue(progressUpdates.Any(update =>
            update.Stage == "Extracting" &&
            update.TotalBytes > 0 &&
            update.TotalFiles > 0));
    }

    [TestMethod]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.Zip)]
    public async Task CreateAndExtract_MixedSourcesPreserveOnlyTopLevelNames(
        ArchiveFormat format)
    {
        using var fixture = new ArchiveFixture();
        var readme = fixture.CreateFile(
            Path.Combine("first-parent", "readme.txt"),
            "top-level file");
        var photos = fixture.CreateDirectory(
            Path.Combine("second-parent", "Photos"));
        fixture.CreateFile(
            Path.Combine(
                "second-parent",
                "Photos",
                "nested",
                "picture.bin"),
            "picture contents");
        fixture.CreateDirectory(
            Path.Combine("second-parent", "Photos", "empty"));
        var notes = fixture.CreateFile(
            Path.Combine("third-parent", "notes.md"),
            "separate file");
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();

        var createPlan = await planner.PlanCreateAsync(
            [readme, photos, notes],
            destination,
            format,
            CompressionPreset.Balanced,
            DateTimeOffset.Now);

        Assert.IsTrue(createPlan.IsValid);
        Assert.IsNotNull(createPlan.Job);
        var progressUpdates = new List<JobProgress>();
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            new InlineProgress<JobProgress>(progressUpdates.Add));

        Assert.AreEqual(
            JobStatus.Completed,
            createResult.Status,
            createResult.EngineDetails);
        Assert.IsTrue(File.Exists(createResult.OutputPath));
        Assert.AreEqual(
            3,
            CountOccurrences(
                createResult.EngineDetails ?? string.Empty,
                "Working directory:"),
            "Three distinct source parents should require exactly three add operations.");
        Assert.IsTrue(progressUpdates.Any(update =>
            update.Stage == "Archiving" &&
            update.TotalFiles == 3));

        var extractPlan = await planner.PlanExtractAsync(
            createResult.OutputPath,
            destination,
            DateTimeOffset.Now);
        Assert.IsTrue(extractPlan.IsValid);
        Assert.IsNotNull(extractPlan.Job);

        var extractResult = await runner.ExtractAsync(
            extractPlan.Job,
            progress: null);

        Assert.AreEqual(
            JobStatus.Completed,
            extractResult.Status,
            extractResult.EngineDetails);
        Assert.AreEqual(
            "top-level file",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "readme.txt")));
        Assert.AreEqual(
            "picture contents",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "Photos",
                "nested",
                "picture.bin")));
        Assert.IsTrue(Directory.Exists(Path.Combine(
            extractResult.OutputPath,
            "Photos",
            "empty")));
        Assert.AreEqual(
            "separate file",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "notes.md")));
        Assert.IsFalse(Directory.Exists(Path.Combine(
            extractResult.OutputPath,
            "first-parent")));
        Assert.IsFalse(Directory.Exists(Path.Combine(
            extractResult.OutputPath,
            "second-parent")));
        Assert.IsFalse(Directory.Exists(Path.Combine(
            extractResult.OutputPath,
            "third-parent")));
    }

    [TestMethod]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.Zip)]
    public async Task CreateAsync_SameParentMixedSourcesUseOneAddOperation(
        ArchiveFormat format)
    {
        using var fixture = new ArchiveFixture();
        var first = fixture.CreateFile(
            Path.Combine("shared-parent", "first.txt"),
            "first");
        var second = fixture.CreateFile(
            Path.Combine("shared-parent", "second.txt"),
            "second");
        var folder = fixture.CreateDirectory(
            Path.Combine("shared-parent", "Folder"));
        fixture.CreateFile(
            Path.Combine("shared-parent", "Folder", "nested.txt"),
            "nested");
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();

        var createPlan = await planner.PlanCreateAsync(
            [first, second, folder],
            destination,
            format,
            CompressionPreset.Balanced,
            DateTimeOffset.Now);

        Assert.IsTrue(createPlan.IsValid);
        Assert.IsNotNull(createPlan.Job);
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            progress: null);

        Assert.AreEqual(
            JobStatus.Completed,
            createResult.Status,
            createResult.EngineDetails);
        Assert.AreEqual(
            1,
            CountOccurrences(
                createResult.EngineDetails ?? string.Empty,
                "Working directory:"),
            "Sources sharing one parent should be sent to one add operation.");

        var extractPlan = await planner.PlanExtractAsync(
            createResult.OutputPath,
            destination,
            DateTimeOffset.Now);
        Assert.IsTrue(extractPlan.IsValid);
        Assert.IsNotNull(extractPlan.Job);
        var extractResult = await runner.ExtractAsync(
            extractPlan.Job,
            progress: null);

        Assert.AreEqual(
            JobStatus.Completed,
            extractResult.Status,
            extractResult.EngineDetails);
        Assert.AreEqual(
            "first",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "first.txt")));
        Assert.AreEqual(
            "second",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "second.txt")));
        Assert.AreEqual(
            "nested",
            await File.ReadAllTextAsync(Path.Combine(
                extractResult.OutputPath,
                "Folder",
                "nested.txt")));
    }

    [TestMethod]
    public async Task ExtractAsync_CancelledOperationRemovesDatedOutput()
    {
        using var fixture = new ArchiveFixture();
        var source = fixture.CreateDirectory("cancel-source");
        fixture.CreateFile(
            Path.Combine("cancel-source", "large.txt"),
            new string('x', 1024 * 1024));
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();
        var createPlan = await planner.PlanCreateAsync(
            source,
            destination,
            ArchiveFormat.SevenZip,
            CompressionPreset.Fast,
            DateTimeOffset.Now);

        Assert.IsNotNull(createPlan.Job);
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            progress: null);
        Assert.AreEqual(JobStatus.Completed, createResult.Status);

        var extractPlan = await planner.PlanExtractAsync(
            createResult.OutputPath,
            destination,
            DateTimeOffset.Now);
        Assert.IsNotNull(extractPlan.Job);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<JobProgress>(update =>
        {
            if (update.Stage == "Extracting")
            {
                cancellation.Cancel();
            }
        });

        var result = await runner.ExtractAsync(
            extractPlan.Job,
            progress,
            cancellation.Token);

        Assert.AreEqual(JobStatus.Cancelled, result.Status);
        Assert.IsFalse(Directory.Exists(result.OutputPath));
        StringAssert.Contains(result.Summary, "incomplete dated output was removed");
        Assert.IsTrue(File.Exists(Path.Combine(source, "large.txt")));
    }

    [TestMethod]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.Zip)]
    public async Task CreateAsync_WithVerifyAfterCreate_VerifiesAndReportsVerified(
        ArchiveFormat format)
    {
        using var fixture = new ArchiveFixture();
        var source = fixture.CreateDirectory("verify-source");
        fixture.CreateFile(
            Path.Combine("verify-source", "data.txt"),
            "verify me");
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();

        var createPlan = await planner.PlanCreateAsync(
            source,
            destination,
            format,
            CompressionPreset.Balanced,
            DateTimeOffset.Now,
            verifyAfterCreate: true);

        Assert.IsTrue(createPlan.IsValid);
        Assert.IsNotNull(createPlan.Job);
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, createResult.Status, createResult.EngineDetails);
        StringAssert.Contains(
            createResult.Summary,
            "created and verified successfully");
    }

    [TestMethod]
    [DataRow(ArchiveFormat.SevenZip)]
    [DataRow(ArchiveFormat.Zip)]
    public async Task CreateAsync_WithoutVerifyAfterCreate_SkipsVerification(
        ArchiveFormat format)
    {
        using var fixture = new ArchiveFixture();
        var source = fixture.CreateDirectory("noverify-source");
        fixture.CreateFile(
            Path.Combine("noverify-source", "data.txt"),
            "skip verify");
        var destination = fixture.CreateDirectory("destination");
        var planner = new ArchiveJobPlanner();

        var createPlan = await planner.PlanCreateAsync(
            source,
            destination,
            format,
            CompressionPreset.Balanced,
            DateTimeOffset.Now,
            verifyAfterCreate: false);

        Assert.IsTrue(createPlan.IsValid);
        Assert.IsNotNull(createPlan.Job);
        var runner = new SevenZipArchiveRunner(SevenZipPath());
        var createResult = await runner.CreateAsync(
            createPlan.Job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, createResult.Status, createResult.EngineDetails);
        StringAssert.Contains(
            createResult.Summary,
            "verification skipped");
        Assert.IsTrue(File.Exists(createResult.OutputPath));
    }

    private static string SevenZipPath() =>
        Path.Combine(AppContext.BaseDirectory, "tools", "7zip", "7za.exe");

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(
                   search,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ArchiveFixture : IDisposable
    {
        public ArchiveFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"ARCHive-archive-{Guid.NewGuid():N}");
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
