using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

public sealed class ProcessLogSource : LogSource
{
    private const int PendingEntryFlushDelayMilliseconds = 75;

    private readonly object _lock = new();
    private readonly ILogLineStream _lineStream;
    private readonly IReadOnlyList<ILogLineParser> _parsers;
    private Timer? _pendingFlushTimer;
    private LogEntry? _pendingEntry;

    public ProcessLogSource(
        string id,
        string displayName,
        ILogLineStream lineStream,
        IReadOnlyList<ILogLineParser>? parsers = null)
        : base(id, displayName)
    {
        _lineStream = lineStream ?? throw new ArgumentNullException(nameof(lineStream));
        _parsers = parsers ?? LogLineParserRegistry.Parsers;
    }

    protected override void EnableCore()
    {
        _lineStream.LineReceived += OnLineReceived;
    }

    protected override void OnBeforeDisable()
    {
        _lineStream.LineReceived -= OnLineReceived;
        FlushPendingEntry();
    }

    protected override void DestroyCore()
    {
        FlushPendingEntry();
    }

    private void OnLineReceived(object? sender, string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        ParsedLogLine? parsedLine = ParseLine(line);
        if (parsedLine is null)
        {
            return;
        }

        lock (_lock)
        {
            if (parsedLine.IsContinuationCandidate && _pendingEntry is not null)
            {
                _pendingEntry = _pendingEntry with
                {
                    Detail = _pendingEntry.Detail + Environment.NewLine + parsedLine.Detail,
                };
                SchedulePendingFlushCore();
                return;
            }

            FlushPendingEntryCore();
            _pendingEntry = new LogEntry(
                NextEntryId(),
                parsedLine.Timestamp,
                parsedLine.Severity,
                Id,
                parsedLine.Message,
                parsedLine.Detail);
            SchedulePendingFlushCore();
        }
    }

    private ParsedLogLine? ParseLine(string line)
    {
        foreach (ILogLineParser parser in _parsers)
        {
            if (parser.TryParse(line, out ParsedLogLine? parsedLine))
            {
                return parsedLine;
            }
        }

        return null;
    }

    private void SchedulePendingFlushCore()
    {
        _pendingFlushTimer?.Dispose();
        _pendingFlushTimer = new Timer(
            OnPendingFlushTimerElapsed,
            null,
            PendingEntryFlushDelayMilliseconds,
            Timeout.Infinite);
    }

    private void OnPendingFlushTimerElapsed(object? state)
    {
        lock (_lock)
        {
            FlushPendingEntryCore();
        }
    }

    private void FlushPendingEntry()
    {
        lock (_lock)
        {
            FlushPendingEntryCore();
        }
    }

    private void FlushPendingEntryCore()
    {
        _pendingFlushTimer?.Dispose();
        _pendingFlushTimer = null;

        if (_pendingEntry is null)
        {
            return;
        }

        Write(_pendingEntry);
        _pendingEntry = null;
    }
}
