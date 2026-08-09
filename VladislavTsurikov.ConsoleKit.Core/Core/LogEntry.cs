namespace VladislavTsurikov.ConsoleKit.Core;

public sealed record LogEntry(
    long Id,
    DateTimeOffset Timestamp,
    LogSeverity Severity,
    string SourceId,
    string Message,
    string Detail);
