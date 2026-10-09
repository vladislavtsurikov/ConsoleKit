using Avalonia.Headless.XUnit;
using VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;
using VladislavTsurikov.ConsoleKit.Core;
using Xunit;

namespace VladislavTsurikov.ConsoleKit.Showroom.Tests;

public sealed class ConsoleViewModelTests
{
    private static LogEntry Entry(string message, LogEntryLevel level = LogEntryLevel.Information) =>
        LogEntry.Create(DateTimeOffset.Now, level, "worker", message);

    [AvaloniaFact]
    public void New_entries_are_appended_without_rebuilding_the_list()
    {
        using LogHub hub = new();
        using ConsoleViewModel console = new(hub);
        hub.Write(Entry("one"));
        console.Refresh();
        LogEntryItemViewModel first = Assert.Single(console.Entries);

        hub.Write(Entry("two", LogEntryLevel.Error));
        console.Refresh();

        Assert.Same(first, console.Entries[0]);
        Assert.Equal(["one", "two"], console.Entries.Select(item => item.Message));
        Assert.Equal(1, console.InfoCount);
        Assert.Equal(1, console.ErrorCount);
    }

    [AvaloniaFact]
    public void Collapse_merges_a_repeat_into_the_last_row()
    {
        using LogHub hub = new();
        using ConsoleViewModel console = new(hub) { IsCollapseEnabled = true };
        hub.Write(Entry("retrying"));
        console.Refresh();

        hub.Write(Entry("retrying"));
        hub.Write(Entry("retrying"));
        console.Refresh();

        LogEntryItemViewModel row = Assert.Single(console.Entries);
        Assert.Equal(3, row.RepeatCount);
    }

    [AvaloniaFact]
    public void Evicted_entries_leave_the_list_and_the_counters()
    {
        using LogHub hub = new(new ConsoleSettings { MaxEntryCount = 2 });
        using ConsoleViewModel console = new(hub);
        hub.Write(Entry("a", LogEntryLevel.Warning));
        hub.Write(Entry("b"));
        console.Refresh();

        hub.Write(Entry("c"));
        console.Refresh();

        Assert.Equal(["b", "c"], console.Entries.Select(item => item.Message));
        Assert.Equal(0, console.WarningCount);
        Assert.Equal(2, console.InfoCount);
    }

    [AvaloniaFact]
    public void A_filter_change_rebuilds_from_the_store()
    {
        using LogHub hub = new();
        using ConsoleViewModel console = new(hub);
        hub.Write(Entry("ok"));
        hub.Write(Entry("bad", LogEntryLevel.Error));
        console.Refresh();

        console.IsInfoEnabled = false;

        Assert.Equal(["bad"], console.Entries.Select(item => item.Message));
    }

    [AvaloniaFact]
    public void Detail_shows_origin_and_properties()
    {
        using LogHub hub = new();
        using ConsoleViewModel console = new(hub);
        hub.Write(Entry("socket closed", LogEntryLevel.Error) with
        {
            ProcessId = 77,
            Stream = LogStream.StandardError,
            Properties = new Dictionary<string, string> { ["Port"] = "6400" },
        });
        console.Refresh();

        console.SelectedEntry = console.Entries[0];

        Assert.Contains("Error · worker · PID 77 · stderr", console.SelectedDetail);
        Assert.Contains("Port = 6400", console.SelectedDetail);
    }

    [AvaloniaFact]
    public void Log_file_commands_appear_once_a_journal_is_set()
    {
        using LogHub hub = new();
        using ConsoleViewModel console = new(hub);
        string? copied = null;
        console.CopyRequested += (_, text) => copied = text;

        Assert.False(console.HasLogFiles);
        console.LogFilePath = @"C:\logs\current-Worker-1-2026.jsonl";
        console.CopyLogPathCommand.Execute(null);

        Assert.True(console.HasLogFiles);
        Assert.Equal(@"C:\logs\current-Worker-1-2026.jsonl", copied);
    }
}
