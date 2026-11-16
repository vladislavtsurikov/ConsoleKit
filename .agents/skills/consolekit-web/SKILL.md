---
name: consolekit-web
description: Embed or maintain ConsoleKit's HTML diagnostic panel in web apps, including startup failures before .NET WebAssembly is ready. Use for browser diagnostics and trace ID handoff to server logs.
---

# ConsoleKit Web

Use the reusable panel in [Web](../../../Web/README.md). Publish its JS and CSS as static assets from a pinned ConsoleKit revision. Keep the host responsible for mounting, visibility, retry, localization requirements and mapping errors to public fields. Add reusable presentation here so consuming apps can upgrade instead of copying their own markup.

## Startup diagnostics

- Load the panel independently of Avalonia and the WebAssembly module. Catch failure to import or initialize the runtime in plain JavaScript. Keep a short HTML fallback in the host for failure to load the diagnostic module itself.
- Pass only a public message, stable error code and optional request trace ID. The browser panel is available before login. Keep exception details, log history, tokens and internal paths on the server.
- For PlantSales, map the public API error response to the panel. Treat the anonymous session response as the normal path to sign-in. A request trace ID identifies a matching structured record in the server journal. For failures before an HTTP response, omit the ID.
- Keep the existing `Worker/` journal and `DesktopUI/` presentation boundaries. A future authenticated operator console can use an explicit, authorized log source. The startup panel does not fetch the operator journal.

When validating an integration, exercise a failed runtime import, a server error with a trace ID and successful startup. Check that values containing HTML render as text and that the host retry action works on a narrow screen.

## Embedding in an existing project

- For an ASP.NET Core static host, publish the files under its existing static asset root, for example `wwwroot/consolekit/`. For a bundled frontend, import the JS and CSS through the existing bundler. In both cases source the assets from the pinned repository revision and publish them with the host's normal versioning strategy.
- Allocate a dedicated child container inside the existing startup screen. On failure call `showDiagnostic(container, { message, code, traceId, onRetry })`. The function replaces the container's children. The host stops the spinner and decides how retry works.
- Prefer the site's existing startup entry point. Keep error handling attached before dynamically importing the runtime and provide a short host fallback if the module itself is unavailable.
- For desktop UI or server log capture, use [consolekit-integrate](../consolekit-integrate/SKILL.md).
