using Serilog;
using Serilog.Configuration;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public static class LoggerConfigurationConsoleKitExtensions
{
    public static LoggerConfiguration ConsoleKit(
        this LoggerSinkConfiguration sinkConfiguration,
        SerilogLogSource source)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(source);
        return sinkConfiguration.Sink(new ConsoleKitSink(source));
    }
}
