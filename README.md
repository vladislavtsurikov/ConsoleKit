# ConsoleKit

A structured log journal and console for .NET 10 and Avalonia 11.3. ConsoleKit:

- **Collects logs** from the application itself (Serilog, `ILogger` adapters) and from the processes it
  starts (stdout and stderr, starts, restarts, exit codes);
- **Shows them** in a Unity-style console: filters, search, collapse, details;
- **Writes them to disk** as session files with a summary that people and LLMs can read after the
  application is gone. Old sessions are kept within limits.

## Repository layout

| Folder | Responsibility |
|---|---|
| `Worker/` | Headless journal: Core, ProcessLogReader, Files, Serilog. No UI. |
| `DesktopUI/` | Avalonia view-model and views, AbberiaUI components. |
| `Showroom/` | Standalone AbberiaUI Showroom of the components, and its tests. |
| `tests/` | Tests of `Worker/`. |
| `docs/` | Architecture, data flow, processes, log file format, extension guides. |
| `scripts/` | Build, test, Showroom, log folder helpers. |

## Quick start

```csharp
// One journal: memory for the console, session files on disk.
using LogHub hub = new(new ConsoleSettings { MaxEntryCount = 5000 });
LogSessionWriter journal = new(new LogFileOptions { ProductName = "MyProduct", AppName = "Worker" });
hub.AddSink(journal);

// A process the app starts: both streams, start, restart and exit code.
Process process = Process.Start(startInfoWithRedirectedStreams)!;
ProcessLogSource tool = new("tool:build", "Build", SystemProcessOutput.Attach(process));
hub.Sources.Register(tool);
tool.Enable();

// The console.
ConsoleViewModel console = new(hub) { LogDirectory = journal.Directory };
```

- **Session file:** `journal.CurrentFilePath` is the file being written now, in
  `%LOCALAPPDATA%/MyProduct/logs`.
- **Finalizing:** when the hub is disposed, or the process exits normally, the session is renamed with
  its end time and gets a `.summary.md`.
- **Crashes:** sessions of crashed processes are recovered the next time the folder is opened.

## Commands

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\start-showroom.ps1
.\scripts\open-logs.ps1 -Product MCPFamily      # open the log folder (-PathOnly prints it)
.\scripts\tail-logs.ps1 -Product MCPFamily -App Worker -Problems
```

## Documentation

- [Architecture](docs/architecture.md): projects and dependency rules.
- [Data flow](docs/data-flow.md): from a process or logger to the console and the disk.
- [Journaling processes](docs/process-logs.md): own, background and foreign processes; stderr levels.
- [Log files](docs/log-files.md): session file format, limits, summary — also a guide for LLMs.
- [Adding a source](docs/adding-log-source.md), [adding a parser](docs/adding-log-parser.md).

## Using the repository

ConsoleKit is consumed as a git submodule (MCPFamily, DevRelay) through `ProjectReference`:

```powershell
git submodule update --init --recursive
```

AbberiaUI is resolved through `AbberiaUIRoot`, which defaults to `AbberiaUI` next to this repository;
override it with `dotnet build -p:AbberiaUIRoot=D:\src\AbberiaUI`.
