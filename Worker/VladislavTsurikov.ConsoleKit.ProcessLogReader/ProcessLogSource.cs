using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

/// <summary>
/// Journals a process: every stdout and stderr line becomes an entry (multi-line stack traces are
/// joined), and with an <see cref="IProcessOutput"/> its start, restart and exit are recorded too. A
/// failed exit is an Error with the last stderr lines attached.
/// </summary>
public sealed class ProcessLogSource : LogSource
{
    private readonly object _lock = new();
    private readonly ILogLineStream? _lineStream;
    private readonly IProcessOutput? _process;
    private readonly IReadOnlyList<ILogLineParser> _parsers;
    private readonly ProcessLogOptions _options;
    private readonly Queue<string> _recentErrors = new();
    private readonly Timer _pendingFlushTimer;
    private LogEntry? _pendingEntry;
    private bool _hasStarted;
    private bool _hasExited;

    /// <summary>Journals a plain line stream (no stream or lifecycle information).</summary>
    public ProcessLogSource(
        string id,
        string displayName,
        ILogLineStream lineStream,
        IReadOnlyList<ILogLineParser>? parsers = null)
        : base(id, displayName)
    {
        _lineStream = lineStream ?? throw new ArgumentNullException(nameof(lineStream));
        _parsers = parsers ?? LogLineParserRegistry.Parsers;
        _options = new ProcessLogOptions();
        _pendingFlushTimer = new Timer(OnPendingFlushTimerElapsed);
    }

    /// <summary>Journals a process: both streams, its start and its exit.</summary>
    public ProcessLogSource(
        string id,
        string displayName,
        IProcessOutput process,
        ProcessLogOptions? options = null,
        IReadOnlyList<ILogLineParser>? parsers = null)
        : base(id, displayName)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _parsers = parsers ?? LogLineParserRegistry.Parsers;
        _options = options ?? new ProcessLogOptions();
        _pendingFlushTimer = new Timer(OnPendingFlushTimerElapsed);
    }

    protected override void EnableCore()
    {
        if (_lineStream is not null)
        {
            _lineStream.LineReceived += OnLineReceived;
        }

        if (_process is not null)
        {
            _process.OutputReceived += OnOutput;
            _process.ErrorReceived += OnError;
            _process.Started += OnStarted;
            _process.Exited += OnExited;
        }
    }

    protected override void OnBeforeDisable()
    {
        if (_lineStream is not null)
        {
            _lineStream.LineReceived -= OnLineReceived;
        }

        if (_process is not null)
        {
            _process.OutputReceived -= OnOutput;
            _process.ErrorReceived -= OnError;
            _process.Started -= OnStarted;
            _process.Exited -= OnExited;
        }

        FlushPendingEntry();
    }

    protected override void DestroyCore()
    {
        FlushPendingEntry();
        _pendingFlushTimer.Dispose();
    }

    private void OnLineReceived(object? sender, string line) => HandleLine(line, LogStream.None);

    private void OnOutput(string line) => HandleLine(line, LogStream.StandardOutput);

    private void OnError(string line)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _recentErrors.Enqueue(line);
                while (_recentErrors.Count > Math.Max(0, _options.ExitTailLineCount))
                {
                    _recentErrors.Dequeue();
                }
            }
        }

        HandleLine(line, LogStream.StandardError);
    }

    private void OnStarted(ProcessStartedInfo info)
    {
        if (!_options.RecordLifecycle)
        {
            return;
        }

        string verb;
        lock (_lock)
        {
            verb = _hasStarted ? "Restarted" : "Started";
            _hasStarted = true;
            _hasExited = false;
            _recentErrors.Clear();
        }

        string detail = info.WorkingDirectory is null
            ? info.CommandLine
            : $"{info.CommandLine}{Environment.NewLine}Working directory: {info.WorkingDirectory}";
        WriteImmediately(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, Id,
            $"{verb}: PID {info.ProcessId}", detail) with { ProcessId = info.ProcessId });
    }

    private void OnExited(int? exitCode)
    {
        if (!_options.RecordLifecycle)
        {
            return;
        }

        string[] tail;
        lock (_lock)
        {
            if (_hasExited)
            {
                return;
            }

            _hasExited = true;
            tail = _recentErrors.ToArray();
        }

        bool failed = exitCode is not null and not 0;
        string message = exitCode is null ? "Exited" : $"Exited with code {exitCode}";
        string detail = failed && tail.Length > 0
            ? $"{message}{Environment.NewLine}Last stderr lines:{Environment.NewLine}{string.Join(Environment.NewLine, tail)}"
            : message;
        WriteImmediately(LogEntry.Create(DateTimeOffset.Now, failed ? LogEntryLevel.Error : LogEntryLevel.Information,
            Id, message, detail) with { ProcessId = _process?.ProcessId });
    }

    private void HandleLine(string line, LogStream stream)
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

        LogEntryLevel level = parsedLine.Level ?? stream switch
        {
            LogStream.StandardError => _options.StandardErrorLevel,
            LogStream.StandardOutput => _options.StandardOutputLevel,
            _ => LogLevels.FromSeverity(parsedLine.Severity),
        };

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
                LogLevels.ToSeverity(level),
                Id,
                parsedLine.Message,
                parsedLine.Detail)
            {
                Level = level,
                Stream = stream,
                ProcessId = _process?.ProcessId,
                Category = parsedLine.Category,
                Properties = parsedLine.Properties,
            };
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

    /// <summary>Lifecycle entries keep their order: the pending line goes first.</summary>
    private void WriteImmediately(LogEntry entry)
    {
        lock (_lock)
        {
            FlushPendingEntryCore();
            Write(entry);
        }
    }

    private void SchedulePendingFlushCore()
    {
        // One timer per source, moved forward on every line, instead of a new timer per line.
        _pendingFlushTimer.Change(_options.ContinuationDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnPendingFlushTimerElapsed(object? state)
    {
        FlushPendingEntry();
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
        try
        {
            _pendingFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
        }

        if (_pendingEntry is null)
        {
            return;
        }

        Write(_pendingEntry);
        _pendingEntry = null;
    }
}
