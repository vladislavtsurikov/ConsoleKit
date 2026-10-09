namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

/// <summary>What a process journal needs from a running process: its two streams and its lifecycle.</summary>
/// <remarks>
/// ConsoleKit does not know how a host starts processes. Hosts adapt their own process wrapper to this
/// interface (see <see cref="SystemProcessOutput"/> for <see cref="System.Diagnostics.Process"/>), so a
/// process runner library never has to reference ConsoleKit and the other way round.
/// </remarks>
public interface IProcessOutput
{
    string DisplayName { get; }

    int? ProcessId { get; }

    event Action<string>? OutputReceived;

    event Action<string>? ErrorReceived;

    event Action<ProcessStartedInfo>? Started;

    event Action<int?>? Exited;
}

/// <summary>A process that has just started: the journal records its id and command line.</summary>
public sealed record ProcessStartedInfo(int ProcessId, string CommandLine, string? WorkingDirectory = null);
