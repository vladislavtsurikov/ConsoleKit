namespace VladislavTsurikov.ConsoleKit.Core.Filtering;

public sealed class LogSeverityCounter
{
    public int Count(IReadOnlyList<LogEntry> entries, LogSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Count(entry => entry.Severity == severity);
    }
}
