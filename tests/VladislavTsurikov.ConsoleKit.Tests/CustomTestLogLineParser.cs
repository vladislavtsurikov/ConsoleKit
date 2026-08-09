using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

[LogLineParser(order: 5)]
public sealed class CustomTestLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLine)
    {
        if (!line.StartsWith("TEST:", StringComparison.Ordinal))
        {
            parsedLine = null;
            return false;
        }

        parsedLine = new ParsedLogLine(
            DateTimeOffset.UtcNow,
            LogSeverity.Info,
            line,
            line);
        return true;
    }
}
