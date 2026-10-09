using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Files;

public enum LogSessionStatus
{
    Running,
    Finished,
    Crashed,
}

/// <summary>One session as listed in index.json.</summary>
public sealed record LogSessionRecord
{
    public required string Id { get; init; }

    public required string App { get; init; }

    public int ProcessId { get; init; }

    public DateTimeOffset Started { get; init; }

    public DateTimeOffset? Ended { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogSessionStatus Status { get; init; }

    public IReadOnlyList<string> Files { get; init; } = [];

    public string? Summary { get; init; }

    public long Entries { get; init; }

    public int Errors { get; init; }

    public int Warnings { get; init; }
}

/// <summary>
/// The log folder as a list of sessions, and the maintenance shared by every application writing into
/// it: the index, recovery of sessions that never closed, and deletion of old sessions. Several
/// processes (Desktop, Worker) use one folder, so changes are serialized with a per-folder mutex.
/// </summary>
public static class LogSessionCatalog
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Sessions in the folder, newest first. Running sessions come from their files; one whose process
    /// is gone is listed as crashed until the next writer recovers it.
    /// </summary>
    public static IReadOnlyList<LogSessionRecord> List(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        List<LogSessionRecord> sessions = ReadIndex(directory)
            .Where(record => record.Files.Any(file => File.Exists(Path.Combine(directory, file))))
            .ToList();

        foreach (IGrouping<string, string> running in Directory
                     .EnumerateFiles(directory, LogSessionNames.CurrentPrefix + "*" + LogSessionNames.Extension)
                     .GroupBy(LogSessionNames.BaseNameOf))
        {
            if (sessions.Any(session => session.Id == running.Key))
            {
                continue;
            }

            string firstFile = running.OrderBy(file => file, StringComparer.Ordinal).First();
            LogSessionNames.TryParseCurrent(firstFile, out string app, out int processId);
            ReadHeader(firstFile, out LogSessionHeader? header);
            sessions.Add(new LogSessionRecord
            {
                Id = running.Key,
                App = header?.App ?? app,
                ProcessId = processId,
                Started = header?.Started ?? File.GetCreationTime(firstFile),
                // A running file of a process that is gone is a crash waiting to be recovered.
                Status = IsAlive(processId) ? LogSessionStatus.Running : LogSessionStatus.Crashed,
                Files = running.Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToArray(),
            });
        }

        return sessions.OrderByDescending(session => session.Started).ToArray();
    }

    /// <summary>Reads the entries of a session file (the header line is skipped).</summary>
    public static IEnumerable<LogEntry> ReadEntries(string file)
    {
        using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using StreamReader reader = new(stream, Encoding.UTF8);
        long id = 0;
        while (reader.ReadLine() is { } line)
        {
            if (LogJsonFormat.TryReadEntry(line, out LogEntry? entry))
            {
                yield return entry! with { Id = ++id };
            }
        }
    }

