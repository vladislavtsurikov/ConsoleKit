using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class LogEntryStoreTests
{
    [Fact]
    public void Write_WhenCapacityIsExceeded_EvictsOldestEntries()
    {
        ConsoleSettings settings = new() { MaxEntryCount = 3 };
        LogEntryStore store = new(settings);

        for (int index = 1; index <= 5; index++)
        {
            store.Write(CreateEntry(index));
        }

        IReadOnlyList<LogEntry> entries = store.Snapshot();
        Assert.Equal(3, entries.Count);
        Assert.Equal(new long[] { 3, 4, 5 }, entries.Select(entry => entry.Id));
    }

    [Fact]
    public void Write_FromMultipleThreads_PreservesConfiguredCapacity()
    {
        ConsoleSettings settings = new() { MaxEntryCount = 5000 };
        LogEntryStore store = new(settings);

        Parallel.For(0, 5000, index => store.Write(CreateEntry(index)));

        Assert.Equal(5000, store.Count);
        Assert.Equal(5000, store.Snapshot().Select(entry => entry.Id).Distinct().Count());
    }

    private static LogEntry CreateEntry(long id)
    {
        return new LogEntry(
            id,
            DateTimeOffset.UtcNow,
            LogSeverity.Info,
            "test",
            $"message-{id}",
            $"detail-{id}");
    }
}
