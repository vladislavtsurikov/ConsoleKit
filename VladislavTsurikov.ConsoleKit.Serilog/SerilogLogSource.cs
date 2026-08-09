using Serilog.Events;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public sealed class SerilogLogSource : LogSource
{
    private long _nextEntryId;
    private bool _acceptEvents;

    public SerilogLogSource(string id, string displayName)
        : base(id, displayName)
    {
    }

    public void Publish(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (!_acceptEvents || !IsEnabled)
        {
            return;
        }

        long entryId = Interlocked.Increment(ref _nextEntryId);
        string message = logEvent.RenderMessage();
        string detail = CreateDetail(logEvent);
        LogEntry entry = new(
            entryId,
            logEvent.Timestamp,
            SerilogSeverityMapper.Map(logEvent.Level),
            Id,
            message,
            detail);
        Write(entry);
    }

    protected override void EnableCore()
    {
        _acceptEvents = true;
    }

    protected override void OnBeforeDisable()
    {
        _acceptEvents = false;
    }

    private static string CreateDetail(LogEvent logEvent)
    {
        List<string> lines = new()
        {
            $"Level: {logEvent.Level}",
            logEvent.RenderMessage(),
        };

        if (logEvent.Properties.Count > 0)
        {
            lines.Add("Properties:");
            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                lines.Add($"  {property.Key}: {property.Value}");
            }
        }

        if (logEvent.Exception is not null)
        {
            lines.Add("Exception:");
            lines.Add(logEvent.Exception.ToString());
        }

        return string.Join(Environment.NewLine, lines);
    }
}
