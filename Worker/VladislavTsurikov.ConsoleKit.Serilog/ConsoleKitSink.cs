using Serilog.Core;
using Serilog.Events;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public sealed class ConsoleKitSink : ILogEventSink, IDisposable
{
    private readonly SerilogLogSource _source;
    private readonly bool _ownsSource;
    private bool _disposed;

    public ConsoleKitSink(SerilogLogSource source, bool ownsSource = false)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _ownsSource = ownsSource;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_disposed)
        {
            return;
        }

        _source.Publish(logEvent);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsSource)
        {
            _source.Destroy();
        }

        _disposed = true;
    }
}
