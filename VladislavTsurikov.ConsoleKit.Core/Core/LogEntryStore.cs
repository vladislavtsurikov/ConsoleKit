namespace VladislavTsurikov.ConsoleKit.Core;

public sealed class LogEntryStore : ILogEntryWriter
{
    private readonly object _lock = new();
    private readonly LogEntry?[] _entries;
    private int _startIndex;
    private int _count;

    public LogEntryStore(ConsoleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _entries = new LogEntry[settings.MaxEntryCount];
    }

    public event Action<LogEntry>? EntryAppended;

    public event Action? EntriesCleared;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    public void Write(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_lock)
        {
            int writeIndex = (_startIndex + _count) % _entries.Length;
            if (_count == _entries.Length)
            {
                writeIndex = _startIndex;
                _startIndex = (_startIndex + 1) % _entries.Length;
            }
            else
            {
                _count++;
            }

            _entries[writeIndex] = entry;
        }

        EntryAppended?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock)
        {
            LogEntry[] snapshot = new LogEntry[_count];
            for (int index = 0; index < _count; index++)
            {
                int sourceIndex = (_startIndex + index) % _entries.Length;
                snapshot[index] = _entries[sourceIndex]!;
            }

            return snapshot;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_entries);
            _startIndex = 0;
            _count = 0;
        }

        EntriesCleared?.Invoke();
    }
}
