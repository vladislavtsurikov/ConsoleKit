---
name: consolekit-integrate
description: Integrate ConsoleKit into an existing .NET worker, ASP.NET Core service, process host or Avalonia app. Choose project references, connect existing log sources, mount the desktop console and configure persistence. For HTML startup diagnostics, use consolekit-web.
---

# Integrate ConsoleKit

1. Inspect the host's target framework, logging providers, composition root, views and shutdown ownership. Read [host integration](references/host-integration.md) and verify the APIs against the ConsoleKit revision the host consumes.
2. Add only the projects needed for the selected sources, storage and presentation. Reuse the host's existing logger and output sinks.
3. Create one hub per journal. Wire sources and optional persistence at the composition root. Mount an Avalonia view over the hub if the host needs a desktop console. For a browser startup screen, follow [consolekit-web](../consolekit-web/SKILL.md).
4. Check that a representative message reaches the intended destination once, retains its level and correlation properties, and that shutdown releases subscriptions and flushes the session. Use existing host checks when available.
