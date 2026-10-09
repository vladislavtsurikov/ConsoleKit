using Serilog;
using Serilog.Configuration;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public static class LoggerConfigurationConsoleKitExtensions
{
    private const string DefaultSourceId = "Serilog";
    private const string DefaultSourceDisplayName = "Serilog";

    public static LoggerConfiguration ConsoleKit(
        this LoggerSinkConfiguration sinkConfiguration,
        SerilogLogSource source)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(source);
        return sinkConfiguration.Sink(new ConsoleKitSink(source));
    }

    public static LoggerConfiguration ConsoleKit(
        this LoggerSinkConfiguration sinkConfiguration,
        ILogEntryWriter writer,
        string sourceId = DefaultSourceId,
        string displayName = DefaultSourceDisplayName)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        SerilogLogSource source = new(sourceId, displayName);
        source.Setup(writer);
        source.Enable();
        return sinkConfiguration.Sink(new ConsoleKitSink(source, ownsSource: true));
    }
}
