using System.Diagnostics;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

public sealed class ProcessLogLineStream : ILogLineStream, IDisposable
{
    private readonly Process _process;
    private bool _disposed;

    public ProcessLogLineStream(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _process.OutputDataReceived += OnDataReceived;
        _process.ErrorDataReceived += OnDataReceived;
    }

    public event EventHandler<string>? LineReceived;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _process.OutputDataReceived -= OnDataReceived;
        _process.ErrorDataReceived -= OnDataReceived;
        _disposed = true;
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.Data))
        {
            return;
        }

        LineReceived?.Invoke(this, eventArgs.Data);
    }
}
