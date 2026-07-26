using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ARCHive.Core;

namespace ARCHive.Infrastructure;

public sealed record BetaTrialCheck(
    BetaTrialDecision Decision,
    bool IsFirstRun,
    string? Error = null);

[SupportedOSPlatform("windows")]
public sealed class BetaTrialGate
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("ARCHive.Beta.1.0.0.SevenDayTrial");

    private readonly BetaTrialPolicy _policy = new();
    private readonly string _statePath;

    public BetaTrialGate(string? statePath = null)
    {
        _statePath = statePath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ARCHive",
            "Beta",
            "trial.dat");
    }

    public BetaTrialCheck Check(DateTimeOffset nowUtc)
    {
        try
        {
            var isFirstRun = !File.Exists(_statePath);
            var state = isFirstRun
                ? new BetaTrialState(nowUtc, nowUtc)
                : LoadState();
            var decision = _policy.Evaluate(state, nowUtc);

            var updatedState = decision.IsAllowed
                ? state with { LastRunUtc = nowUtc }
                : state with
                {
                    Locked = true,
                    LockReason = decision.Status
                };
            SaveState(updatedState);

            return new BetaTrialCheck(decision, isFirstRun);
        }
        catch (Exception exception)
        {
            var unavailable = new BetaTrialDecision(
                BetaTrialStatus.InvalidState,
                DateTimeOffset.MinValue,
                TimeSpan.Zero);
            return new BetaTrialCheck(
                unavailable,
                IsFirstRun: false,
                Error: exception.Message);
        }
    }

    private BetaTrialState LoadState()
    {
        var encrypted = File.ReadAllBytes(_statePath);
        var json = ProtectedData.Unprotect(
            encrypted,
            Entropy,
            DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<BetaTrialState>(json)
            ?? throw new InvalidDataException(
                "The local beta trial record is empty.");
    }

    private void SaveState(BetaTrialState state)
    {
        var directory = Path.GetDirectoryName(_statePath)
            ?? throw new InvalidOperationException(
                "The beta trial folder is unavailable.");
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.SerializeToUtf8Bytes(state);
        var encrypted = ProtectedData.Protect(
            json,
            Entropy,
            DataProtectionScope.CurrentUser);
        var temporaryPath = _statePath + ".new";
        File.WriteAllBytes(temporaryPath, encrypted);
        File.Move(temporaryPath, _statePath, overwrite: true);
    }
}
