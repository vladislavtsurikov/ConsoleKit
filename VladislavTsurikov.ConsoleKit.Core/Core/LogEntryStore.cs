namespace VladislavTsurikov.ConsoleKit.Core;

public sealed class LogEntryStore : ILogEntryWriter
{
    private readonly object _lock = new();
    private readonly Queue<LogEntry> _entries;
    private readonly int _maxEntryCount;

    public LogEntryStore(ConsoleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _maxEntryCount = settings.MaxEntryCount;
        _entries = new Queue<LogEntry>(_maxEntryCount);
    }

    public event Action<LogEntry>? EntryAppended;

    public event Action? EntriesCleared;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            while (_entries.Count >= _maxEntryCount)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntryAppended?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        EntriesCleared?.Invoke();
    }
}
