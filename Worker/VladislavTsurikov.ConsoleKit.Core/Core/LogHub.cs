namespace VladislavTsurikov.ConsoleKit.Core;

/// <summary>Receives every entry the hub stores, e.g. a file journal or a network forwarder.</summary>
public interface ILogSink : IDisposable
{
    void Emit(LogEntry entry);
}

/// <summary>
/// The single entry point of a journal: sources write into it, it keeps the recent entries in memory
/// for the console and hands each one to its sinks (files, other processes).
/// </summary>
/// <example>
/// <code>
/// using LogHub hub = new(new ConsoleSettings { MaxEntryCount = 5000 });
/// hub.AddSink(new LogSessionWriter(new LogFileOptions { AppName = "Worker" }));
/// hub.Sources.Register(source);
/// source.Enable();
/// </code>
/// </example>
public sealed class LogHub : ILogEntryWriter, IDisposable
{
    private readonly object _sinkLock = new();
    private ILogSink[] _sinks = [];
    private bool _disposed;

    public LogHub(ConsoleSettings? settings = null)
    {
        Settings = settings ?? new ConsoleSettings();
        Store = new LogEntryStore(Settings);
        Sources = new LogSourceRegistry(this);
    }

    public ConsoleSettings Settings { get; }

    public LogEntryStore Store { get; }

    public LogSourceRegistry Sources { get; }

    public IReadOnlyList<ILogSink> Sinks => Volatile.Read(ref _sinks);

    public void AddSink(ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_sinkLock)
        {
            _sinks = [.. _sinks, sink];
        }
    }

    public bool RemoveSink(ILogSink sink)
    {
        lock (_sinkLock)
        {
            if (!_sinks.Contains(sink))
            {
                return false;
            }

            _sinks = _sinks.Where(existing => !ReferenceEquals(existing, sink)).ToArray();
            return true;
        }
    }

    public void Write(LogEntry entry)
    {
        Append(entry);
    }

    /// <summary>Stores the entry, hands it to the sinks and returns it with the id the store gave it.</summary>
    public LogEntry Append(LogEntry entry)
    {
        if (_disposed)
        {
            return entry;
        }

        LogEntry stored = Store.Append(entry);
        foreach (ILogSink sink in Volatile.Read(ref _sinks))
        {
            try
            {
                sink.Emit(stored);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A failing sink must not take the console or the other sinks down with it.
            }
        }

        return stored;
    }

    /// <summary>Destroys the sources first, so their last entries still reach the sinks, then disposes the sinks.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Sources.Dispose();
        _disposed = true;

        ILogSink[] sinks;
        lock (_sinkLock)
        {
            sinks = _sinks;
            _sinks = [];
        }

        foreach (ILogSink sink in sinks)
        {
            sink.Dispose();
        }
    }
}
