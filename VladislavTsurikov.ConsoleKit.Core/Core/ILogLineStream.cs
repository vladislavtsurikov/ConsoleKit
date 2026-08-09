namespace VladislavTsurikov.ConsoleKit.Core;

public interface ILogLineStream
{
    event EventHandler<string>? LineReceived;
}
