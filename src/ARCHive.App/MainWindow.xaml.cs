using System.Diagnostics;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using ARCHive.Archive;
using ARCHive.Copy;
using ARCHive.Core;
using ARCHive.Infrastructure;
using Microsoft.Win32;

namespace ARCHive.App;

public partial class MainWindow : Window
{
    private readonly JobPlanner _copyPlanner = new();
    private readonly ArchiveJobPlanner _archivePlanner = new();
    private readonly CopyPauseController _copyPauseController = new();
    private readonly ICopyJobRunner _copyRunner;
    private readonly IArchiveJobRunner _archiveRunner = new SevenZipArchiveRunner();
    private readonly IJobLogger _logger = new JsonJobLogger();
    private readonly TransferRateEstimator _transferRateEstimator = new();
    private readonly Stopwatch _jobStopwatch = new();
    private readonly ObservableCollection<string> _copySourcePaths = [];
    private readonly ObservableCollection<string> _archiveSourcePaths = [];
    private CancellationTokenSource? _preflightCancellation;
    private CancellationTokenSource? _jobCancellation;
    private PreflightResult? _copyPreflight;
    private ArchivePlanResult<ArchiveCreateSpec>? _createPreflight;
    private ArchivePlanResult<ArchiveExtractSpec>? _extractPreflight;
    private JobResult? _lastResult;
    private string? _lastLogPath;
    private JobAction _selectedAction = JobAction.Copy;
    private bool _jobRunning;
    private bool _closeAfterCancellation;
    private bool _updatingSourceUi;
    private string _extractSourcePath = string.Empty;

    public MainWindow()
    {
        _copyRunner = new CopyJobRunner(_copyPauseController);
        InitializeComponent();
    }

