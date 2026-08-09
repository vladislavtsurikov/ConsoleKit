using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class PlainTextLogLineParserTests
{
    [Fact]
    public void TryParse_PreservesArbitraryTextAsInfo()
    {
        PlainTextLogLineParser parser = new();

        bool parsed = parser.TryParse(
            "arbitrary process output",
            out ParsedLogLine? result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal(LogSeverity.Info, result.Severity);
        Assert.Equal("arbitrary process output", result.Message);
        Assert.Equal("arbitrary process output", result.Detail);
    }
}
