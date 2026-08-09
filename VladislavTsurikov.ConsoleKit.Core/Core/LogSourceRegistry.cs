namespace VladislavTsurikov.ConsoleKit.Core;

public sealed class LogSourceRegistry : IDisposable
{
    private readonly object _lock = new();
    private readonly ILogEntryWriter _writer;
    private readonly List<LogSource> _sources = new();
    private bool _disposed;

    public LogSourceRegistry(ILogEntryWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public event Action? SourcesChanged;

    public IReadOnlyList<LogSource> Sources
    {
        get
        {
            lock (_lock)
            {
                return _sources.ToArray();
            }
        }
    }

    public void Register(LogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_sources.Any(item => string.Equals(
                    item.Id,
                    source.Id,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"A log source with id '{source.Id}' is already registered.");
            }

            source.Setup(_writer);
            _sources.Add(source);
        }

        SourcesChanged?.Invoke();
    }

    public void Unregister(LogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfDisposed();

        bool removed;
        lock (_lock)
        {
            removed = _sources.Remove(source);
        }

        if (!removed)
        {
            return;
        }

        source.Destroy();
        SourcesChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        LogSource[] sources;
        lock (_lock)
        {
            sources = _sources.ToArray();
            _sources.Clear();
            _disposed = true;
        }

        foreach (LogSource source in sources)
        {
            source.Destroy();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
