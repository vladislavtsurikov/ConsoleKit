using System.Globalization;
using System.Text;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Files;

/// <summary>
/// What happened in a session, collected while it is written (or read back after a crash): counts per
/// level and source, process starts and exits, and the first errors and warnings with their line in
/// the session file. Renders the summary an LLM reads before the full JSONL.
/// </summary>
public sealed class LogSessionStatistics
{
    public const int NotableLimit = 50;

    private readonly Dictionary<LogEntryLevel, int> _levels = new();
    private readonly SortedDictionary<string, SourceCounts> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _processEvents = new();
    private readonly List<Notable> _notable = new();

    public long EntryCount { get; private set; }

    public IReadOnlyDictionary<LogEntryLevel, int> Levels => _levels;

    public int Count(LogEntryLevel level) => _levels.GetValueOrDefault(level);

    /// <param name="entry">The entry as written.</param>
    /// <param name="file">The session file it went to.</param>
    /// <param name="line">Its 1-based line number in that file.</param>
    public void Add(LogEntry entry, string file, long line)
    {
        EntryCount++;
        _levels[entry.Level] = _levels.GetValueOrDefault(entry.Level) + 1;

        if (!_sources.TryGetValue(entry.SourceId, out SourceCounts? counts))
        {
            counts = new SourceCounts();
            _sources[entry.SourceId] = counts;
        }

        counts.Total++;
        if (entry.Level >= LogEntryLevel.Error)
        {
            counts.Errors++;
        }
        else if (entry.Level == LogEntryLevel.Warning)
        {
            counts.Warnings++;
        }

        if (entry.ProcessId is not null &&
            (entry.Message.StartsWith("Started:", StringComparison.Ordinal) ||
             entry.Message.StartsWith("Restarted:", StringComparison.Ordinal) ||
             entry.Message.StartsWith("Exited", StringComparison.Ordinal)))
        {
            _processEvents.Add($"{Time(entry)} `{entry.SourceId}` {entry.Message}");
        }

        if (entry.Level >= LogEntryLevel.Warning && _notable.Count < NotableLimit)
        {
            _notable.Add(new Notable(entry, Path.GetFileName(file), line));
        }
    }

    public string RenderSummary(LogSessionHeader header, DateTimeOffset? ended, bool crashed, IReadOnlyList<string> files)
    {
        StringBuilder text = new();
        text.AppendLine(CultureInfo.InvariantCulture, $"# Log session: {header.App}");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- Product: {header.Product}{(header.Version is null ? string.Empty : $" {header.Version}")}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Process: {header.ProcessId} on {header.Machine}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Started: {header.Started.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine(crashed
            ? "- Ended: **did not close normally** (crash, kill or power loss); the last entries may be missing."
            : $"- Ended: {ended?.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Entries: {EntryCount}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Files: {string.Join(", ", files.Select(file => $"`{Path.GetFileName(file)}`"))}");
        text.AppendLine();

        text.AppendLine("## Levels");
        text.AppendLine();
        text.AppendLine("| Level | Count |");
        text.AppendLine("|---|---|");
        foreach (LogEntryLevel level in Enum.GetValues<LogEntryLevel>().Reverse())
        {
            if (_levels.TryGetValue(level, out int count))
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"| {level} | {count} |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Sources");
        text.AppendLine();
        text.AppendLine("| Source | Entries | Errors | Warnings |");
        text.AppendLine("|---|---|---|---|");
        foreach ((string source, SourceCounts counts) in _sources)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"| `{source}` | {counts.Total} | {counts.Errors} | {counts.Warnings} |");
        }

        if (_processEvents.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("## Processes");
            text.AppendLine();
            foreach (string processEvent in _processEvents)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {processEvent}");
            }
        }

        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"## Errors and warnings (first {NotableLimit})");
        text.AppendLine();
        if (_notable.Count == 0)
        {
            text.AppendLine("None.");
        }

        foreach (Notable notable in _notable)
        {
            string message = notable.Entry.Message.ReplaceLineEndings(" ");
            if (message.Length > 300)
            {
                message = message[..300] + "…";
            }

            text.AppendLine(CultureInfo.InvariantCulture,
                $"- {Time(notable.Entry)} **{notable.Entry.Level}** `{notable.Entry.SourceId}`: {message} (`{notable.File}` line {notable.Line})");
        }

        text.AppendLine();
        text.AppendLine("## Reading the full log");
        text.AppendLine();
        text.AppendLine("Each file is JSON Lines: the first line is the session header, then one entry per line with");
        text.AppendLine("`t` (time), `l` (level), `src` (source), `pid`, `stream` (stdout/stderr), `cat`, `msg`, `detail`, `props`.");
        text.AppendLine("Find errors with a search for `\"l\":\"Error\"` or `\"l\":\"Fatal\"`; the line numbers above point into the files.");
        return text.ToString();
    }

    private static string Time(LogEntry entry) =>
        entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private sealed class SourceCounts
    {
        public int Total { get; set; }

        public int Errors { get; set; }

        public int Warnings { get; set; }
    }

    private sealed record Notable(LogEntry Entry, string File, long Line);
}
