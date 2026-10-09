namespace VladislavTsurikov.ConsoleKit.Core;

/// <summary>
/// Shows entries that another process already journaled, for example Worker logs in Desktop. Level,
/// process and properties are kept; the entries are marked as mirrored so local file sinks skip them.
/// </summary>
public sealed class RemoteLogSource : LogSource
{
    public RemoteLogSource(string id, string displayName)
        : base(id, displayName)
    {
    }

    public void Append(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Write(entry with { SourceId = Id, IsMirrored = true });
    }
}
