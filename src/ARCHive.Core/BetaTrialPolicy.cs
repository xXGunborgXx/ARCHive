namespace ARCHive.Core;

public enum BetaTrialStatus
{
    Active,
    Expired,
    ClockRollback,
    InvalidState
}

public sealed record BetaTrialState(
    DateTimeOffset FirstRunUtc,
    DateTimeOffset LastRunUtc,
    bool Locked = false,
    BetaTrialStatus? LockReason = null);

public sealed record BetaTrialDecision(
    BetaTrialStatus Status,
    DateTimeOffset ExpiresUtc,
    TimeSpan Remaining)
{
    public bool IsAllowed => Status == BetaTrialStatus.Active;
}

public sealed class BetaTrialPolicy
{
    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(7);
    public static readonly TimeSpan ClockRollbackTolerance =
        TimeSpan.FromMinutes(15);

    public BetaTrialDecision Evaluate(
        BetaTrialState state,
        DateTimeOffset nowUtc)
    {
        var expiresUtc = state.FirstRunUtc + TrialLength;
        if (state.Locked)
        {
            return new BetaTrialDecision(
                state.LockReason ?? BetaTrialStatus.InvalidState,
                expiresUtc,
                TimeSpan.Zero);
        }

        if (nowUtc + ClockRollbackTolerance < state.LastRunUtc)
        {
            return new BetaTrialDecision(
                BetaTrialStatus.ClockRollback,
                expiresUtc,
                TimeSpan.Zero);
        }

        if (nowUtc >= expiresUtc)
        {
            return new BetaTrialDecision(
                BetaTrialStatus.Expired,
                expiresUtc,
                TimeSpan.Zero);
        }

        return new BetaTrialDecision(
            BetaTrialStatus.Active,
            expiresUtc,
            expiresUtc - nowUtc);
    }
}
