using Avalonia.Threading;

namespace VladislavTsurikov.ConsoleKit.Avalonia;

public sealed class ConsoleRefreshScheduler : IDisposable
{
    private readonly object _lock = new();
    private readonly TimeSpan _refreshInterval;
    private readonly Action _refreshAction;
    private Timer? _timer;
    private bool _isRefreshPending;
    private bool _disposed;

    public ConsoleRefreshScheduler(int refreshIntervalMilliseconds, Action refreshAction)
    {
        _refreshInterval = TimeSpan.FromMilliseconds(Math.Max(1, refreshIntervalMilliseconds));
        _refreshAction = refreshAction ?? throw new ArgumentNullException(nameof(refreshAction));
    }

    public void RequestRefresh()
    {
        lock (_lock)
        {
            if (_disposed || _isRefreshPending)
            {
                return;
            }

            _isRefreshPending = true;
            _timer = new Timer(
                OnTimerElapsed,
                null,
                _refreshInterval,
                Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnTimerElapsed(object? state)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _isRefreshPending = false;
            _timer?.Dispose();
            _timer = null;
        }

        Dispatcher.UIThread.Post(_refreshAction);
    }
}
