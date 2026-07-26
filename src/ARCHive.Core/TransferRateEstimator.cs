namespace ARCHive.Core;

public sealed record TransferRateSnapshot(
    double? BytesPerSecond,
    bool IsWaitingForProgress);

public sealed class TransferRateEstimator
{
    private static readonly TimeSpan MinimumSample = TimeSpan.FromMilliseconds(200);
    private long? _previousBytes;
    private TimeSpan _previousElapsed;
    private double? _smoothedBytesPerSecond;

    public void Reset()
    {
        _previousBytes = null;
        _previousElapsed = TimeSpan.Zero;
        _smoothedBytesPerSecond = null;
    }

    public TransferRateSnapshot Update(
        long completedBytes,
        TimeSpan elapsed)
    {
        if (_previousBytes.HasValue && completedBytes < _previousBytes.Value)
        {
            Reset();
        }

        if (!_previousBytes.HasValue)
        {
            _previousBytes = completedBytes;
            _previousElapsed = elapsed;
            return new TransferRateSnapshot(null, false);
        }

        var elapsedSinceSample = elapsed - _previousElapsed;
        var bytesSinceSample = completedBytes - _previousBytes.Value;
        if (elapsedSinceSample < MinimumSample || bytesSinceSample <= 0)
        {
            if (bytesSinceSample == 0 &&
                elapsedSinceSample >= TimeSpan.FromSeconds(2))
            {
                return new TransferRateSnapshot(null, true);
            }

            return CreateSnapshot();
        }

        var currentRate = bytesSinceSample / elapsedSinceSample.TotalSeconds;
        _smoothedBytesPerSecond = _smoothedBytesPerSecond.HasValue
            ? (_smoothedBytesPerSecond.Value * 0.7) + (currentRate * 0.3)
            : currentRate;
        _previousBytes = completedBytes;
        _previousElapsed = elapsed;

        return CreateSnapshot();
    }

    private TransferRateSnapshot CreateSnapshot()
    {
        if (!_smoothedBytesPerSecond.HasValue ||
            _smoothedBytesPerSecond.Value <= 0)
        {
            return new TransferRateSnapshot(null, false);
        }

        return new TransferRateSnapshot(
            _smoothedBytesPerSecond,
            false);
    }
}
