namespace VladislavTsurikov.ConsoleKit.Core;

public abstract class LogSource
{
    private static long s_nextEntryId;

    private ILogEntryWriter? _writer;
    private bool _isDestroyed;

    protected LogSource(string id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id.Trim();
        DisplayName = displayName.Trim();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public bool IsEnabled { get; private set; }

    public bool IsSetup => _writer is not null;

    public void Setup(ILogEntryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ThrowIfDestroyed();

        if (_writer is not null)
        {
            return;
        }

        _writer = writer;
        SetupCore();
        OnSetupCompleted();
    }

    public void Enable()
    {
        ThrowIfDestroyed();
        EnsureSetup();

        if (IsEnabled)
        {
            return;
        }

        EnableCore();
        IsEnabled = true;
        OnEnabled();
    }

    public void Disable()
    {
        if (_isDestroyed || !IsEnabled)
        {
            return;
        }

        OnBeforeDisable();
        DisableCore();
        IsEnabled = false;
    }

    public void Destroy()
    {
        if (_isDestroyed)
        {
            return;
        }

        Disable();
        DestroyCore();
        _writer = null;
        _isDestroyed = true;
    }

    protected long NextEntryId()
    {
        return Interlocked.Increment(ref s_nextEntryId);
    }

    protected void Write(LogEntry entry)
    {
        if (!IsEnabled)
        {
            return;
        }

        EnsureSetup();
        _writer!.Write(entry);
    }

    protected virtual void SetupCore()
    {
    }

    protected virtual void OnSetupCompleted()
    {
    }

    protected virtual void EnableCore()
    {
    }

    protected virtual void OnEnabled()
    {
    }

    protected virtual void OnBeforeDisable()
    {
    }

    protected virtual void DisableCore()
    {
    }

    protected virtual void DestroyCore()
    {
    }

    private void EnsureSetup()
    {
        if (_writer is null)
        {
            throw new InvalidOperationException(
                $"Log source '{Id}' must be set up before it can be enabled or write entries.");
        }
    }

    private void ThrowIfDestroyed()
    {
        ObjectDisposedException.ThrowIf(_isDestroyed, this);
    }
}
