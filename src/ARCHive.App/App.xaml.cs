using System.Windows;
using ARCHive.Core;
using ARCHive.Infrastructure;

namespace ARCHive.App;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;
    private readonly object _pendingArgsLock = new();
    private readonly Queue<string[]> _pendingArgs = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstance = new SingleInstanceService();
        _singleInstance.ArgumentsReceived += OnSecondInstanceArgs;
        if (!_singleInstance.TryAcquire(e.Args))
        {
            if (!_singleInstance.LastForwardSucceeded)
            {
                new BetaNoticeWindow(
                    "Could not add the selection",
                    "ARCHive is already running, but Windows could not pass " +
                    "this selection to it." + Environment.NewLine +
                    Environment.NewLine +
                    "Please add the files from the open ARCHive window." +
                    (string.IsNullOrWhiteSpace(_singleInstance.LastForwardError)
                        ? string.Empty
                        : Environment.NewLine + Environment.NewLine +
                          "Technical detail: " +
                          _singleInstance.LastForwardError),
                    "Close").ShowDialog();
            }

            Shutdown();
            return;
        }

        _singleInstance.StartListening();

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

        var contextMenu = ContextMenuArgs.Parse(e.Args);

        if (contextMenu.HasError)
        {
            new BetaNoticeWindow(
                "Context menu error",
                contextMenu.ErrorMessage +
                Environment.NewLine + Environment.NewLine +
                "You can also open ARCHive directly and add files from " +
                "within the application.",
                "Exit ARCHive").ShowDialog();
            Shutdown();
            return;
        }

        MainWindow mainWindow;
        if (!contextMenu.IsEmpty)
        {
            var action = contextMenu.Action switch
            {
                ContextMenuAction.Copy => JobAction.Copy,
                ContextMenuAction.CreateArchive => JobAction.CreateArchive,
                ContextMenuAction.ExtractArchive => JobAction.ExtractArchive,
                _ => JobAction.Copy
            };
            mainWindow = new MainWindow(action, contextMenu.SourcePaths);
        }
        else
        {
            mainWindow = new MainWindow();
        }

        MainWindow = mainWindow;
        mainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DrainPendingArguments();
    }

    private void OnSecondInstanceArgs(string[] args)
    {
        lock (_pendingArgsLock)
        {
            _pendingArgs.Enqueue(args);
        }

        _ = Dispatcher.BeginInvoke(DrainPendingArguments);
    }

    private void DrainPendingArguments()
    {
        if (MainWindow is not MainWindow mainWindow)
        {
            return;
        }

        while (true)
        {
            string[] args;
            lock (_pendingArgsLock)
            {
                if (_pendingArgs.Count == 0)
                {
                    return;
                }

                args = _pendingArgs.Dequeue();
            }

            var parsed = ContextMenuArgs.Parse(args);
            if (!parsed.HasError && !parsed.IsEmpty)
            {
                mainWindow.InjectFromContextMenu(
                    parsed.Action.ToJobAction(),
                    parsed.SourcePaths);
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

internal static class ContextMenuActionExtensions
{
    public static JobAction ToJobAction(this ContextMenuAction action) =>
        action switch
        {
            ContextMenuAction.Copy => JobAction.Copy,
            ContextMenuAction.CreateArchive => JobAction.CreateArchive,
            ContextMenuAction.ExtractArchive => JobAction.ExtractArchive,
            _ => JobAction.Copy
        };
}
