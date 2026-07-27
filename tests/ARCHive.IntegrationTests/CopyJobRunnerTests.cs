using ARCHive.Copy;
using ARCHive.Core;
using System.Security.Cryptography;

namespace ARCHive.IntegrationTests;

[TestClass]
public sealed class CopyJobRunnerTests
{
    [TestMethod]
    public async Task RunAsync_CopiesSingleFileAndPreservesContent()
    {
        using var fixture = new CopyFixture();
        var source = fixture.CreateFile("source.txt", "ARCHive copy test");
        var destination = fixture.CreateDirectory("destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, result.Status);
        Assert.IsTrue(File.Exists(result.OutputPath));
        Assert.AreEqual("ARCHive copy test", await File.ReadAllTextAsync(result.OutputPath));
    }

    [TestMethod]
    public async Task RunAsync_CopiesFolderWithCooperativeCoordinator()
    {
        using var fixture = new CopyFixture();
        var source = fixture.CreateDirectory("source-folder");
        fixture.CreateFile(Path.Combine("source-folder", "nested", "one.txt"), "one");
        fixture.CreateFile(Path.Combine("source-folder", "two.txt"), "two");
        var destination = fixture.CreateDirectory("destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var progressUpdates = new List<JobProgress>();
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            new InlineProgress<JobProgress>(progressUpdates.Add));

        Assert.AreNotEqual(JobStatus.Failed, result.Status, result.EngineDetails);
        Assert.IsTrue(File.Exists(Path.Combine(result.OutputPath, "nested", "one.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(result.OutputPath, "two.txt")));
        Assert.IsNotEmpty(progressUpdates);
        Assert.IsTrue(progressUpdates.Any(update =>
            update.Stage == "Verifying" && update.IsIndeterminate));
        Assert.AreEqual(100d, progressUpdates[^1].Percent);
        StringAssert.Contains(
            result.EngineDetails,
            "Copy engine: cooperative pause coordinator");
        StringAssert.Contains(result.EngineDetails, "Concurrent files: 8");
        StringAssert.Contains(result.EngineDetails, "Verification:");
    }

    [TestMethod]
    public async Task RunAsync_CopiesMixedFilesAndFoldersIntoOneBatch()
    {
        using var fixture = new CopyFixture();
        var document = fixture.CreateFile("document.txt", "document");
        var pictureFolder = fixture.CreateDirectory("Pictures");
        fixture.CreateFile(
            Path.Combine("Pictures", "nested", "picture.bin"),
            "picture");
        var notes = fixture.CreateFile("notes.md", "notes");
        var destination = fixture.CreateDirectory("mixed-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            [document, pictureFolder, notes],
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, result.Status, result.EngineDetails);
        Assert.AreEqual(
            "document",
            await File.ReadAllTextAsync(
                Path.Combine(result.OutputPath, "document.txt")));
        Assert.AreEqual(
            "picture",
            await File.ReadAllTextAsync(
                Path.Combine(
                    result.OutputPath,
                    "Pictures",
                    "nested",
                    "picture.bin")));
        Assert.AreEqual(
            "notes",
            await File.ReadAllTextAsync(
                Path.Combine(result.OutputPath, "notes.md")));
        StringAssert.Contains(result.Summary, "selected items");
    }

    [TestMethod]
    public async Task RunAsync_CancelledMixedCopyRemovesWholeBatchOutput()
    {
        using var fixture = new CopyFixture();
        var first = fixture.CreateFile("first.txt", "first");
        var folder = fixture.CreateDirectory("Folder");
        fixture.CreateFile(Path.Combine("Folder", "second.txt"), "second");
        var destination = fixture.CreateDirectory("cancel-mixed-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            [first, folder],
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            progress: null,
            cancellation.Token);

        Assert.AreEqual(JobStatus.Cancelled, result.Status);
        Assert.IsFalse(Directory.Exists(result.OutputPath));
        Assert.IsTrue(File.Exists(first));
        Assert.IsTrue(File.Exists(Path.Combine(folder, "second.txt")));
    }

    [TestMethod]
    public async Task RunAsync_CancelledFolderRemovesOwnedIncompleteOutput()
    {
        using var fixture = new CopyFixture();
        var source = fixture.CreateDirectory("cancel-source");
        fixture.CreateFile(
            Path.Combine("cancel-source", "one.txt"),
            new string('x', 1024 * 1024));
        var destination = fixture.CreateDirectory("destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            progress: null,
            cancellation.Token);

        Assert.AreEqual(JobStatus.Cancelled, result.Status);
        Assert.IsFalse(Directory.Exists(result.OutputPath));
        StringAssert.Contains(
            result.Summary,
            "entire dated output was removed");
        Assert.IsTrue(File.Exists(Path.Combine(source, "one.txt")));
    }

    [TestMethod]
    public async Task RunAsync_UsesConservativeConcurrencyForLargeFiles()
    {
        using var fixture = new CopyFixture();
        var source = fixture.CreateDirectory("large-profile-source");
        fixture.CreateFile(
            Path.Combine("large-profile-source", "one.txt"),
            "profile");
        var destination = fixture.CreateDirectory("destination");
        var output = Path.Combine(destination, "large-profile-output");
        var job = new JobSpec(
            Guid.NewGuid(),
            JobAction.Copy,
            source,
            destination,
            output,
            SourceIsDirectory: true,
            TotalBytes: 7,
            TotalFiles: 1,
            DateTimeOffset.Now,
            LargestFileBytes: 256L * 1024 * 1024);

        var result = await new CopyJobRunner().RunAsync(
            job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, result.Status, result.EngineDetails);
        StringAssert.Contains(result.EngineDetails, "Concurrent files: 2");
        StringAssert.Contains(
            result.EngineDetails,
            "temporary file per active item");
    }

    [TestMethod]
    public async Task RunAsync_PausesBetweenFilesAndResumesToCompletion()
    {
        using var fixture = new CopyFixture();
        var source = CreatePauseFixture(fixture);
        var destination = fixture.CreateDirectory("pause-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var pause = new CopyPauseController();
        var requested = false;
        var progress = new InlineProgress<JobProgress>(update =>
        {
            if (!requested &&
                update.Stage == "Copying" &&
                update.FilesCompleted >= 1)
            {
                requested = pause.RequestPause();
            }
        });
        var run = new CopyJobRunner(pause).RunAsync(
            preflight.Job,
            progress);

        await WaitUntilAsync(() => pause.IsPaused);
        var completedWhilePaused = Directory.EnumerateFiles(
            preflight.Job.OutputPath,
            "*",
            SearchOption.AllDirectories).ToArray();
        Assert.IsGreaterThan(0, completedWhilePaused.Length);
        Assert.IsLessThan(
            (int)preflight.Job.TotalFiles,
            completedWhilePaused.Length);
        Assert.IsFalse(completedWhilePaused.Any(path =>
            path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)));

        Assert.IsTrue(pause.Resume());
        var result = await run;

        Assert.AreEqual(JobStatus.Completed, result.Status, result.EngineDetails);
        Assert.AreEqual(preflight.Job.TotalFiles, result.FilesProcessed);
    }

    [TestMethod]
    public async Task RunAsync_CancelWhilePausedRemovesAllJobOutput()
    {
        using var fixture = new CopyFixture();
        var source = CreatePauseFixture(fixture);
        var destination = fixture.CreateDirectory("cancel-pause-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var pause = new CopyPauseController();
        using var cancellation = new CancellationTokenSource();
        var requested = false;
        var progress = new InlineProgress<JobProgress>(update =>
        {
            if (!requested && update.FilesCompleted >= 1)
            {
                requested = pause.RequestPause();
            }
        });
        var run = new CopyJobRunner(pause).RunAsync(
            preflight.Job,
            progress,
            cancellation.Token);

        await WaitUntilAsync(() => pause.IsPaused);
        cancellation.Cancel();
        var result = await run;

        Assert.AreEqual(JobStatus.Cancelled, result.Status);
        Assert.IsFalse(Directory.Exists(result.OutputPath));
        Assert.AreEqual(
            preflight.Job.TotalFiles,
            Directory.EnumerateFiles(
                source,
                "*",
                SearchOption.AllDirectories).LongCount());
    }

    [TestMethod]
    public async Task RunAsync_SourceChangeWhilePausedFailsAndPreservesPartialOutput()
    {
        using var fixture = new CopyFixture();
        var source = CreatePauseFixture(fixture);
        var destination = fixture.CreateDirectory("change-pause-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var pause = new CopyPauseController();
        var requested = false;
        var progress = new InlineProgress<JobProgress>(update =>
        {
            if (!requested && update.FilesCompleted >= 1)
            {
                requested = pause.RequestPause();
            }
        });
        var run = new CopyJobRunner(pause).RunAsync(
            preflight.Job,
            progress);

        await WaitUntilAsync(() => pause.IsPaused);
        var completedDestination = Directory.EnumerateFiles(
            preflight.Job.OutputPath,
            "*",
            SearchOption.AllDirectories).First();
        var relative = Path.GetRelativePath(
            preflight.Job.OutputPath,
            completedDestination);
        var completedSource = Path.Combine(source, relative);
        await File.AppendAllTextAsync(completedSource, "changed");
        Assert.IsTrue(pause.Resume());

        var result = await run;
        Assert.AreEqual(JobStatus.Failed, result.Status);
        StringAssert.Contains(result.Summary, "preserved");
        Assert.IsTrue(Directory.Exists(result.OutputPath));
    }

    [TestMethod]
    public async Task RunAsync_VerifiesGeneratedBinaryFilesBySha256()
    {
        using var fixture = new CopyFixture();
        var source = fixture.CreateDirectory("hash-source");
        var expected = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var random = new Random(20260727);

        for (var index = 0; index < 4; index++)
        {
            var relative = Path.Combine(
                index % 2 == 0 ? "even" : "odd",
                $"payload-{index}.bin");
            var path = Path.Combine(source, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bytes = new byte[4 * 1024 * 1024];
            random.NextBytes(bytes);
            await File.WriteAllBytesAsync(path, bytes);
            expected[relative] = Convert.ToHexString(SHA256.HashData(bytes));
        }

        var destination = fixture.CreateDirectory("hash-destination");
        var preflight = await new JobPlanner().PlanCopyAsync(
            source,
            destination,
            DateTimeOffset.Now);

        Assert.IsNotNull(preflight.Job);
        var result = await new CopyJobRunner().RunAsync(
            preflight.Job,
            progress: null);

        Assert.AreEqual(JobStatus.Completed, result.Status, result.EngineDetails);
        foreach (var item in expected)
        {
            await using var stream = File.OpenRead(
                Path.Combine(result.OutputPath, item.Key));
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            Assert.AreEqual(item.Value, actual, item.Key);
        }
    }

    private static string CreatePauseFixture(CopyFixture fixture)
    {
        var source = fixture.CreateDirectory("pause-source");
        for (var index = 0; index < 24; index++)
        {
            fixture.CreateFile(
                Path.Combine(
                    "pause-source",
                    $"file-{index:00}.txt"),
                new string((char)('a' + index % 26), 64 * 1024));
        }

        return source;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class CopyFixture : IDisposable
    {
        public CopyFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"ARCHive-integration-{Guid.NewGuid():N}");
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
