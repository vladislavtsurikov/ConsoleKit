using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

/// <summary>How a <see cref="ProcessLogSource"/> turns process output into entries.</summary>
public sealed class ProcessLogOptions
{
    /// <summary>
    /// Level of a stdout line that does not state its own level.
    /// </summary>
    public LogEntryLevel StandardOutputLevel { get; init; } = LogEntryLevel.Information;

    /// <summary>
    /// Level of a stderr line that does not state its own level. Information by default: stdio MCP
    /// servers and many CLIs log ordinary messages to stderr because stdout carries their protocol.
    /// </summary>
    public LogEntryLevel StandardErrorLevel { get; init; } = LogEntryLevel.Information;

    /// <summary>Record "Started", "Restarted" and "Exited with code" entries.</summary>
    public bool RecordLifecycle { get; init; } = true;

    /// <summary>How many recent stderr lines are attached to the entry of a failed exit.</summary>
    public int ExitTailLineCount { get; init; } = 20;

    /// <summary>How long a line waits for continuation lines (stack traces) before it is written.</summary>
    public TimeSpan ContinuationDelay { get; init; } = TimeSpan.FromMilliseconds(75);
}