    internal static bool ReadHeader(string file, out LogSessionHeader? header)
    {
        header = null;
        try
        {
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream, Encoding.UTF8);
            return reader.ReadLine() is { } first && LogJsonFormat.TryReadHeader(first, out header);
        }
        catch (IOException)
        {
            return false;
        }
    }

    internal static IReadOnlyList<LogSessionRecord> ReadIndex(string directory)
    {
        string path = Path.Combine(directory, LogSessionNames.IndexFileName);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<LogSessionRecord>>(File.ReadAllText(path), s_jsonOptions) ?? []
                : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return [];
        }
    }

    internal static void WriteIndex(string directory, IEnumerable<LogSessionRecord> sessions)
    {
        string path = Path.Combine(directory, LogSessionNames.IndexFileName);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(sessions.OrderByDescending(s => s.Started).ToList(), s_jsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Adds or replaces a session in the index.</summary>
    internal static void Upsert(string directory, LogSessionRecord record)
    {
        List<LogSessionRecord> sessions = ReadIndex(directory).Where(existing => existing.Id != record.Id).ToList();
        sessions.Add(record);
        WriteIndex(directory, sessions);
    }

    /// <summary>Runs <paramref name="action"/> while no other process maintains the same folder.</summary>
    internal static void WithFolderLock(string directory, Action action)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToLowerInvariant())))[..16];
        using Mutex mutex = new(false, $"Local\\ConsoleKit.Logs.{key}");
        bool owned = false;
        try
        {
            try
            {
                owned = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                owned = true;
            }

            action();
        }
        finally
        {
            if (owned)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    /// <summary>
    /// Turns the files of sessions whose process is gone into crashed sessions with a summary, so a
    /// crash still leaves a readable journal.
    /// </summary>
    internal static void RecoverCrashed(string directory, int ownProcessId)
    {
        foreach (IGrouping<string, string> running in Directory
                     .EnumerateFiles(directory, LogSessionNames.CurrentPrefix + "*" + LogSessionNames.Extension)
                     .GroupBy(LogSessionNames.BaseNameOf)
                     .ToArray())
        {
            string[] parts = running.OrderBy(PartNumber).ToArray();
            if (!LogSessionNames.TryParseCurrent(parts[0], out string app, out int processId) ||
                processId == ownProcessId || IsAlive(processId))
            {
                continue;
            }

            ReadHeader(parts[0], out LogSessionHeader? header);
            header ??= new LogSessionHeader(string.Empty, app, null, processId, File.GetCreationTime(parts[0]), Environment.MachineName);
            string baseName = LogSessionNames.CrashedBaseName(header.App, processId, header.Started);

            LogSessionStatistics statistics = new();
            List<string> files = new();
            for (int index = 0; index < parts.Length; index++)
            {
                string target = LogSessionNames.PartFileName(baseName, index + 1);
                File.Move(parts[index], Path.Combine(directory, target), overwrite: true);
                files.Add(target);
                long line = 0;
                foreach (string text in File.ReadLines(Path.Combine(directory, target)))
                {
                    line++;
                    if (LogJsonFormat.TryReadEntry(text, out LogEntry? entry))
                    {
                        statistics.Add(entry!, target, line);
                    }
                }
            }

            string summary = LogSessionNames.SummaryFileName(baseName);
            File.WriteAllText(Path.Combine(directory, summary), statistics.RenderSummary(header, null, crashed: true, files));
            Upsert(directory, Record(baseName, header, null, LogSessionStatus.Crashed, files, summary, statistics));
        }
    }

    /// <summary>
    /// Deletes finished sessions beyond the limits, oldest first: <see cref="LogFileOptions.MaxSessions"/>
    /// per application (Desktop runs must not push Worker sessions out), the age limit, and the size
    /// limit of the whole folder. Running sessions are never touched.
    /// </summary>
    internal static void ApplyRetention(string directory, LogFileOptions options)
    {
        List<SessionFiles> sessions = Directory
            .EnumerateFiles(directory)
            .Where(file => !LogSessionNames.IsCurrent(file) &&
                           (file.EndsWith(LogSessionNames.Extension, StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(LogSessionNames.SummarySuffix, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(LogSessionNames.BaseNameOf)
            .Select(group => new SessionFiles(
                group.Key,
                LogSessionNames.TryParseStart(group.Key, out DateTime started) ? started : group.Min(File.GetLastWriteTime),
                group.Max(File.GetLastWriteTime),
                group.ToArray(),
                group.Sum(file => new FileInfo(file).Length)))
            .OrderByDescending(session => session.Started)
            .ToList();

        long totalBudget = (long)options.MaxTotalSizeMb * 1024 * 1024;
        long runningSize = Directory.EnumerateFiles(directory, LogSessionNames.CurrentPrefix + "*")
            .Sum(file => new FileInfo(file).Length);
        DateTime cutoff = DateTime.Now.AddDays(-Math.Max(0, options.MaxAgeDays));

        long kept = runningSize;
        List<SessionFiles> remove = new();
        Dictionary<string, int> perApp = new(StringComparer.OrdinalIgnoreCase);
        foreach (SessionFiles session in sessions)
        {
            string app = LogSessionNames.AppOf(session.BaseName);
            int index = perApp.GetValueOrDefault(app);
            perApp[app] = index + 1;
            bool tooMany = index >= Math.Max(0, options.MaxSessions);
            bool tooOld = options.MaxAgeDays > 0 && session.LastWrite < cutoff;
            bool tooBig = kept + session.Size > totalBudget;
            if (tooMany || tooOld || tooBig)
            {
                remove.Add(session);
            }
            else
            {
                kept += session.Size;
            }
        }

        foreach (SessionFiles session in remove)
        {
            foreach (string file in session.Files)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        if (remove.Count > 0)
        {
            HashSet<string> removed = remove.Select(session => session.BaseName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            WriteIndex(directory, ReadIndex(directory).Where(record => !removed.Contains(record.Id)));
        }
    }

    internal static LogSessionRecord Record(
        string baseName,
        LogSessionHeader header,
        DateTimeOffset? ended,
        LogSessionStatus status,
        IReadOnlyList<string> files,
        string? summary,
        LogSessionStatistics statistics)
    {
        return new LogSessionRecord
        {
            Id = baseName,
            App = header.App,
            ProcessId = header.ProcessId,
            Started = header.Started,
            Ended = ended,
            Status = status,
            Files = files,
            Summary = summary,
            Entries = statistics.EntryCount,
            Errors = statistics.Count(LogEntryLevel.Error) + statistics.Count(LogEntryLevel.Fatal),
            Warnings = statistics.Count(LogEntryLevel.Warning),
        };
    }

    private static int PartNumber(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        int index = name.LastIndexOf("_part", StringComparison.Ordinal);
        return index >= 0 && int.TryParse(name[(index + 5)..], out int part) ? part : 1;
    }

    private static bool IsAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed record SessionFiles(string BaseName, DateTime Started, DateTime LastWrite, string[] Files, long Size);
}
