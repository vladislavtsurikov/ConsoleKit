# ConsoleKit

Reusable structured console and process-log viewer for .NET 10 and Avalonia 11.3.10.
It provides a bounded thread-safe log store, lifecycle-safe sources, severity/source/search
filtering, consecutive-entry collapse, Serilog integration, process-line parsing and a
Unity-style Avalonia console view.

## Modules

| Project | Responsibility |
|---|---|
| `VladislavTsurikov.ConsoleKit.Core` | Log model, bounded store, source lifecycle, filtering, counters and collapse. |
| `VladislavTsurikov.ConsoleKit.Serilog` | Serilog sink, source and severity mapping. |
| `VladislavTsurikov.ConsoleKit.ProcessLogReader` | Process line streams, parser discovery and process log sources. |
| `VladislavTsurikov.ConsoleKit.Avalonia` | Reusable console UI, view-models, virtualized list and refresh coalescing. |

## Data flow

```text
Serilog events -> SerilogLogSource -------------------+
                                                       |
Process stdout/stderr -> ILogLineStream -> parsers -> ProcessLogSource
                                                       |
                                                       v
                                                LogEntryStore
                                                       |
                         filter + collapse + counts <--+
                                                       |
                                                       v
                                                   ConsoleView
```

## Add a process source

1. Create or obtain an `ILogLineStream`.
2. Create `ProcessLogSource` with a stable source id and display name.
3. Register it in `LogSourceRegistry` and call `Enable()`.
4. Bind one `ConsoleViewModel` to `ConsoleView`.

```csharp
ConsoleSettings settings = new();
LogEntryStore store = new(settings);
LogSourceRegistry registry = new(store);
ProcessLogSource source = new("worker", "Worker", lineStream);
registry.Register(source);
source.Enable();
ConsoleViewModel viewModel = new(store, registry, settings);
```

## Add a parser

Implement `ILogLineParser`, give the class a parameterless constructor and annotate it.
Lower order values run first. `PlainTextLogLineParser` uses order `1000` and is the
terminal fallback.

```csharp
[LogLineParser(order: 20)]
public sealed class VendorLogLineParser : ILogLineParser
{
    public bool TryParse(string line, out ParsedLogLine? parsedLine)
    {
        parsedLine = null;
        return false;
    }
}
```

`LogLineParserRegistry` scans loaded assemblies once and caches the ordered parser list.
Adding a parser does not require editing the registry.

## Serilog

Create and register a `SerilogLogSource`, then connect it to a logger:

```csharp
SerilogLogSource source = new("desktop", "Desktop");
registry.Register(source);
source.Enable();
ILogger logger = new LoggerConfiguration()
    .WriteTo.ConsoleKit(source)
    .CreateLogger();
```

Verbose, Debug and Information map to Info; Warning maps to Warning; Error and Fatal map
to Error. The original level, properties, exception and stack trace remain in `Detail`.

## Console UI

`ConsoleView` includes Clear, Collapse, search, source toggles, severity counters/toggles,
a virtualized entry list, Copy command and a detail panel. UI refreshes are coalesced by
`ConsoleRefreshScheduler`; the default interval is 100 ms. Theme colors and fonts are read
through `DynamicResource` keys compatible with ThemeKit (`TkSurfaceAltBrush`,
`TkBorderSubtleBrush`, `TkCodeFontFamily`, `TkWarningBrush`, `TkDangerBrush`).

## Settings

`ConsoleSettings.MaxEntryCount` defaults to 1000 and bounds the in-memory store.
`ConsoleSettings.RefreshIntervalMilliseconds` defaults to 100.

## Repository use

ConsoleKit targets .NET 10. The repository is designed to be consumed as a git submodule
and by `ProjectReference`.

```powershell
git submodule update --init --recursive
```

The solution file is `ConsoleKit.slnx`; tests live in
`tests/VladislavTsurikov.ConsoleKit.Tests`.
