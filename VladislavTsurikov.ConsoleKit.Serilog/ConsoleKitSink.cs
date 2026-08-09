using Serilog.Core;
using Serilog.Events;

namespace VladislavTsurikov.ConsoleKit.Serilog;

public sealed class ConsoleKitSink : ILogEventSink
{
    private readonly SerilogLogSource _source;

    public ConsoleKitSink(SerilogLogSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public void Emit(LogEvent logEvent)
    {
        _source.Publish(logEvent);
    }
}
