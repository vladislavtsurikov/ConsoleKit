namespace VladislavTsurikov.ConsoleKit.Core.Collapsing;

public sealed record CollapsedLogEntry(LogEntry Entry, int RepeatCount);
