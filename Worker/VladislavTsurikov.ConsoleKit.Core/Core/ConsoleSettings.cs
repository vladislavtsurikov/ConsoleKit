namespace VladislavTsurikov.ConsoleKit.Core;

public sealed class ConsoleSettings
{
    public const int DefaultMaxEntryCount = 1000;
    public const int DefaultRefreshIntervalMilliseconds = 100;

    private int _maxEntryCount = DefaultMaxEntryCount;
    private int _refreshIntervalMilliseconds = DefaultRefreshIntervalMilliseconds;

    public int MaxEntryCount
    {
        get => _maxEntryCount;
        set => _maxEntryCount = Math.Max(1, value);
    }

    public int RefreshIntervalMilliseconds
    {
        get => _refreshIntervalMilliseconds;
        set => _refreshIntervalMilliseconds = Math.Max(1, value);
    }
}
