/**
 * Renders one public diagnostic in a container managed by the host.
 * Pass user-facing values only. The component performs no network requests.
 */
export function showDiagnostic(container, { message, code, traceId, onRetry } = {}) {
    const document = container.ownerDocument;
    const panel = document.createElement('section');
    panel.className = 'consolekit-diagnostic';
    panel.setAttribute('role', 'alert');

    const summary = document.createElement('p');
    summary.textContent = message ?? 'Не удалось загрузить приложение.';
    panel.append(summary);

    if (code || traceId) {
        const details = document.createElement('dl');
        for (const [label, value] of [['Код ошибки', code], ['Номер обращения', traceId]]) {
            if (!value) continue;
            const term = document.createElement('dt');
            const description = document.createElement('dd');
            term.textContent = label;
            description.textContent = value;
            details.append(term, description);
        }
        panel.append(details);
    }

    if (typeof onRetry === 'function') {
        const retry = document.createElement('button');
        retry.type = 'button';
        retry.textContent = 'Повторить';
        retry.addEventListener('click', onRetry);
        panel.append(retry);
    }

    container.replaceChildren(panel);
}
