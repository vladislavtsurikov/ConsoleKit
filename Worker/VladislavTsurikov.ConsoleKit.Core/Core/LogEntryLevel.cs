namespace VladislavTsurikov.ConsoleKit.Core;

/// <summary>
/// The full level of an entry, as the producer reported it. The console groups these into the
/// three <see cref="LogSeverity"/> filters; files and tools keep the exact level.
/// </summary>
public enum LogEntryLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}

/// <summary>Which output stream of a process an entry came from.</summary>
public enum LogStream
{
    None,
    StandardOutput,
    StandardError,
}

public static class LogLevels
{
    /// <summary>The console filter group of a level: Trace and Debug count as Info, Fatal as Error.</summary>
    public static LogSeverity ToSeverity(LogEntryLevel level)
    {
        return level switch
        {
            LogEntryLevel.Warning => LogSeverity.Warning,
            LogEntryLevel.Error or LogEntryLevel.Fatal => LogSeverity.Error,
            _ => LogSeverity.Info,
        };
    }

    /// <summary>The level an entry has when its producer only knew the severity group.</summary>
    public static LogEntryLevel FromSeverity(LogSeverity severity)
    {
        return severity switch
        {
            LogSeverity.Warning => LogEntryLevel.Warning,
            LogSeverity.Error => LogEntryLevel.Error,
            _ => LogEntryLevel.Information,
        };
    }

    /// <summary>Parses common level names: Serilog, Microsoft.Extensions.Logging, short forms.</summary>
    public static bool TryParse(string? text, out LogEntryLevel level)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "trace" or "trc" or "verbose" or "vrb":
                level = LogEntryLevel.Trace;
                return true;
            case "debug" or "dbg":
                level = LogEntryLevel.Debug;
                return true;
            case "information" or "info" or "inf":
                level = LogEntryLevel.Information;
                return true;
            case "warning" or "warn" or "wrn":
                level = LogEntryLevel.Warning;
                return true;
            case "error" or "err" or "fail":
                level = LogEntryLevel.Error;
                return true;
            case "fatal" or "ftl" or "critical" or "crit":
                level = LogEntryLevel.Fatal;
                return true;
            default:
                level = LogEntryLevel.Information;
                return false;
        }
    }
}
