using ARCHive.Core;
using ARCHive.Infrastructure;
using System.Runtime.Versioning;

namespace ARCHive.IntegrationTests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class BetaTrialGateTests
{
    [TestMethod]
    public void Check_PersistsSevenDayTrialAcrossLaunches()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var gate = new BetaTrialGate(
                Path.Combine(directory, "trial.dat"));
            var firstRun = new DateTimeOffset(
                2026,
                7,
                27,
                0,
                0,
                0,
                TimeSpan.Zero);

            var first = gate.Check(firstRun);
            var second = gate.Check(firstRun + TimeSpan.FromDays(1));
            var expired = gate.Check(firstRun + TimeSpan.FromDays(7));

            Assert.IsTrue(first.IsFirstRun);
            Assert.IsTrue(first.Decision.IsAllowed);
            Assert.IsFalse(second.IsFirstRun);
            Assert.IsTrue(second.Decision.IsAllowed);
            Assert.IsFalse(expired.Decision.IsAllowed);
            Assert.AreEqual(
                BetaTrialStatus.Expired,
                expired.Decision.Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Check_PersistsClockRollbackLock()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var gate = new BetaTrialGate(
                Path.Combine(directory, "trial.dat"));
            var firstRun = new DateTimeOffset(
                2026,
                7,
                27,
                0,
                0,
                0,
                TimeSpan.Zero);

            gate.Check(firstRun);
            gate.Check(firstRun + TimeSpan.FromHours(2));
            var rollback = gate.Check(firstRun + TimeSpan.FromHours(1));
            var later = gate.Check(firstRun + TimeSpan.FromHours(3));

            Assert.AreEqual(
                BetaTrialStatus.ClockRollback,
                rollback.Decision.Status);
            Assert.AreEqual(
                BetaTrialStatus.ClockRollback,
                later.Decision.Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "ARCHive-BetaTrialTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
