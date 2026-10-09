using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Files;

/// <summary>Where session files go and how many old ones are kept.</summary>
public sealed class LogFileOptions
{
    /// <summary>The product folder under %LOCALAPPDATA%, shared by all its applications (e.g. "MCPFamily").</summary>
    public string ProductName { get; init; } = "ConsoleKit";

    /// <summary>The application writing the session (e.g. "Worker", "Desktop"); part of every file name.</summary>
    public string AppName { get; init; } = "App";

    /// <summary>The application version written into the session header.</summary>
    public string? AppVersion { get; init; }

    /// <summary>The log folder. Defaults to %LOCALAPPDATA%/&lt;ProductName&gt;/logs.</summary>
    public string? Directory { get; init; }

    /// <summary>Finished sessions kept per application; the oldest are deleted beyond this.</summary>
    public int MaxSessions { get; init; } = 30;

    /// <summary>A session file larger than this is continued in a new part file.</summary>
    public int MaxFileSizeMb { get; init; } = 20;

    /// <summary>The whole folder is kept under this size by deleting the oldest sessions.</summary>
    public int MaxTotalSizeMb { get; init; } = 300;

    /// <summary>Finished sessions older than this are deleted.</summary>
    public int MaxAgeDays { get; init; } = 14;

    /// <summary>Entries below this level stay in memory only.</summary>
    public LogEntryLevel MinimumLevel { get; init; } = LogEntryLevel.Debug;

    /// <summary>How often buffered entries are flushed to disk. Errors are flushed at once.</summary>
    public TimeSpan FlushInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Optional masking of secrets before an entry is written.</summary>
    public Func<LogEntry, LogEntry>? Redactor { get; init; }

    /// <summary>Finalize the session when the process exits normally.</summary>
    public bool FinalizeOnProcessExit { get; init; } = true;

    public string ResolveDirectory()
    {
        return Directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductName,
            "logs");
    }
}
