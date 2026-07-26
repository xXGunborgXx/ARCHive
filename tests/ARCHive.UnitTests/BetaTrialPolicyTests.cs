using ARCHive.Core;

namespace ARCHive.UnitTests;

[TestClass]
public sealed class BetaTrialPolicyTests
{
    private static readonly DateTimeOffset FirstRun =
        new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);

    private readonly BetaTrialPolicy _policy = new();

    [TestMethod]
    public void Evaluate_AllowsFirstSevenDays()
    {
        var state = new BetaTrialState(FirstRun, FirstRun);

        var result = _policy.Evaluate(
            state,
            FirstRun + TimeSpan.FromDays(6));

        Assert.IsTrue(result.IsAllowed);
        Assert.AreEqual(
            BetaTrialStatus.Active,
            result.Status);
        Assert.AreEqual(
            TimeSpan.FromDays(1),
            result.Remaining);
    }

    [TestMethod]
    public void Evaluate_ExpiresAtExactlySevenDays()
    {
        var state = new BetaTrialState(FirstRun, FirstRun);

        var result = _policy.Evaluate(
            state,
            FirstRun + TimeSpan.FromDays(7));

        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(
            BetaTrialStatus.Expired,
            result.Status);
    }

    [TestMethod]
    public void Evaluate_AllowsSmallClockCorrection()
    {
        var lastRun = FirstRun + TimeSpan.FromHours(2);
        var state = new BetaTrialState(FirstRun, lastRun);

        var result = _policy.Evaluate(
            state,
            lastRun - TimeSpan.FromMinutes(10));

        Assert.IsTrue(result.IsAllowed);
    }

    [TestMethod]
    public void Evaluate_LocksLargerClockRollback()
    {
        var lastRun = FirstRun + TimeSpan.FromHours(2);
        var state = new BetaTrialState(FirstRun, lastRun);

        var result = _policy.Evaluate(
            state,
            lastRun - TimeSpan.FromMinutes(16));

        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(
            BetaTrialStatus.ClockRollback,
            result.Status);
    }

    [TestMethod]
    public void Evaluate_PreservesExistingLock()
    {
        var state = new BetaTrialState(
            FirstRun,
            FirstRun,
            Locked: true,
            LockReason: BetaTrialStatus.ClockRollback);

        var result = _policy.Evaluate(
            state,
            FirstRun + TimeSpan.FromHours(1));

        Assert.IsFalse(result.IsAllowed);
        Assert.AreEqual(
            BetaTrialStatus.ClockRollback,
            result.Status);
    }
}
