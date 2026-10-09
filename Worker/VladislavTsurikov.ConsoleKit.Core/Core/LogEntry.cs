namespace VladislavTsurikov.ConsoleKit.Core;

/// <summary>
/// One log entry. <see cref="Severity"/> is the console filter group; <see cref="Level"/> is the exact
/// level and defaults to the group when a producer does not set it. The optional process fields let
/// one journal hold the output of many processes.
/// </summary>
public sealed record LogEntry(
    long Id,
    DateTimeOffset Timestamp,
    LogSeverity Severity,
    string SourceId,
    string Message,
    string Detail)
{
    private readonly LogEntryLevel? _level;

    public LogEntryLevel Level
    {
        get => _level ?? LogLevels.FromSeverity(Severity);
        init => _level = value;
    }

    /// <summary>The process that produced the entry, when it came from a process.</summary>
    public int? ProcessId { get; init; }

    public LogStream Stream { get; init; }

    /// <summary>A logger category or similar grouping inside a source.</summary>
    public string? Category { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>
    /// The entry was copied from another process's journal (for example Worker logs shown in Desktop),
    /// so it is already on disk there and file sinks skip it.
    /// </summary>
    public bool IsMirrored { get; init; }

    /// <summary>Creates an entry from an exact level; the severity group follows from it.</summary>
    public static LogEntry Create(
        DateTimeOffset timestamp,
        LogEntryLevel level,
        string sourceId,
        string message,
        string? detail = null)
    {
        return new LogEntry(0, timestamp, LogLevels.ToSeverity(level), sourceId, message, detail ?? message)
        {
            Level = level,
        };
    }
}
