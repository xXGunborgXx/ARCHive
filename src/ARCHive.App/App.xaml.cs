using System.Windows;
using ARCHive.Core;
using ARCHive.Infrastructure;

namespace ARCHive.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var trial = new BetaTrialGate().Check(DateTimeOffset.UtcNow);
        if (!trial.Decision.IsAllowed)
        {
            var message = trial.Decision.Status switch
            {
                BetaTrialStatus.Expired =>
                    "This seven-day ARCHive beta has expired.",
                BetaTrialStatus.ClockRollback =>
                    "ARCHive detected that the Windows clock moved backwards. " +
                    "The beta has been locked to protect the trial period.",
                _ =>
                    "The local ARCHive beta trial record could not be verified."
            };
            if (!string.IsNullOrWhiteSpace(trial.Error))
            {
                message += Environment.NewLine + Environment.NewLine +
                    "Technical detail: " + trial.Error;
            }

            new BetaNoticeWindow(
                "Beta unavailable",
                message + Environment.NewLine + Environment.NewLine +
                "Please send your completed questionnaire or request help at " +
                "GunborgServers@gmail.com.",
                "Exit ARCHive").ShowDialog();
            Shutdown();
            return;
        }

        if (trial.IsFirstRun)
        {
            var accepted = new BetaNoticeWindow(
                "Seven-day beta activated",
                "Your ARCHive beta begins now and expires on " +
                trial.Decision.ExpiresUtc.ToLocalTime()
                    .ToString("MMMM d, yyyy 'at' h:mm tt") +
                "." + Environment.NewLine + Environment.NewLine +
                "The trial is checked only when ARCHive starts. An operation " +
                "already in progress will never be interrupted by expiry.",
                "Start Beta").ShowDialog();
            if (accepted != true)
            {
                Shutdown();
                return;
            }
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }
}
