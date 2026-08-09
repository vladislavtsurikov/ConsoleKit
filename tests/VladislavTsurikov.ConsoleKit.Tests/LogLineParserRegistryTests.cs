using VladislavTsurikov.ConsoleKit.ProcessLogReader.Parsing;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class LogLineParserRegistryTests
{
    [Fact]
    public void Parsers_AreDiscoveredInOrder_AndScannedOnce()
    {
        IReadOnlyList<ILogLineParser> first = LogLineParserRegistry.Parsers;
        IReadOnlyList<ILogLineParser> second = LogLineParserRegistry.Parsers;

        Assert.Same(first, second);
        Assert.Equal(1, LogLineParserRegistry.ScanCount);
        Assert.Contains(first, parser => parser is CustomTestLogLineParser);
        Assert.True(
            first.ToList().FindIndex(parser => parser is CustomTestLogLineParser) <
            first.ToList().FindIndex(parser => parser is CompactJsonLogLineParser));
    }
}
