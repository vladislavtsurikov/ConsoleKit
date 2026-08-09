using Serilog;
using Serilog.Events;
using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.Serilog;

namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class SerilogIntegrationTests
{
    [Theory]
    [InlineData(LogEventLevel.Verbose, LogSeverity.Info)]
    [InlineData(LogEventLevel.Debug, LogSeverity.Info)]
    [InlineData(LogEventLevel.Information, LogSeverity.Info)]
    [InlineData(LogEventLevel.Warning, LogSeverity.Warning)]
    [InlineData(LogEventLevel.Error, LogSeverity.Error)]
    [InlineData(LogEventLevel.Fatal, LogSeverity.Error)]
    public void SeverityMapper_MapsAllSerilogLevels(LogEventLevel level, LogSeverity expected)
    {
        Assert.Equal(expected, SerilogSeverityMapper.Map(level));
    }

    [Fact]
    public void Sink_DeliversMessageAndExceptionDetail_WhenSourceIsEnabled()
    {
        LogEntryStore store = new(new ConsoleSettings());
        LogSourceRegistry registry = new(store);
        SerilogLogSource source = new("desktop", "Desktop");
        registry.Register(source);
        source.Enable();
        using ILogger logger = new LoggerConfiguration()
            .WriteTo.ConsoleKit(source)
            .CreateLogger();
        InvalidOperationException exception = new("boom");

        logger.Error(exception, "Operation failed");

        LogEntry entry = Assert.Single(store.Snapshot());
        Assert.Equal(LogSeverity.Error, entry.Severity);
        Assert.Contains("Operation failed", entry.Message);
        Assert.Contains("boom", entry.Detail);
    }

    [Fact]
    public void Sink_DoesNotDeliverWhileSourceIsDisabled()
    {
        LogEntryStore store = new(new ConsoleSettings());
        LogSourceRegistry registry = new(store);
        SerilogLogSource source = new("desktop", "Desktop");
        registry.Register(source);
        source.Enable();
        using ILogger logger = new LoggerConfiguration()
            .WriteTo.ConsoleKit(source)
            .CreateLogger();
        source.Disable();

        logger.Information("hidden");
        source.Enable();
        logger.Information("visible");

        LogEntry entry = Assert.Single(store.Snapshot());
        Assert.Contains("visible", entry.Message);
    }
}
