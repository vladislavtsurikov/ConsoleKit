---
title: "ConsoleKit architecture"
description: "ConsoleKit has two production boundaries."
---

ConsoleKit has two production boundaries:

```text
Worker/     Headless journal: model, hub, processes, files, Serilog.
DesktopUI/  Avalonia and AbberiaUI presentation.
Showroom/   Standalone demo application for the AbberiaUI components.
tests/      Unit tests of Worker/ (Showroom tests live beside the Showroom).
docs/       This documentation.
scripts/    build, test, Showroom, log folder helpers.
```

| Project | Responsibility |
|---|---|
| `Worker/VladislavTsurikov.ConsoleKit.Core` | `LogEntry` (levels, process, stream, category, properties), `LogHub`, `LogEntryStore`, sources (`LogSource`, `RemoteLogSource`), registry, filter, collapse. |
| `Worker/VladislavTsurikov.ConsoleKit.ProcessLogReader` | `IProcessOutput`, `SystemProcessOutput`, `ProcessLogSource` (streams, lifecycle), `FileTailLineStream`, line parsers. |
| `Worker/VladislavTsurikov.ConsoleKit.Files` | `LogSessionWriter` (session files, summary, index, crash recovery, limits), `LogSessionCatalog`, `LogJsonFormat`. |
| `Worker/VladislavTsurikov.ConsoleKit.Serilog` | Serilog sink and source. |
| `DesktopUI/VladislavTsurikov.ConsoleKit.Avalonia` | `ConsoleViewModel` (incremental), legacy `ConsoleView`. |
| `DesktopUI/VladislavTsurikov.ConsoleKit.Abberia` | `ConsoleToolbarView`, `ConsoleEntriesView`, `ConsoleDetailView`, `ConsolePanel`. |

`Worker/` does not reference Avalonia, AbberiaUI, ThemeKit or any host's process library
(`DependencyBoundaryTests`). Hosts connect their process wrappers through `IProcessOutput`.

```text
Core
├── ProcessLogReader
├── Files
├── Serilog
└── DesktopUI.Avalonia
    └── DesktopUI.Abberia
        └── Showroom
```

Project names, assembly names and namespaces are stable; the folders express dependency
boundaries only.

See also:
- `data-flow.md` — how an entry travels from a process to the UI and the disk;
- `process-logs.md` — journaling the processes an application starts;
- `log-files.md` — the session file format, also for LLMs;
- `adding-log-source.md`, `adding-log-parser.md`.