    private void OnTitleBarMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void OnMinimizeWindow(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnCloseWindow(object sender, RoutedEventArgs e) =>
        Close();

    private void OnSelectCopy(object sender, RoutedEventArgs e) =>
        SelectAction(JobAction.Copy);

    private void OnSelectCreateArchive(object sender, RoutedEventArgs e) =>
        SelectAction(JobAction.CreateArchive);

    private void OnSelectExtractArchive(object sender, RoutedEventArgs e) =>
        SelectAction(JobAction.ExtractArchive);

    private void SelectAction(JobAction action)
    {
        if (_jobRunning)
        {
            return;
        }

        var previousAction = _selectedAction;
        CaptureCurrentSource(previousAction);
        _selectedAction = action;
        SetActionButtonState(CopyActionButton, action == JobAction.Copy);
        SetActionButtonState(
            CreateArchiveActionButton,
            action == JobAction.CreateArchive);
        SetActionButtonState(
            ExtractArchiveActionButton,
            action == JobAction.ExtractArchive);

        ArchiveOptionsPanel.Visibility = action == JobAction.CreateArchive
            ? Visibility.Visible
            : Visibility.Collapsed;
        ChooseFileButton.Content = action is
            JobAction.Copy or JobAction.CreateArchive
            ? "Add File(s)"
            : "Choose File";
        ChooseSourceFolderButton.Content = action is
            JobAction.Copy or JobAction.CreateArchive
            ? "Add Folder(s)"
            : "Choose Folder";
        ChooseSourceFolderButton.Visibility =
            action == JobAction.ExtractArchive
                ? Visibility.Collapsed
                : Visibility.Visible;
        AutomationProperties.SetName(
            ChooseFileButton,
            action is JobAction.Copy or JobAction.CreateArchive
                ? "Add one or more source files"
                : "Choose source file");
        AutomationProperties.SetName(
            ChooseSourceFolderButton,
            action is JobAction.Copy or JobAction.CreateArchive
                ? "Add one or more source folders"
                : "Choose source folder");

        var activeSources = ActiveSourceCollection();
        if (activeSources is { Count: > 0 })
        {
            RefreshSelectedSourceUi();
        }
        else
        {
            SetSourceText(
                action == JobAction.ExtractArchive
                    ? _extractSourcePath
                    : string.Empty,
                isReadOnly: false);
            ClearSourcesButton.Visibility = Visibility.Collapsed;
        }

        SourceHelpText.Text = action switch
        {
            JobAction.ExtractArchive =>
                "Drop a 7z or ZIP archive here, paste its path, or browse.",
            JobAction.CreateArchive =>
                "Add files and folders to one archive; every choice stays in the same list.",
            _ => "Add files and folders separately; every choice stays in the same list."
        };
        DestinationHelpText.Text = action switch
        {
            JobAction.ExtractArchive =>
                "Choose where the new dated extraction folder will be created.",
            JobAction.CreateArchive =>
                "Choose where the new dated archive will be created.",
            _ => "Choose where the new dated copy will be created."
        };
        StartButton.Content = action switch
        {
            JobAction.CreateArchive => "Create Archive",
            JobAction.ExtractArchive => "Extract Archive",
            _ => "Start Copy"
        };
        AutomationProperties.SetName(
            StartButton,
            action switch
            {
                JobAction.CreateArchive => "Create archive",
                JobAction.ExtractArchive => "Extract archive",
                _ => "Start copy"
            });

        ResultPanel.Visibility = Visibility.Collapsed;
        QueuePreflight();
    }

    private void OnChooseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = _selectedAction == JobAction.ExtractArchive
                ? "Choose a 7z or ZIP archive"
                : _selectedAction is
                    JobAction.Copy or JobAction.CreateArchive
                    ? "Add file(s) - folders are for navigation only"
                    : "Choose a file",
            CheckFileExists = true,
            Multiselect = _selectedAction is
                JobAction.Copy or JobAction.CreateArchive,
            Filter = _selectedAction == JobAction.ExtractArchive
                ? "Supported archives (*.7z;*.zip)|*.7z;*.zip|All files (*.*)|*.*"
                : "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            if (_selectedAction is
                JobAction.Copy or JobAction.CreateArchive)
            {
                AddSelectedSources(dialog.FileNames);
            }
            else
            {
                SourceTextBox.Text = dialog.FileName;
            }
        }
    }

    private void OnChooseSourceFolder(object sender, RoutedEventArgs e)
    {
        if (_selectedAction == JobAction.ExtractArchive)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = _selectedAction == JobAction.CreateArchive
                ? "Add folder(s) to the archive - files cannot be selected"
                : "Add folder(s) - files cannot be selected",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            if (_selectedAction is
                JobAction.Copy or JobAction.CreateArchive)
            {
                AddSelectedSources(dialog.FolderNames);
            }
            else
            {
                SourceTextBox.Text = dialog.FolderName;
            }
        }
    }

    private void OnChooseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose the destination folder",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            DestinationTextBox.Text = dialog.FolderName;
        }
    }

    private void OnPathTextChanged(
        object sender,
        System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_updatingSourceUi)
        {
            if (_selectedAction == JobAction.ExtractArchive)
            {
                _extractSourcePath = SourceTextBox.Text;
            }

            QueuePreflight();
        }
    }

    private void OnArchiveOptionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded && _selectedAction == JobAction.CreateArchive)
        {
            QueuePreflight();
        }
    }

    private void OnArchiveOptionChanged(object sender, RoutedEventArgs e)
    {
        if (IsLoaded && _selectedAction == JobAction.CreateArchive)
        {
            QueuePreflight();
        }
    }

    private async void QueuePreflight()
    {
        if (_jobRunning || !IsInitialized)
        {
            return;
        }

        _preflightCancellation?.Cancel();
        _preflightCancellation?.Dispose();
        _preflightCancellation = new CancellationTokenSource();
        var token = _preflightCancellation.Token;

        StartButton.IsEnabled = false;
        ResultPanel.Visibility = Visibility.Collapsed;
        PreflightPanel.Visibility = Visibility.Visible;
        ClearPreflightResults();

        var copySources = GetCopySourcePaths();
        var archiveSources = GetArchiveSourcePaths();
        var sourceMissing = _selectedAction switch
        {
            JobAction.Copy => copySources.Count == 0,
            JobAction.CreateArchive => archiveSources.Count == 0,
            _ => string.IsNullOrWhiteSpace(SourceTextBox.Text)
        };
        if (sourceMissing ||
            string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            PreflightTitle.Text = "Choose a source and destination";
            PreflightDetails.Text = "ARCHive will check the paths automatically.";
            return;
        }

        try
        {
            await Task.Delay(300, token);
            PreflightTitle.Text = "Preparing...";
            PreflightDetails.Text =
                "Checking the source and destination without writing anything.";
            var createdAt = DateTimeOffset.Now;

            switch (_selectedAction)
            {
                case JobAction.Copy:
                    _copyPreflight = await _copyPlanner.PlanCopyAsync(
                        copySources,
                        DestinationTextBox.Text,
                        createdAt,
                        token);
                    ShowCopyPreflight(_copyPreflight);
                    break;

                case JobAction.CreateArchive:
                    _createPreflight = await _archivePlanner.PlanCreateAsync(
                        archiveSources,
                        DestinationTextBox.Text,
                        SelectedArchiveFormat(),
                        SelectedCompressionPreset(),
                        createdAt,
                        SelectedVerifyAfterCreate(),
                        token);
                    ShowCreatePreflight(_createPreflight);
                    break;

                case JobAction.ExtractArchive:
                    _extractPreflight = await _archivePlanner.PlanExtractAsync(
                        SourceTextBox.Text,
                        DestinationTextBox.Text,
                        createdAt,
                        token);
                    ShowExtractPreflight(_extractPreflight);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer path, action, or option replaced this preflight.
        }
    }

    private void ShowCopyPreflight(PreflightResult result)
    {
        if (!TryShowValidation(result.Job, result.Issues))
        {
            return;
        }

        var job = result.Job!;
        PreflightTitle.Text = $"Ready to copy {FormatCount(job.TotalFiles, "file")}";
        PreflightDetails.Text = BuildPreflightDetails(
            job.TotalBytes,
            job.OutputPath,
            result.DestinationFreeBytes);
        StartButton.IsEnabled = true;
    }

    private void ShowCreatePreflight(
        ArchivePlanResult<ArchiveCreateSpec> result)
    {
        if (!TryShowValidation(result.Job, result.Issues))
        {
            return;
        }

        var job = result.Job!;
        var typeName = job.Format == ArchiveFormat.SevenZip ? "7z" : "ZIP";
        PreflightTitle.Text =
            $"Ready to create {typeName} from {FormatCount(job.TotalFiles, "file")}";
        PreflightDetails.Text = BuildPreflightDetails(
            job.TotalBytes,
            job.OutputPath,
            result.DestinationFreeBytes);
        StartButton.IsEnabled = true;
    }

    private void ShowExtractPreflight(
        ArchivePlanResult<ArchiveExtractSpec> result)
    {
        if (!TryShowValidation(result.Job, result.Issues))
        {
            return;
        }

        var job = result.Job!;
        PreflightTitle.Text = "Ready to inspect and extract";
        PreflightDetails.Text =
            $"{Path.GetFileName(job.ArchivePath)} → {job.OutputPath}" +
            (result.DestinationFreeBytes.HasValue
                ? $"{Environment.NewLine}{FormatBytes(result.DestinationFreeBytes.Value)} available at destination."
                : string.Empty);
        StartButton.IsEnabled = true;
    }

    private bool TryShowValidation<TJob>(
        TJob? job,
        IReadOnlyList<ValidationIssue> issues)
        where TJob : class
    {
        var error = issues.FirstOrDefault(
            issue => issue.Severity == ValidationSeverity.Error);
        if (error is null && job is not null)
        {
            return true;
        }

        PreflightTitle.Text = "Cannot start yet";
        PreflightDetails.Text = error?.Message ?? "Check the selected paths.";
        StartButton.IsEnabled = false;
        return false;
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_jobRunning)
        {
            return;
        }

        StartButton.IsEnabled = false;
        PreflightTitle.Text = "Checking again...";
        PreflightDetails.Text =
            "Confirming that the source and destination are still available.";
        var createdAt = DateTimeOffset.Now;

        switch (_selectedAction)
        {
            case JobAction.Copy:
                var copyPlan = await _copyPlanner.PlanCopyAsync(
                    GetCopySourcePaths(),
                    DestinationTextBox.Text,
                    createdAt);
                if (!copyPlan.IsValid || copyPlan.Job is null)
                {
                    _copyPreflight = copyPlan;
                    ShowCopyPreflight(copyPlan);
                    return;
                }

                BeginJob(
                    canPause: copyPlan.Job.SourceIsDirectory &&
                        copyPlan.Job.TotalFiles > 1);
                var copyResult = await _copyRunner.RunAsync(
                    copyPlan.Job,
                    new Progress<JobProgress>(ShowProgress),
                    _jobCancellation!.Token);
                var copyLog = await TryWriteLogAsync(
                    copyPlan.Job,
                    copyResult,
                    copyPlan.Job.CreatedAt);
                FinishJob(copyResult, copyLog);
                break;

            case JobAction.CreateArchive:
                var createPlan = await _archivePlanner.PlanCreateAsync(
                    GetArchiveSourcePaths(),
                    DestinationTextBox.Text,
                    SelectedArchiveFormat(),
                    SelectedCompressionPreset(),
                    createdAt,
                    SelectedVerifyAfterCreate());
                if (!createPlan.IsValid || createPlan.Job is null)
                {
                    _createPreflight = createPlan;
                    ShowCreatePreflight(createPlan);
                    return;
                }

                BeginJob(canPause: false);
                var createResult = await _archiveRunner.CreateAsync(
                    createPlan.Job,
                    new Progress<JobProgress>(ShowProgress),
                    _jobCancellation!.Token);
                var createLog = await TryWriteLogAsync(
                    createPlan.Job,
                    createResult,
                    createPlan.Job.CreatedAt);
                FinishJob(createResult, createLog);
                break;

            case JobAction.ExtractArchive:
                var extractPlan = await _archivePlanner.PlanExtractAsync(
                    SourceTextBox.Text,
                    DestinationTextBox.Text,
                    createdAt);
                if (!extractPlan.IsValid || extractPlan.Job is null)
                {
                    _extractPreflight = extractPlan;
                    ShowExtractPreflight(extractPlan);
                    return;
                }

                BeginJob(canPause: false);
                var extractResult = await _archiveRunner.ExtractAsync(
                    extractPlan.Job,
                    new Progress<JobProgress>(ShowProgress),
                    _jobCancellation!.Token);
                var extractLog = await TryWriteLogAsync(
                    extractPlan.Job,
                    extractResult,
                    extractPlan.Job.CreatedAt);
                FinishJob(extractResult, extractLog);
                break;
        }
    }

    private void BeginJob(bool canPause)
    {
        _jobRunning = true;
        _transferRateEstimator.Reset();
        _jobStopwatch.Restart();
        _jobCancellation = new CancellationTokenSource();
        InputsPanel.IsEnabled = false;
        ActionPanel.IsEnabled = false;
        ArchiveOptionsPanel.Visibility = Visibility.Collapsed;
        PreflightPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        CancelButton.IsEnabled = true;
        PauseButton.Visibility = canPause
            ? Visibility.Visible
            : Visibility.Collapsed;
        PauseButton.IsEnabled = canPause;
        PauseButton.Content = "Pause";
        TransferRatePanel.Visibility = Visibility.Collapsed;
        TransferSpeedText.Text = string.Empty;
    }

    private void FinishJob(JobResult result, DiagnosticLogResult log)
    {
        _lastResult = result;
        _lastLogPath = log.Path;
        _jobRunning = false;
        _jobStopwatch.Stop();
        _jobCancellation?.Dispose();
        _jobCancellation = null;
        InputsPanel.IsEnabled = true;
        ActionPanel.IsEnabled = true;
        ArchiveOptionsPanel.Visibility =
            _selectedAction == JobAction.CreateArchive
                ? Visibility.Visible
                : Visibility.Collapsed;
        CancelButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        PauseButton.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
        ShowResult(result, log);
        StartButton.IsEnabled = HasValidPreflight();

        if (_closeAfterCancellation)
        {
            _closeAfterCancellation = false;
            Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private async Task<DiagnosticLogResult> TryWriteLogAsync<TJob>(
        TJob job,
        JobResult result,
        DateTimeOffset createdAt)
    {
        try
        {
            var path = await _logger.WriteAsync(job, result, createdAt);
            return new DiagnosticLogResult(path, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DiagnosticLogResult(
                null,
                $"Diagnostic log could not be written: {ex.Message}");
        }
    }

    private void ShowProgress(JobProgress progress)
    {
        ProgressTitle.Text = progress.Stage;
        ProgressDetails.Text = progress.TotalBytes > 0
            ? $"{progress.Message}{Environment.NewLine}" +
              $"{FormatBytes(progress.BytesCompleted)} of {FormatBytes(progress.TotalBytes)}"
            : progress.Message;
        JobProgressBar.IsIndeterminate = progress.IsIndeterminate;
        if (progress.Stage == "Paused")
        {
            PauseButton.Content = "Resume";
            PauseButton.IsEnabled = true;
        }
        else if (progress.Stage == "Pausing")
        {
            PauseButton.Content = "Resume";
            PauseButton.IsEnabled = true;
        }
        else if (progress.Stage == "Verifying" ||
                 progress.FilesCompleted >= progress.TotalFiles)
        {
            PauseButton.IsEnabled = false;
        }
        else if (_copyPauseController.CanPause &&
                 !_copyPauseController.IsPauseRequested)
        {
            PauseButton.Content = "Pause";
            PauseButton.IsEnabled = true;
        }

        ShowTransferRate(progress);

        if (progress.Percent.HasValue)
        {
            JobProgressBar.Value = progress.Percent.Value;
            ProgressPercent.Text = $"{progress.Percent.Value:0}%";
        }
        else
        {
            ProgressPercent.Text = string.Empty;
        }
    }

    private void ShowTransferRate(JobProgress progress)
    {
        if (progress.IsIndeterminate ||
            progress.TotalBytes <= 0 ||
            progress.Stage is "Paused" or "Pausing")
        {
            TransferRatePanel.Visibility = Visibility.Collapsed;
            return;
        }

        TransferRatePanel.Visibility = Visibility.Visible;
        var rate = _transferRateEstimator.Update(
            progress.BytesCompleted,
            _jobStopwatch.Elapsed);

        if (rate.IsWaitingForProgress)
        {
            TransferSpeedText.Text = "Speed: waiting for storage...";
            return;
        }

        if (!rate.BytesPerSecond.HasValue)
        {
            TransferSpeedText.Text = "Speed: measuring...";
            return;
        }

        TransferSpeedText.Text =
            $"Speed: {FormatBytes((long)rate.BytesPerSecond.Value)}/s";
    }

    private void ShowResult(JobResult result, DiagnosticLogResult log)
    {
        ResultPanel.Visibility = Visibility.Visible;
        ResultTitle.Text = result.Status switch
        {
            JobStatus.Completed => "Completed",
            JobStatus.CompletedWithWarnings => "Completed with warnings",
            JobStatus.Cancelled => "Cancelled",
            _ => "Operation failed"
        };

        var outputExists =
            File.Exists(result.OutputPath) || Directory.Exists(result.OutputPath);
        var processedLabel = result.Status == JobStatus.Cancelled
            ? "Work before cancellation"
            : "Processed";
        ResultDetails.Text =
            $"{result.Summary}{Environment.NewLine}" +
            $"{(outputExists ? "Output" : "Planned output")}: {result.OutputPath}{Environment.NewLine}" +
            $"{processedLabel}: {FormatBytes(result.BytesProcessed)}" +
            (result.FilesProcessed > 0
                ? $" in {FormatCount(result.FilesProcessed, "file")}"
                : string.Empty) +
            $"{Environment.NewLine}Elapsed: {FormatDuration(result.Duration)}" +
            (string.IsNullOrWhiteSpace(log.Error)
                ? string.Empty
                : $"{Environment.NewLine}{log.Error}");

        OpenDestinationButton.IsEnabled = outputExists;
        OpenDiagnosticLogButton.IsEnabled =
            !string.IsNullOrWhiteSpace(log.Path) && File.Exists(log.Path);

        if (OpenDestinationButton.IsEnabled)
        {
            OpenDestinationButton.Focus();
        }
        else if (OpenDiagnosticLogButton.IsEnabled)
        {
            OpenDiagnosticLogButton.Focus();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        RequestCancellation();
    }

    private void OnPause(object sender, RoutedEventArgs e)
    {
        if (!_jobRunning || !_copyPauseController.CanPause)
        {
            return;
        }

        if (_copyPauseController.IsPauseRequested)
        {
            if (_copyPauseController.Resume())
            {
                PauseButton.Content = "Pause";
                ProgressTitle.Text = "Resuming";
                ProgressDetails.Text = "Starting the next file...";
            }

            return;
        }

        if (_copyPauseController.RequestPause())
        {
            PauseButton.Content = "Resume";
            ProgressTitle.Text = "Pausing";
            ProgressDetails.Text =
                "Finishing active files. No new files will be started.";
            TransferRatePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void RequestCancellation()
    {
        if (!_jobRunning || _jobCancellation?.IsCancellationRequested == true)
        {
            return;
        }

        CancelButton.IsEnabled = false;
        ProgressTitle.Text = "Cancelling...";
        ProgressDetails.Text = "Stopping safely. The source will not be changed.";
        TransferRatePanel.Visibility = Visibility.Collapsed;
        _jobCancellation?.Cancel();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _jobRunning)
        {
            RequestCancellation();
            e.Handled = true;
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!_jobRunning)
        {
            return;
        }

        e.Cancel = true;
        _closeAfterCancellation = true;
        RequestCancellation();
    }

    private void OnOpenDestination(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        var navigation = DestinationNavigation.Plan(_lastResult.OutputPath);
        if (navigation is null)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add(navigation.DirectoryPath);
        Process.Start(startInfo);
    }

    private void OnOpenDiagnosticLog(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastLogPath) ||
            !File.Exists(_lastLogPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastLogPath,
            UseShellExecute = true
        });
    }

    private void OnSourceDragOver(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e);
        var allowed = paths.Count > 0 &&
            paths.All(path =>
                File.Exists(path) ||
                (_selectedAction != JobAction.ExtractArchive &&
                 Directory.Exists(path))) &&
            (_selectedAction != JobAction.ExtractArchive ||
             paths.Count == 1);
        e.Effects = allowed ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSourceDrop(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e);
        if ((_selectedAction is
                JobAction.Copy or JobAction.CreateArchive) &&
            paths.Count > 0 &&
            paths.All(path => File.Exists(path) || Directory.Exists(path)))
        {
            AddSelectedSources(paths);
        }
        else if (paths.Count == 1 &&
                 (_selectedAction != JobAction.ExtractArchive ||
                  File.Exists(paths[0])))
        {
            SourceTextBox.Text = paths[0];
        }
    }

    private void OnDestinationDragOver(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e);
        e.Effects = paths.Count == 1 && Directory.Exists(paths[0])
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDestinationDrop(object sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e);
        if (paths.Count == 1 && Directory.Exists(paths[0]))
        {
            DestinationTextBox.Text = paths[0];
        }
    }

    private static IReadOnlyList<string> GetDroppedPaths(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return [];
        }

        return e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths
            : [];
    }

    private IReadOnlyList<string> GetCopySourcePaths()
    {
        if (_copySourcePaths.Count > 0)
        {
            return _copySourcePaths.ToArray();
        }

        return string.IsNullOrWhiteSpace(SourceTextBox.Text)
            ? []
            : [SourceTextBox.Text.Trim()];
    }

    private IReadOnlyList<string> GetArchiveSourcePaths()
    {
        if (_archiveSourcePaths.Count > 0)
        {
            return _archiveSourcePaths.ToArray();
        }

        return string.IsNullOrWhiteSpace(SourceTextBox.Text)
            ? []
            : [SourceTextBox.Text.Trim()];
    }

    private ObservableCollection<string>? ActiveSourceCollection() =>
        _selectedAction switch
        {
            JobAction.Copy => _copySourcePaths,
            JobAction.CreateArchive => _archiveSourcePaths,
            _ => null
        };

    private void CaptureCurrentSource(JobAction action)
    {
        if (_updatingSourceUi)
        {
            return;
        }

        if (action == JobAction.ExtractArchive)
        {
            _extractSourcePath = SourceTextBox.Text;
            return;
        }

        var collection = action == JobAction.Copy
            ? _copySourcePaths
            : _archiveSourcePaths;
        if (collection.Count == 0 &&
            !SourceTextBox.IsReadOnly &&
            (File.Exists(SourceTextBox.Text) ||
             Directory.Exists(SourceTextBox.Text)))
        {
            collection.Add(SourceTextBox.Text);
        }
    }

    private void AddSelectedSources(IEnumerable<string> paths)
    {
        var collection = ActiveSourceCollection();
        if (collection is null)
        {
            return;
        }

        if (collection.Count == 0 &&
            !SourceTextBox.IsReadOnly &&
            (File.Exists(SourceTextBox.Text) ||
             Directory.Exists(SourceTextBox.Text)))
        {
            collection.Add(SourceTextBox.Text);
        }

        foreach (var path in paths)
        {
            if (!collection.Contains(
                    path,
                    StringComparer.OrdinalIgnoreCase))
            {
                collection.Add(path);
            }
        }

        RefreshSelectedSourceUi();
    }

    private void OnClearSources(object sender, RoutedEventArgs e)
    {
        ActiveSourceCollection()?.Clear();
        RefreshSelectedSourceUi();
    }

    private void RefreshSelectedSourceUi()
    {
        var collection = ActiveSourceCollection();
        if (collection is null || collection.Count == 0)
        {
            SetSourceText(string.Empty, isReadOnly: false);
            ClearSourcesButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SetSourceText(
                collection.Count == 1
                    ? collection[0]
                    : $"{collection.Count:N0} items selected",
                isReadOnly: true);
            ClearSourcesButton.Visibility = Visibility.Visible;
        }

        QueuePreflight();
    }

    private void SetSourceText(string text, bool isReadOnly)
    {
        _updatingSourceUi = true;
        SourceTextBox.IsReadOnly = isReadOnly;
        SourceTextBox.Text = text;
        _updatingSourceUi = false;
    }

    private ArchiveFormat SelectedArchiveFormat() =>
        SevenZipRadioButton.IsChecked == true
            ? ArchiveFormat.SevenZip
            : ArchiveFormat.Zip;

    private CompressionPreset SelectedCompressionPreset() =>
        CompressionComboBox.SelectedIndex switch
        {
            0 => CompressionPreset.Fast,
            2 => CompressionPreset.Smallest,
            _ => CompressionPreset.Balanced
        };

    private bool SelectedVerifyAfterCreate() =>
        VerifyArchiveCheckBox.IsChecked == true;

    private bool HasValidPreflight() =>
        _selectedAction switch
        {
            JobAction.Copy => _copyPreflight?.IsValid == true,
            JobAction.CreateArchive => _createPreflight?.IsValid == true,
            JobAction.ExtractArchive => _extractPreflight?.IsValid == true,
            _ => false
        };

    private void ClearPreflightResults()
    {
        _copyPreflight = null;
        _createPreflight = null;
        _extractPreflight = null;
    }

    private static string BuildPreflightDetails(
        long totalBytes,
        string outputPath,
        long? freeBytes) =>
        $"{FormatBytes(totalBytes)} → {outputPath}" +
        (freeBytes.HasValue
            ? $"{Environment.NewLine}{FormatBytes(freeBytes.Value)} available at destination."
            : string.Empty);

    private void SetActionButtonState(
        System.Windows.Controls.Button button,
        bool selected)
    {
        button.Background = selected
            ? (Brush)FindResource("AccentDarkBrush")
            : (Brush)FindResource("PanelRaisedBrush");
        button.BorderBrush = selected
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("BorderBrush");
        button.Foreground = selected
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextBrush");
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatCount(long count, string singular) =>
        count == 1 ? $"1 {singular}" : $"{count:N0} {singular}s";

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");

    private sealed record DiagnosticLogResult(string? Path, string? Error);
}
