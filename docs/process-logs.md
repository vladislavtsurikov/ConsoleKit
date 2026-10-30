---
title: "Journaling processes"
description: "ProcessLogSource turns a process into journal entries."
---

`ProcessLogSource` turns a process into journal entries:

- every stdout and stderr line becomes an entry; stack-trace lines are joined to the line above;
- `Stream` says which stream a line came from, `ProcessId` which process wrote it;
- with an `IProcessOutput` the start, every restart and the exit are recorded too:
  - `Started: PID 1234`, with the command line in the detail;
  - `Restarted: PID 1240`;
  - `Exited with code 0` — Information;
  - `Exited with code 1` — **Error**, with the last 20 stderr lines in the detail.

## Levels of process lines

The level of a line comes from the line itself when it says it:

- Serilog compact JSON (`"@l"`);
- `[ERR]`, `[12:00:01 WRN]`;
- `warn:`, `fail:` (Microsoft.Extensions.Logging console);
- `ERROR …` at the start of the line.

Otherwise it comes from the stream: `ProcessLogOptions.StandardOutputLevel` and
`StandardErrorLevel`, both **Information** by default. stdio MCP servers and many CLIs write their
ordinary logs to stderr, because stdout carries their protocol, so stderr is not a warning by itself.

## Connecting a process

ConsoleKit does not start processes and does not depend on any process-runner library. It needs an
`IProcessOutput`: two line events, a start event and an exit event.

### A `System.Diagnostics.Process`

```csharp
using LogHub hub = new();
Process process = Process.Start(startInfoWithRedirectedStreams)!;
SystemProcessOutput output = SystemProcessOutput.Attach(process, "my-tool");
ProcessLogSource source = new("tool:my-tool", "My tool", output);
hub.Sources.Register(source);
source.Enable();
```

`Attach` enables exit events and starts reading redirected streams nobody reads yet. Pass
`beginReading: false` when the caller already called `BeginOutputReadLine` itself.

### A host's own process wrapper

Implement `IProcessOutput` over it. MCPFamily does this for `ProcessControl.ChildProcess` in
`ChildProcessOutput` (Worker), so neither library references the other.

### A process you did not start (or a background process)

Its stdout is not available, but its log file is. `FileTailLineStream` follows a file while
another process writes it:

- the file is opened shared;
- the read position is remembered;
- a truncated or recreated file is read from the start.

```csharp
FileTailLineStream tail = new(@"C:\logs\worker.log", fromEnd: true);
ProcessLogSource source = new("worker-file", "Worker (file)", tail);
```

## Plain line streams

The older constructor `ProcessLogSource(id, name, ILogLineStream)` still works for any line
source (no stream or lifecycle information).
