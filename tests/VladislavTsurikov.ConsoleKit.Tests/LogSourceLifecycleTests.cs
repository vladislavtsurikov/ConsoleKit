using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class LogSourceLifecycleTests
{
    [Fact]
    public void EnableAndDisable_AreIdempotent_AndDoNotRequireBaseCalls()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLogSource source = new("test", "Test");
        source.Setup(store);

        source.Enable();
        source.Enable();
        source.Disable();
        source.Disable();
        source.Enable();
        source.Emit("kept");

        Assert.Equal(2, source.EnableCount);
        Assert.Equal(1, source.DisableCount);
        Assert.Single(store.Snapshot());
    }

    [Fact]
    public void DisableThenEnable_PreservesPreviouslyStoredEntries()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLogSource source = new("test", "Test");
        source.Setup(store);
        source.Enable();
        source.Emit("before-disable");

        source.Disable();
        source.Enable();
        source.Emit("after-enable");

        IReadOnlyList<LogEntry> entries = store.Snapshot();
        Assert.Equal(2, entries.Count);
        Assert.Equal("before-disable", entries[0].Message);
        Assert.Equal("after-enable", entries[1].Message);
    }

    [Fact]
    public void EntryIds_AreMonotonicAcrossDifferentSources()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLogSource firstSource = new("first", "First");
        TestLogSource secondSource = new("second", "Second");
        firstSource.Setup(store);
        secondSource.Setup(store);
        firstSource.Enable();
        secondSource.Enable();

        firstSource.Emit("first-entry");
        secondSource.Emit("second-entry");

        IReadOnlyList<LogEntry> entries = store.Snapshot();
        Assert.Equal(2, entries.Count);
        Assert.True(entries[1].Id > entries[0].Id);
    }

    private sealed class TestLogSource : LogSource
    {
        public TestLogSource(string id, string displayName)
            : base(id, displayName)
        {
        }

        public int EnableCount { get; private set; }

        public int DisableCount { get; private set; }

        public void Emit(string message)
        {
            Write(new LogEntry(
                NextEntryId(),
                DateTimeOffset.UtcNow,
                LogSeverity.Info,
                Id,
                message,
                message));
        }

        protected override void EnableCore()
        {
            EnableCount++;
        }

        protected override void DisableCore()
        {
            DisableCount++;
        }
    }
}
