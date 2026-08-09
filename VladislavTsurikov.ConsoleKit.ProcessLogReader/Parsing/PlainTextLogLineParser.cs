using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

[LogLineParser(order: 1000)]
public sealed class PlainTextLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLine)
    {
        parsedLine = new ParsedLogLine(
            DateTimeOffset.Now,
            LogSeverity.Info,
            line,
            line,
            IsContinuation(line));
        return true;
    }

    private static bool IsContinuation(string line)
    {
        string trimmed = line.TrimStart();
        return line.Length != trimmed.Length ||
               trimmed.StartsWith("at ", StringComparison.Ordinal) ||
               trimmed.StartsWith("--- End of stack trace", StringComparison.Ordinal);
    }
}
