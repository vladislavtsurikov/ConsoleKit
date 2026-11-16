# HTML diagnostic panel

The panel renders a public error in any host page. It is an ES module and a stylesheet that can load before .NET or Avalonia. The host supplies the container and handles recovery.

Publish `diagnostic.js` and `diagnostic.css` with the application's static files, then load them from the same origin:

```html
<link rel="stylesheet" href="./consolekit/diagnostic.css">
<div id="startup-diagnostic"></div>
<script type="module">
    import { showDiagnostic } from './consolekit/diagnostic.js';

    showDiagnostic(document.getElementById('startup-diagnostic'), {
        message: 'Не удалось загрузить приложение.',
        code: 'server',
        traceId: 'example-request-id',
        onRetry: () => location.reload()
    });
</script>
```

Call the function only after an error. It replaces the container's children, so give it a dedicated container. To clear it, call `container.replaceChildren()`. Omit `traceId` when no response supplied one and omit `onRetry` when retry is unavailable.

Server operators use the trace ID to match the error with a structured record. The panel never reads the server journal and renders incoming text without interpreting markup.
