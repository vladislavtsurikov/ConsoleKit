using System.Globalization;
using System.Text.RegularExpressions;

namespace VladislavTsurikov.ConsoleKit.Files;

/// <summary>
/// File names of a log folder:
/// <code>
/// current-Worker-4321-2026-10-09_15-30-12.jsonl              running session
/// 2026-10-09_15-30-12__2026-10-09_17-02-45_Worker.jsonl      finished session
/// 2026-10-09_15-30-12__2026-10-09_17-02-45_Worker_part2.jsonl next part of a large session
/// 2026-10-09_15-30-12__crashed_Worker-4321.jsonl             session that never closed
/// &lt;session&gt;.summary.md                                       summary written when the session ends
/// index.json                                                  list of sessions
/// </code>
/// </summary>
public static partial class LogSessionNames
{
    public const string Extension = ".jsonl";
    public const string SummarySuffix = ".summary.md";
    public const string IndexFileName = "index.json";
    public const string CurrentPrefix = "current-";

    private const string StampFormat = "yyyy-MM-dd_HH-mm-ss";

    public static string Stamp(DateTimeOffset time) => time.ToLocalTime().ToString(StampFormat, CultureInfo.InvariantCulture);

    public static string CurrentBaseName(string app, int processId, DateTimeOffset started) =>
        $"{CurrentPrefix}{Sanitize(app)}-{processId}-{Stamp(started)}";

    public static string FinishedBaseName(string app, DateTimeOffset started, DateTimeOffset ended) =>
        $"{Stamp(started)}__{Stamp(ended)}_{Sanitize(app)}";

    public static string CrashedBaseName(string app, int processId, DateTimeOffset started) =>
        $"{Stamp(started)}__crashed_{Sanitize(app)}-{processId}";

    /// <summary>"name.jsonl" for part 1, "name_part2.jsonl" for the following parts.</summary>
    public static string PartFileName(string baseName, int part) =>
        part <= 1 ? baseName + Extension : $"{baseName}_part{part}{Extension}";

    public static string SummaryFileName(string baseName) => baseName + SummarySuffix;

    /// <summary>The session a file belongs to: its name without the part suffix and extension.</summary>
    public static string BaseNameOf(string fileName)
    {
        string name = Path.GetFileName(fileName);
        if (name.EndsWith(SummarySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return name[..^SummarySuffix.Length];
        }

        name = Path.GetFileNameWithoutExtension(name);
        Match part = PartPattern().Match(name);
        return part.Success ? name[..part.Index] : name;
    }

    public static bool IsCurrent(string fileName) =>
        Path.GetFileName(fileName).StartsWith(CurrentPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads the process id out of a running session's file name.</summary>
    public static bool TryParseCurrent(string fileName, out string app, out int processId)
    {
        Match match = CurrentPattern().Match(Path.GetFileName(fileName));
        app = match.Success ? match.Groups["app"].Value : string.Empty;
        processId = match.Success ? int.Parse(match.Groups["pid"].Value, CultureInfo.InvariantCulture) : 0;
        return match.Success;
    }

    /// <summary>The start time a finished or crashed session name begins with.</summary>
    public static bool TryParseStart(string baseName, out DateTime started) =>
        DateTime.TryParseExact(baseName.Length >= StampFormat.Length ? baseName[..StampFormat.Length] : baseName,
            StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out started);

    /// <summary>The application a finished or crashed session belongs to.</summary>
    public static string AppOf(string baseName)
    {
        Match crashed = CrashedPattern().Match(baseName);
        if (crashed.Success)
        {
            return crashed.Groups["app"].Value;
        }

        Match finished = FinishedPattern().Match(baseName);
        return finished.Success ? finished.Groups["app"].Value : string.Empty;
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new(value.Select(character => invalid.Contains(character) || character is ' ' or '-' ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "App" : clean;
    }

    [GeneratedRegex(@"_part\d+$")]
    private static partial Regex PartPattern();

    [GeneratedRegex(@"^current-(?<app>[^-]+)-(?<pid>\d+)-")]
    private static partial Regex CurrentPattern();

    [GeneratedRegex(@"^[\d_-]+__crashed_(?<app>[^-]+)-\d+$")]
    private static partial Regex CrashedPattern();

    [GeneratedRegex(@"^[\d_-]+__[\d_-]+?_(?<app>[^_]+)$")]
    private static partial Regex FinishedPattern();
}
