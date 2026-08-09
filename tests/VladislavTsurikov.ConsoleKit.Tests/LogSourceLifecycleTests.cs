using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class LogSourceLifecycleTests
{
    [Fact]
    public void EnableAndDisable_AreIdempotent_AndDoNotRequireBaseCalls()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLogSource source = new();
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

    private sealed class TestLogSource : LogSource
    {
        private long _nextId;

        public TestLogSource()
            : base("test", "Test")
        {
        }

        public int EnableCount { get; private set; }

        public int DisableCount { get; private set; }

        public void Emit(string message)
        {
            Write(new LogEntry(
                Interlocked.Increment(ref _nextId),
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
