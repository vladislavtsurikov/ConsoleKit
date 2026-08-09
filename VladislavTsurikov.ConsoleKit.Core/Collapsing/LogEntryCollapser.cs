namespace VladislavTsurikov.ConsoleKit.Core.Collapsing;

public sealed class LogEntryCollapser
{
    public IReadOnlyList<CollapsedLogEntry> Collapse(IReadOnlyList<LogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return Array.Empty<CollapsedLogEntry>();
        }

        List<CollapsedLogEntry> collapsedEntries = new();
        LogEntry currentEntry = entries[0];
        int repeatCount = 1;

        for (int index = 1; index < entries.Count; index++)
        {
            LogEntry candidate = entries[index];
            if (CanCollapse(currentEntry, candidate))
            {
                repeatCount++;
                continue;
            }

            collapsedEntries.Add(new CollapsedLogEntry(currentEntry, repeatCount));
            currentEntry = candidate;
            repeatCount = 1;
        }

        collapsedEntries.Add(new CollapsedLogEntry(currentEntry, repeatCount));
        return collapsedEntries;
    }

    private static bool CanCollapse(LogEntry first, LogEntry second)
    {
        return first.Severity == second.Severity &&
               string.Equals(first.SourceId, second.SourceId, StringComparison.Ordinal) &&
               string.Equals(first.Message, second.Message, StringComparison.Ordinal);
    }
}
