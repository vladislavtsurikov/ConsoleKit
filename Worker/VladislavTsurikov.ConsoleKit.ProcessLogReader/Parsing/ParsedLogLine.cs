using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

/// <summary>
/// A parsed process line. <see cref="Level"/> is set only when the line itself said its level
/// (JSON "@l", "[ERR]", "warn:"); otherwise the process source decides from the stream it came from.
/// </summary>
public sealed record ParsedLogLine(
    DateTimeOffset Timestamp,
    LogSeverity Severity,
    string Message,
    string Detail,
    bool IsContinuationCandidate = false)
{
    public LogEntryLevel? Level { get; init; }

    public string? Category { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
