using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class ProcessLogSourceTests
{
    [Fact]
    public void Source_AppendsStackTraceContinuationToPreviousEntry()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLineStream stream = new();
        ProcessLogSource source = new(
            "worker",
            "Worker",
            stream,
            new ILogLineParser[] { new PlainTextLogLineParser() });
        source.Setup(store);
        source.Enable();

        stream.Emit("Failure");
        stream.Emit("   at Example.Run()");
        source.Disable();

        LogEntry entry = Assert.Single(store.Snapshot());
        Assert.Equal("Failure", entry.Message);
        Assert.Contains("Example.Run", entry.Detail);
    }

    [Fact]
    public void Destroy_RemovesLineStreamSubscription()
    {
        LogEntryStore store = new(new ConsoleSettings());
        TestLineStream stream = new();
        ProcessLogSource source = new(
            "worker",
            "Worker",
            stream,
            new ILogLineParser[] { new PlainTextLogLineParser() });
        source.Setup(store);
        source.Enable();
        source.Destroy();

        stream.Emit("ignored");

        Assert.Empty(store.Snapshot());
    }

    private sealed class TestLineStream : ILogLineStream
    {
        public event EventHandler<string>? LineReceived;

        public void Emit(string line)
        {
            LineReceived?.Invoke(this, line);
        }
    }
}
