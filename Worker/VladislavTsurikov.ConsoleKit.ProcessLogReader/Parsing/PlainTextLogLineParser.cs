using System.Text.RegularExpressions;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

/// <summary>
/// The fallback parser: every line is accepted. A level written at the start of the line is
/// recognized ("[ERR] ...", "[12:00:01 WRN] ...", "warn: Category[0]", "ERROR ..."); otherwise the line
/// has no level of its own and the process source uses the level configured for its stream.
/// </summary>
[LogLineParser(order: 1000)]
public sealed partial class PlainTextLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLine)
    {
        LogEntryLevel? level = DetectLevel(line);
        parsedLine = new ParsedLogLine(
            DateTimeOffset.Now,
            level is { } known ? LogLevels.ToSeverity(known) : LogSeverity.Info,
            line,
            line,
            IsContinuation(line))
        {
            Level = level,
        };
        return true;
    }

    private static LogEntryLevel? DetectLevel(string line)
    {
        Match match = LevelPattern().Match(line);
        if (!match.Success)
        {
            return null;
        }

        return LogLevels.TryParse(match.Groups["level"].Value, out LogEntryLevel level) ? level : null;
    }

    private static bool IsContinuation(string line)
    {
        string trimmed = line.TrimStart();
        return line.Length != trimmed.Length ||
               trimmed.StartsWith("at ", StringComparison.Ordinal) ||
               trimmed.StartsWith("--- End of stack trace", StringComparison.Ordinal);
    }

    // [ERR] / [12:00:01 WRN] / warn: / fail: / ERROR / WARNING: at the start of the line.
    [GeneratedRegex(
        @"^\[(?:[\d:.\-T ]+\s)?(?<level>[A-Za-z]{3,11})\]|^(?<level>trce|dbug|info|warn|fail|crit):|^(?<level>TRACE|DEBUG|INFO|WARN|WARNING|ERROR|FATAL|CRITICAL)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex LevelPattern();
}
