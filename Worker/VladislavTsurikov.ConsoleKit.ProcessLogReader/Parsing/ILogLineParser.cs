namespace VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

public interface ILogLineParser
{
    bool TryParse(string line, out ParsedLogLine? parsedLine);
}
