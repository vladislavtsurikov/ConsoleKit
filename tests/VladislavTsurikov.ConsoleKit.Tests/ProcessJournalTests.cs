using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class ProcessJournalTests
{
    private static readonly ILogLineParser[] s_parsers = [new CompactJsonLogLineParser(), new PlainTextLogLineParser()];

    [Fact]
    public void Store_numbers_entries_in_storage_order()
    {
        LogEntryStore store = new(new ConsoleSettings());
        LogEntry first = store.Append(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "a", "one") with { Id = 99 });
        LogEntry second = store.Append(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "a", "two") with { Id = 5 });

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
        Assert.Equal(["two"], store.SnapshotAfter(1).Select(entry => entry.Message));
    }

    [Fact]
    public void Hub_hands_stored_entries_to_sinks_and_keeps_going_when_one_fails()
    {
        using LogHub hub = new();
        RecordingSink failing = new(fail: true);
        RecordingSink recording = new();
        hub.AddSink(failing);
        hub.AddSink(recording);

        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Warning, "app", "careful"));

        LogEntry entry = Assert.Single(recording.Entries);
        Assert.Equal(1, entry.Id);
        Assert.Equal(LogSeverity.Warning, entry.Severity);
        Assert.Equal(LogEntryLevel.Warning, entry.Level);
    }

    [Fact]
    public void Level_defaults_to_the_severity_group()
    {
        LogEntry entry = new(1, DateTimeOffset.Now, LogSeverity.Error, "a", "m", "d");

        Assert.Equal(LogEntryLevel.Error, entry.Level);
        Assert.Equal(LogSeverity.Info, LogLevels.ToSeverity(LogEntryLevel.Debug));
        Assert.Equal(LogSeverity.Error, LogLevels.ToSeverity(LogEntryLevel.Fatal));
    }

    [Theory]
    [InlineData("[12:00:01 ERR] boom", LogEntryLevel.Error)]
    [InlineData("[WRN] careful", LogEntryLevel.Warning)]
    [InlineData("warn: Microsoft.Hosting[0]", LogEntryLevel.Warning)]
    [InlineData("fail: Something[1]", LogEntryLevel.Error)]
    [InlineData("DEBUG starting", LogEntryLevel.Debug)]
    public void Plain_text_recognizes_a_leading_level(string line, LogEntryLevel expected)
    {
        Assert.True(new PlainTextLogLineParser().TryParse(line, out ParsedLogLine? parsed));
        Assert.Equal(expected, parsed!.Level);
    }

    [Fact]
    public void Plain_text_without_a_level_leaves_it_to_the_stream()
    {
        new PlainTextLogLineParser().TryParse("listening on port 6400", out ParsedLogLine? parsed);
        Assert.Null(parsed!.Level);
    }

    [Fact]
    public void Process_source_separates_streams_and_does_not_warn_on_plain_stderr()
    {
        LogEntryStore store = new(new ConsoleSettings());
        FakeProcess process = new();
        ProcessLogSource source = new("endpoint:unity", "Unity", process, new ProcessLogOptions { RecordLifecycle = false }, s_parsers);
        source.Setup(store);
        source.Enable();

        process.Out("ready");
        process.Err("handshake ok");
        process.Err("[ERR] socket closed");
        source.Disable();

        LogEntry[] entries = store.Snapshot().ToArray();
        Assert.Equal(3, entries.Length);
        Assert.Equal(LogStream.StandardOutput, entries[0].Stream);
        Assert.Equal(LogStream.StandardError, entries[1].Stream);
        Assert.Equal(LogEntryLevel.Information, entries[1].Level);
        Assert.Equal(LogEntryLevel.Error, entries[2].Level);
        Assert.All(entries, entry => Assert.Equal(4242, entry.ProcessId));
    }

    [Fact]
    public void Process_source_keeps_the_level_of_serilog_json_on_stderr()
    {
        LogEntryStore store = new(new ConsoleSettings());
        FakeProcess process = new();
        ProcessLogSource source = new("worker", "Worker", process, new ProcessLogOptions { RecordLifecycle = false }, s_parsers);
        source.Setup(store);
        source.Enable();

        process.Err("{\"@t\":\"2026-10-09T12:00:00Z\",\"@mt\":\"Disk full\",\"@l\":\"Fatal\",\"SourceContext\":\"Storage\",\"Drive\":\"C\"}");
        source.Disable();

        LogEntry entry = Assert.Single(store.Snapshot());
        Assert.Equal(LogEntryLevel.Fatal, entry.Level);
        Assert.Equal(LogSeverity.Error, entry.Severity);
        Assert.Equal("Storage", entry.Category);
        Assert.Equal("C", entry.Properties!["Drive"]);
    }

    [Fact]
    public void Process_source_records_start_restart_and_a_failed_exit_with_the_last_stderr_lines()
    {
        LogEntryStore store = new(new ConsoleSettings());
        FakeProcess process = new();
        ProcessLogSource source = new("endpoint:files", "Files", process, parsers: s_parsers);
        source.Setup(store);
        source.Enable();

        process.Start();
        process.Err("cannot open config");
        process.Exit(1);
        process.Start();
        process.Exit(0);
        source.Disable();

        string[] messages = store.Snapshot().Select(entry => entry.Message).ToArray();
        Assert.Equal(["Started: PID 4242", "cannot open config", "Exited with code 1", "Restarted: PID 4242", "Exited with code 0"], messages);
        LogEntry failed = store.Snapshot()[2];
        Assert.Equal(LogEntryLevel.Error, failed.Level);
        Assert.Contains("cannot open config", failed.Detail);
        Assert.Equal(LogEntryLevel.Information, store.Snapshot()[4].Level);
    }

    [Fact]
    public async Task File_tail_reads_appended_lines_and_restarts_after_truncation()
    {
        string path = Path.Combine(Path.GetTempPath(), $"consolekit-tail-{Guid.NewGuid():N}.log");
        try
        {
            await File.WriteAllTextAsync(path, "old\n");
            using FileTailLineStream tail = new(path, fromEnd: true, pollInterval: TimeSpan.FromHours(1));
            List<string> lines = new();
            tail.LineReceived += (_, line) => lines.Add(line);

            await File.AppendAllTextAsync(path, "first\r\nsecond\npart");
            tail.Poll();
            Assert.Equal(["first", "second"], lines);

            await File.AppendAllTextAsync(path, "ial\n");
            tail.Poll();
            Assert.Equal("partial", lines[^1]);

            await File.WriteAllTextAsync(path, "new\n");
            tail.Poll();
            Assert.Equal("new", lines[^1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class RecordingSink(bool fail = false) : ILogSink
    {
        public List<LogEntry> Entries { get; } = new();

        public void Emit(LogEntry entry)
        {
            if (fail)
            {
                throw new InvalidOperationException("sink failed");
            }

            Entries.Add(entry);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeProcess : IProcessOutput
    {
        public string DisplayName => "fake";

        public int? ProcessId => 4242;

        public event Action<string>? OutputReceived;

        public event Action<string>? ErrorReceived;

        public event Action<ProcessStartedInfo>? Started;

        public event Action<int?>? Exited;

        public void Out(string line) => OutputReceived?.Invoke(line);

        public void Err(string line) => ErrorReceived?.Invoke(line);

        public void Start() => Started?.Invoke(new ProcessStartedInfo(4242, "fake --run"));

        public void Exit(int code) => Exited?.Invoke(code);
    }
}
