using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.Files;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class LogSessionWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"consolekit-logs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private LogFileOptions Options(int maxSessions = 30, int maxFileSizeMb = 20, int maxTotalSizeMb = 300) => new()
    {
        ProductName = "Tests",
        AppName = "Worker",
        AppVersion = "1.2.3",
        Directory = _directory,
        MaxSessions = maxSessions,
        MaxFileSizeMb = maxFileSizeMb,
        MaxTotalSizeMb = maxTotalSizeMb,
        FinalizeOnProcessExit = false,
    };

    [Fact]
    public async Task Writes_a_running_session_with_a_header_while_the_app_runs()
    {
        using LogHub hub = new();
        LogSessionWriter writer = new(Options());
        hub.AddSink(writer);

        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "worker", "hello") with { ProcessId = 7, Stream = LogStream.StandardError });
        await writer.FlushAsync();

        Assert.StartsWith(LogSessionNames.CurrentPrefix, Path.GetFileName(writer.CurrentFilePath));
        string[] lines = ReadShared(writer.CurrentFilePath);
        Assert.True(LogJsonFormat.TryReadHeader(lines[0], out LogSessionHeader? header));
        Assert.Equal("Worker", header!.App);
        Assert.Contains("\"stream\":\"stderr\"", lines[1]);
        Assert.Equal(LogSessionStatus.Running, Assert.Single(LogSessionCatalog.List(_directory)).Status);
    }

    [Fact]
    public void Finalizing_renames_the_session_writes_a_summary_and_the_index()
    {
        LogHub hub = new();
        LogSessionWriter writer = new(Options());
        hub.AddSink(writer);
        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "worker", "Started: PID 9") with { ProcessId = 9 });
        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Error, "endpoint:unity", "Connection refused"));
        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Debug, "worker", "noise") with { IsMirrored = true });

        hub.Dispose();

        string file = Assert.Single(writer.FinishedFiles);
        Assert.DoesNotContain(LogSessionNames.CurrentPrefix, Path.GetFileName(file));
        Assert.Equal(3, File.ReadAllLines(file).Length); // header + two entries, the mirrored one is skipped

        string summary = File.ReadAllText(writer.SummaryFilePath!);
        Assert.Contains("Connection refused", summary);
        Assert.Contains("endpoint:unity", summary);
        Assert.Contains("line 3", summary);
        Assert.Contains("Started: PID 9", summary);

        LogSessionRecord record = Assert.Single(LogSessionCatalog.List(_directory));
        Assert.Equal(LogSessionStatus.Finished, record.Status);
        Assert.Equal(1, record.Errors);
        Assert.Equal(2, LogSessionCatalog.ReadEntries(file).Count());
    }

    [Fact]
    public void A_session_whose_process_died_is_recovered_as_crashed()
    {
        Directory.CreateDirectory(_directory);
        DateTimeOffset started = DateTimeOffset.Now.AddMinutes(-5);
        string orphan = Path.Combine(_directory, LogSessionNames.PartFileName(LogSessionNames.CurrentBaseName("Desktop", 999_999, started), 1));
        File.WriteAllLines(orphan,
        [
            LogJsonFormat.WriteHeader(new LogSessionHeader("Tests", "Desktop", null, 999_999, started, "pc")),
            LogJsonFormat.WriteEntry(LogEntry.Create(started, LogEntryLevel.Fatal, "desktop", "Unhandled exception")),
        ]);

        using LogSessionWriter writer = new(Options());

        Assert.False(File.Exists(orphan));
        LogSessionRecord crashed = Assert.Single(LogSessionCatalog.List(_directory), record => record.Status == LogSessionStatus.Crashed);
        Assert.Equal("Desktop", crashed.App);
        Assert.Equal(1, crashed.Errors);
        string summary = File.ReadAllText(Path.Combine(_directory, crashed.Summary!));
        Assert.Contains("did not close normally", summary);
        Assert.Contains("Unhandled exception", summary);
    }

    [Fact]
    public void A_large_session_continues_in_part_files()
    {
        LogHub hub = new();
        LogSessionWriter writer = new(Options(maxFileSizeMb: 1));
        hub.AddSink(writer);
        string payload = new('x', 4096);
        for (int index = 0; index < 400; index++)
        {
            hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "worker", payload));
        }

        hub.Dispose();

        Assert.True(writer.FinishedFiles.Count >= 2);
        Assert.EndsWith("_part2.jsonl", writer.FinishedFiles[1]);
        Assert.All(writer.FinishedFiles, file => Assert.True(LogJsonFormat.TryReadHeader(File.ReadLines(file).First(), out _)));
        Assert.Equal(400, writer.FinishedFiles.Sum(file => LogSessionCatalog.ReadEntries(file).Count()));
    }

    [Fact]
    public void Old_sessions_beyond_the_limit_are_deleted_and_the_running_one_is_kept()
    {
        Directory.CreateDirectory(_directory);
        for (int day = 1; day <= 5; day++)
        {
            DateTimeOffset started = DateTimeOffset.Now.AddHours(-day);
            string baseName = LogSessionNames.FinishedBaseName("Worker", started, started.AddMinutes(1));
            File.WriteAllText(Path.Combine(_directory, LogSessionNames.PartFileName(baseName, 1)), "{}\n");
            File.WriteAllText(Path.Combine(_directory, LogSessionNames.SummaryFileName(baseName)), "summary");
        }

        using LogSessionWriter writer = new(Options(maxSessions: 2));

        string[] finished = Directory.GetFiles(_directory, "*.jsonl").Where(file => !LogSessionNames.IsCurrent(file)).ToArray();
        Assert.Equal(2, finished.Length);
        Assert.Equal(2, Directory.GetFiles(_directory, "*.summary.md").Length);
        Assert.True(File.Exists(writer.CurrentFilePath));
    }

    [Fact]
    public void The_session_limit_applies_to_each_application_separately()
    {
        Directory.CreateDirectory(_directory);
        for (int hour = 1; hour <= 3; hour++)
        {
            foreach (string app in new[] { "Worker", "Desktop" })
            {
                DateTimeOffset started = DateTimeOffset.Now.AddHours(-hour).AddMinutes(app == "Desktop" ? 1 : 0);
                string baseName = LogSessionNames.FinishedBaseName(app, started, started.AddMinutes(1));
                File.WriteAllText(Path.Combine(_directory, LogSessionNames.PartFileName(baseName, 1)), "{}\n");
            }
        }

        using LogSessionWriter writer = new(Options(maxSessions: 2));

        string[] kept = Directory.GetFiles(_directory, "*.jsonl").Where(file => !LogSessionNames.IsCurrent(file)).ToArray();
        Assert.Equal(2, kept.Count(file => file.EndsWith("_Worker.jsonl", StringComparison.Ordinal)));
        Assert.Equal(2, kept.Count(file => file.EndsWith("_Desktop.jsonl", StringComparison.Ordinal)));
        Assert.Equal("Worker", LogSessionNames.AppOf(LogSessionNames.FinishedBaseName("Worker", DateTimeOffset.Now, DateTimeOffset.Now)));
        Assert.Equal("Desktop", LogSessionNames.AppOf(LogSessionNames.CrashedBaseName("Desktop", 12, DateTimeOffset.Now)));
    }

    [Fact]
    public async Task The_redactor_masks_entries_before_they_are_written()
    {
        LogFileOptions options = new()
        {
            Directory = _directory,
            AppName = "Worker",
            FinalizeOnProcessExit = false,
            Redactor = entry => entry with
            {
                Message = entry.Message.Replace("secret-token", "***", StringComparison.Ordinal),
                Detail = entry.Detail.Replace("secret-token", "***", StringComparison.Ordinal),
            },
        };
        using LogHub hub = new();
        LogSessionWriter writer = new(options);
        hub.AddSink(writer);

        hub.Write(LogEntry.Create(DateTimeOffset.Now, LogEntryLevel.Information, "worker", "token=secret-token"));
        await writer.FlushAsync();

        string text = string.Join('\n', ReadShared(writer.CurrentFilePath));
        Assert.DoesNotContain("secret-token", text);
        Assert.Contains("token=***", text);
    }

    private static string[] ReadShared(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
