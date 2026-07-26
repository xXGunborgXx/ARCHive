namespace ARCHive.Copy;

public sealed class CopyPauseController
{
    private readonly object _gate = new();
    private TaskCompletionSource _resumeSignal = CompletedSignal();
    private bool _canPause;
    private bool _pauseRequested;
    private bool _isPaused;

    public bool CanPause
    {
        get
        {
            lock (_gate)
            {
                return _canPause;
            }
        }
    }

    public bool IsPauseRequested
    {
        get
        {
            lock (_gate)
            {
                return _pauseRequested;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
            }
        }
    }

    public bool RequestPause()
    {
        lock (_gate)
        {
            if (!_canPause || _pauseRequested)
            {
                return false;
            }

            _pauseRequested = true;
            _resumeSignal = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }

    public bool Resume()
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            if (!_pauseRequested)
            {
                return false;
            }

            _pauseRequested = false;
            _isPaused = false;
            signal = _resumeSignal;
        }

        signal.TrySetResult();
        return true;
    }

    internal void BeginSession(bool canPause)
    {
        TaskCompletionSource priorSignal;
        lock (_gate)
        {
            priorSignal = _resumeSignal;
            _canPause = canPause;
            _pauseRequested = false;
            _isPaused = false;
            _resumeSignal = CompletedSignal();
        }

        priorSignal.TrySetResult();
    }

    internal async Task WaitForResumeAsync(CancellationToken cancellationToken)
    {
        Task signal;
        lock (_gate)
        {
            if (!_pauseRequested)
            {
                return;
            }

            _isPaused = true;
            signal = _resumeSignal.Task;
        }

        await signal.WaitAsync(cancellationToken);
    }

    internal void EndSession()
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            _canPause = false;
            _pauseRequested = false;
            _isPaused = false;
            signal = _resumeSignal;
            _resumeSignal = CompletedSignal();
        }

        signal.TrySetResult();
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
