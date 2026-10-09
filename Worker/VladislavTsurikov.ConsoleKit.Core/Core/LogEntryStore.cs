namespace VladislavTsurikov.ConsoleKit.Core;

/// <summary>
/// A bounded, thread-safe ring of entries. The store numbers entries itself, so ids are increasing in
/// the order entries were stored, whatever their sources passed in.
/// </summary>
public sealed class LogEntryStore : ILogEntryWriter
{
    private readonly object _lock = new();
    private readonly LogEntry?[] _entries;
    private int _startIndex;
    private int _count;
    private long _nextId;

    public LogEntryStore(ConsoleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _entries = new LogEntry[settings.MaxEntryCount];
    }

    public event Action<LogEntry>? EntryAppended;

    /// <summary>Raised with the entry that fell out of the ring when a new one was stored.</summary>
    public event Action<LogEntry>? EntryEvicted;

    public event Action? EntriesCleared;

    public int Capacity => _entries.Length;

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
        Append(entry);
    }

    /// <summary>Stores the entry and returns it with the id the store gave it.</summary>
    public LogEntry Append(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        LogEntry stored;
        LogEntry? evicted = null;
        lock (_lock)
        {
            stored = entry with { Id = ++_nextId };
            int writeIndex = (_startIndex + _count) % _entries.Length;
            if (_count == _entries.Length)
            {
                writeIndex = _startIndex;
                evicted = _entries[writeIndex];
                _startIndex = (_startIndex + 1) % _entries.Length;
            }
            else
            {
                _count++;
            }

            _entries[writeIndex] = stored;
        }

        if (evicted is not null)
        {
            EntryEvicted?.Invoke(evicted);
        }

        EntryAppended?.Invoke(stored);
        return stored;
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

    /// <summary>Entries stored after the given id, oldest first. For incremental readers and remote polling.</summary>
    public IReadOnlyList<LogEntry> SnapshotAfter(long afterId)
    {
        lock (_lock)
        {
            List<LogEntry> result = new();
            for (int index = 0; index < _count; index++)
            {
                LogEntry entry = _entries[(_startIndex + index) % _entries.Length]!;
                if (entry.Id > afterId)
                {
                    result.Add(entry);
                }
            }

            return result;
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
