namespace VladislavTsurikov.ConsoleKit.Core.Filtering;

public sealed class LogEntryFilter
{
    private readonly Dictionary<string, bool> _sourceStates = new(StringComparer.OrdinalIgnoreCase);

    public bool IsInfoEnabled { get; set; } = true;

    public bool IsWarningEnabled { get; set; } = true;

    public bool IsErrorEnabled { get; set; } = true;

    public string SearchText { get; set; } = string.Empty;

    public void SetSourceEnabled(string sourceId, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        _sourceStates[sourceId] = isEnabled;
    }

    public bool Matches(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!IsSeverityEnabled(entry.Severity))
        {
            return false;
        }

        if (_sourceStates.TryGetValue(entry.SourceId, out bool isSourceEnabled) &&
            !isSourceEnabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        string searchText = SearchText.Trim();
        return entry.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.Detail.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
               entry.SourceId.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSeverityEnabled(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Info => IsInfoEnabled,
            LogSeverity.Warning => IsWarningEnabled,
            LogSeverity.Error => IsErrorEnabled,
            _ => false,
        };
    }
}
