# Integrating into an existing host

## References and compatibility

Check the target frameworks and central package versions of the host and pinned ConsoleKit revision. The repository supports a Git submodule and `ProjectReference`. Place references in the projects that own each responsibility:

| Host requirement | ConsoleKit project |
|---|---|
| Journal and custom source | `Worker/...ConsoleKit.Core` |
| Session files, summaries and retention | `Worker/...ConsoleKit.Files` |
| Existing Serilog events | `Worker/...ConsoleKit.Serilog` |
| Process stdout/stderr or local log tail | `Worker/...ConsoleKit.ProcessLogReader` |
| Desktop viewer | `DesktopUI/...ConsoleKit.Avalonia`, or `...ConsoleKit.Abberia` for `ConsolePanel` |

Resolve `AbberiaUIRoot` to the host's compatible AbberiaUI checkout when using the Abberia panel. Keep Worker and ASP.NET Core references headless. Confirm dependency checkout and restore also work in the host's CI.

## Route the host's logs

- **Serilog** — register and enable a `SerilogLogSource` on the hub and attach `ConsoleKitSink` to the existing Serilog pipeline. Keep existing sinks. If `ILogger` already forwards into Serilog, use that route once.
- **Microsoft ILogger directly** — inspect whether the pinned revision provides a compatible adapter. Otherwise add an `ILoggerProvider` bridge at the composition root, using the source contract in [Adding a log source](../../../../docs/adding-log-source.md). Preserve levels, categories, structured properties and scopes such as `TraceId`. Avoid logging back into the same provider from the bridge.
- **Child process** — if the host owns the process and its redirected streams, attach `SystemProcessOutput` or adapt its existing stream wrapper to `IProcessOutput`, then register and enable `ProcessLogSource`. For a local file, use `FileTailLineStream` with the parser appropriate to its format. See [Process logs](../../../../docs/process-logs.md).
- **Existing journal in another process** — use `RemoteLogSource` to mirror entries received through a host-managed authorized transport. The source itself is not transport. Retain the original writer as owner of persistence.

## Wire the lifetime and UI

Create `LogHub` during application composition and set an appropriate memory bound through `ConsoleSettings`. For persistence, add a `LogSessionWriter` sink and choose the product/app names, location and retention suitable for the host account. On shutdown, stop producers and let the host dispose the hub so session files finalize. Keep UI subscriptions and view models scoped to the window or view that owns them.

In an Avalonia host that already uses AbberiaUI, bind the view model to the panel:

```csharp
var console = new ConsoleViewModel(hub);
var panel = new ConsolePanel { DataContext = console };
```

Mount it inside the host's diagnostics tab, dock or window. If the host only uses plain Avalonia, inspect `ConsoleView` first. Read [data flow](../../../../docs/data-flow.md) for the source, store, sink and view boundaries.

## PlantSales example

The public browser panel shows a brief message, code and optional correlation ID. ASP.NET Core already writes structured JSON with `TraceId` to the server's local journal. Keep the existing provider. For an operator viewer on the server host, read that file through a process/file log source with a JSON parser that preserves `TraceId`. For a remote operator viewer, put access controls in the host transport and use a separate source. Keep full exceptions and log history out of the pre-login page.
