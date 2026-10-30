---
title: "Adding a log source"
description: "hub.Sources.Register(source); source.Enable(); `."
---

1. Derive from `LogSource`, or use an existing one:
   - `SerilogLogSource` — Serilog events;
   - `ProcessLogSource` — a process or a line stream;
   - `RemoteLogSource` — entries already journaled by another process.
2. Give the source a stable id and a user-facing display name.
3. Register it in the hub's registry and call `Enable()` when it should begin writing.
4. `Disable()` stops writing and keeps the source; disposing the hub destroys every source after its
   last entries reached the sinks.

```csharp
using LogHub hub = new();
ProcessLogSource source = new("endpoint:files", "Files", processOutput);

hub.Sources.Register(source);
source.Enable();
```

A source should report everything it knows:
- the exact `Level`;
- `ProcessId` and `Stream` for process output;
- `Category` and `Properties` for structured logs.

`LogEntry.Create(time, level, sourceId, message, detail)` sets the console severity group from the
level. Do not make UI controls responsible for acquiring data from a process.
