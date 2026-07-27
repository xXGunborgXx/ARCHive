using ARCHive.App;

namespace ARCHive.IntegrationTests;

[TestClass]
public sealed class SingleInstanceServiceTests
{
    [TestMethod]
    public async Task SecondInstance_ForwardsEveryArgumentToFirstInstance()
    {
        var unique = Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceService(
            $"Local\\ARCHive_Test_{unique}",
            $"ARCHive_Test_Pipe_{unique}");
        Assert.IsTrue(first.TryAcquire([]));

        var received = new TaskCompletionSource<string[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        first.ArgumentsReceived += args => received.TrySetResult(args);
        first.StartListening();

        var expected = new[]
        {
            "--copy",
            @"C:\Test Files\one.bin",
            @"D:\Other Folder\two.iso"
        };
        using var second = new SingleInstanceService(
            $"Local\\ARCHive_Test_{unique}",
            $"ARCHive_Test_Pipe_{unique}");

        Assert.IsFalse(second.TryAcquire(expected));
        Assert.IsTrue(
            second.LastForwardSucceeded,
            second.LastForwardError);

        var actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(expected, actual);
    }
}
