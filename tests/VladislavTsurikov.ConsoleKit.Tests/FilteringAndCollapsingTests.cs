using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.Core.Collapsing;
using VladislavTsurikov.ConsoleKit.Core.Filtering;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class FilteringAndCollapsingTests
{
    [Fact]
    public void Filter_CombinesSeveritySourceAndSearchText()
    {
        LogEntryFilter filter = new()
        {
            IsInfoEnabled = false,
            SearchText = "needle",
        };
        filter.SetSourceEnabled("worker", true);
        filter.SetSourceEnabled("cloudflared", false);

        Assert.True(filter.Matches(CreateEntry(LogSeverity.Error, "worker", "needle found")));
        Assert.False(filter.Matches(CreateEntry(LogSeverity.Info, "worker", "needle found")));
        Assert.False(filter.Matches(CreateEntry(LogSeverity.Error, "cloudflared", "needle found")));
        Assert.False(filter.Matches(CreateEntry(LogSeverity.Error, "worker", "other")));
    }

    [Fact]
    public void Collapser_GroupsOnlyConsecutiveEquivalentEntries()
    {
        LogEntry first = CreateEntry(LogSeverity.Warning, "worker", "retry");
        LogEntry second = first with { Id = 2 };
        LogEntry third = CreateEntry(LogSeverity.Info, "worker", "ready") with { Id = 3 };
        LogEntryCollapser collapser = new();

        IReadOnlyList<CollapsedLogEntry> result = collapser.Collapse(new[] { first, second, third });

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].RepeatCount);
        Assert.Equal(1, result[1].RepeatCount);
    }

    private static LogEntry CreateEntry(LogSeverity severity, string sourceId, string message)
    {
        return new LogEntry(1, DateTimeOffset.UtcNow, severity, sourceId, message, message);
    }
}
