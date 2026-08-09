using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class CompactJsonLogLineParserTests
{
    [Fact]
    public void TryParse_ReadsClefLevelMessageTimestampAndException()
    {
        CompactJsonLogLineParser parser = new();
        string line = "{\"@t\":\"2026-08-09T10:00:00.000Z\",\"@l\":\"Error\",\"@m\":\"Failed\",\"@x\":\"stack\"}";

        bool parsed = parser.TryParse(line, out ParsedLogLine? result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal(LogSeverity.Error, result.Severity);
        Assert.Equal("Failed", result.Message);
        Assert.Contains("stack", result.Detail);
        Assert.Equal(2026, result.Timestamp.Year);
    }
}
