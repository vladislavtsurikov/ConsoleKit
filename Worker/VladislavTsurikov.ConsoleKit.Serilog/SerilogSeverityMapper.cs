using Serilog.Events;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public static class SerilogSeverityMapper
{
    public static LogSeverity Map(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => LogSeverity.Info,
            LogEventLevel.Debug => LogSeverity.Info,
            LogEventLevel.Information => LogSeverity.Info,
            LogEventLevel.Warning => LogSeverity.Warning,
            LogEventLevel.Error => LogSeverity.Error,
            LogEventLevel.Fatal => LogSeverity.Error,
            _ => LogSeverity.Info,
        };
    }
}
