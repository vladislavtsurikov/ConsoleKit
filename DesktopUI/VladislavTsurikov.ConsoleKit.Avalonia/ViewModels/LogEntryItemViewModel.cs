using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.Core.Collapsing;

namespace VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;

public sealed class LogEntryItemViewModel
{
    public LogEntryItemViewModel(CollapsedLogEntry collapsedEntry)
    {
        ArgumentNullException.ThrowIfNull(collapsedEntry);
        Entry = collapsedEntry.Entry;
        RepeatCount = collapsedEntry.RepeatCount;
    }

    public LogEntry Entry { get; }

    public int RepeatCount { get; }

    public string TimestampText => Entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

    public string SourceId => Entry.SourceId;

    public string Message => Entry.Message;

    public string Detail => Entry.Detail;

    public string RepeatText => RepeatCount > 1 ? $"×{RepeatCount}" : string.Empty;

    public bool HasRepeats => RepeatCount > 1;

    public bool IsInfo => Entry.Severity == LogSeverity.Info;

    public bool IsWarning => Entry.Severity == LogSeverity.Warning;

    public bool IsError => Entry.Severity == LogSeverity.Error;
}
