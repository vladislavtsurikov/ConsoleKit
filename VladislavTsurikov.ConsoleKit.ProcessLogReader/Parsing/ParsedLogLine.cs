using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

public sealed record ParsedLogLine(
    DateTimeOffset Timestamp,
    LogSeverity Severity,
    string Message,
    string Detail,
    bool IsContinuationCandidate = false);
