using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Threading;
using VladislavTsurikov.ConsoleKit.Core;
using VladislavTsurikov.ConsoleKit.Core.Collapsing;
using VladislavTsurikov.ConsoleKit.Core.Filtering;

namespace VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;

public sealed class ConsoleViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly LogEntryStore _store;
    private readonly LogSourceRegistry _sourceRegistry;
    private readonly LogEntryFilter _filter = new();
    private readonly LogEntryCollapser _collapser = new();
    private readonly ConsoleRefreshScheduler _refreshScheduler;
    private readonly ConcurrentQueue<LogEntry> _appended = new();
    private long _evictedThroughId;
    private int _pendingInfo;
    private int _pendingWarning;
    private int _pendingError;
    private long _lastShownId;
    private bool _needsRebuild = true;
    private string? _logDirectory;
    private string? _logFilePath;
    private bool _isCollapseEnabled;
    private string _searchText = string.Empty;
    private bool _isInfoEnabled = true;
    private bool _isWarningEnabled = true;
    private bool _isErrorEnabled = true;
    private int _infoCount;
    private int _warningCount;
    private int _errorCount;
    private LogEntryItemViewModel? _selectedEntry;
    private bool _disposed;

    public ConsoleViewModel(
        LogEntryStore store,
        LogSourceRegistry sourceRegistry,
        ConsoleSettings settings)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sourceRegistry = sourceRegistry ?? throw new ArgumentNullException(nameof(sourceRegistry));
        ArgumentNullException.ThrowIfNull(settings);

        Entries = new ObservableCollection<LogEntryItemViewModel>();
        Sources = new ObservableCollection<LogSourceToggleViewModel>();
        ClearCommand = new ConsoleCommand(Clear);
        CopyCommand = new ConsoleCommand(CopyVisibleEntries);
        OpenLogFolderCommand = new ConsoleCommand(OpenLogFolder);
        CopyLogPathCommand = new ConsoleCommand(CopyLogPath);
        _refreshScheduler = new ConsoleRefreshScheduler(
            settings.RefreshIntervalMilliseconds,
            Refresh);

        _store.EntryAppended += OnEntryAppended;
        _store.EntryEvicted += OnEntryEvicted;
        _store.EntriesCleared += OnEntriesCleared;
        _sourceRegistry.SourcesChanged += OnSourcesChanged;
        RefreshSources();
        Refresh();
    }

    /// <summary>A console over a <see cref="LogHub"/>: its store, sources and settings.</summary>
    public ConsoleViewModel(LogHub hub)
        : this(hub?.Store ?? throw new ArgumentNullException(nameof(hub)), hub.Sources, hub.Settings)
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<string>? CopyRequested;

    public ObservableCollection<LogEntryItemViewModel> Entries { get; }

    public ObservableCollection<LogSourceToggleViewModel> Sources { get; }

    public ICommand ClearCommand { get; }

    public ICommand CopyCommand { get; }

    /// <summary>Opens <see cref="LogDirectory"/> in the file manager.</summary>
    public ICommand OpenLogFolderCommand { get; }

    /// <summary>Copies the current session file path (or the folder) for pasting into an LLM chat.</summary>
    public ICommand CopyLogPathCommand { get; }

    /// <summary>The folder of the on-disk journal, when the host writes one.</summary>
    public string? LogDirectory
    {
        get => _logDirectory;
        set
        {
            if (SetValue(ref _logDirectory, value))
            {
                OnPropertyChanged(nameof(HasLogFiles));
            }
        }
    }

    /// <summary>The session file being written now, when the host writes one.</summary>
    public string? LogFilePath
    {
        get => _logFilePath;
        set
        {
            if (SetValue(ref _logFilePath, value))
            {
                OnPropertyChanged(nameof(HasLogFiles));
            }
        }
    }

    public bool HasLogFiles => !string.IsNullOrEmpty(_logDirectory) || !string.IsNullOrEmpty(_logFilePath);

    public bool IsCollapseEnabled
    {
        get => _isCollapseEnabled;
        set
        {
            if (_isCollapseEnabled == value)
            {
                return;
            }

            _isCollapseEnabled = value;
            OnPropertyChanged();
            RequestRebuild();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            string normalizedValue = value ?? string.Empty;
            if (_searchText == normalizedValue)
            {
                return;
            }

            _searchText = normalizedValue;
            _filter.SearchText = normalizedValue;
            OnPropertyChanged();
            RequestRebuild();
        }
    }

    public bool IsInfoEnabled
    {
        get => _isInfoEnabled;
        set
        {
            if (_isInfoEnabled == value)
            {
                return;
            }

            _isInfoEnabled = value;
            _filter.IsInfoEnabled = value;
            OnPropertyChanged();
            RequestRebuild();
        }
    }

    public bool IsWarningEnabled
    {
        get => _isWarningEnabled;
        set
        {
            if (_isWarningEnabled == value)
            {
                return;
            }

            _isWarningEnabled = value;
            _filter.IsWarningEnabled = value;
            OnPropertyChanged();
            RequestRebuild();
        }
    }

    public bool IsErrorEnabled
    {
        get => _isErrorEnabled;
        set
        {
            if (_isErrorEnabled == value)
            {
                return;
            }

            _isErrorEnabled = value;
            _filter.IsErrorEnabled = value;
            OnPropertyChanged();
            RequestRebuild();
        }
    }

    public int InfoCount
    {
        get => _infoCount;
        private set => SetValue(ref _infoCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetValue(ref _warningCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetValue(ref _errorCount, value);
    }

    public LogEntryItemViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (ReferenceEquals(_selectedEntry, value))
            {
                return;
            }

            _selectedEntry = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDetail));
        }
    }

    /// <summary>The selected entry: its level, origin and properties, then its full detail.</summary>
    public string SelectedDetail => SelectedEntry is { } item ? FormatDetail(item.Entry) : string.Empty;

    public string GetVisibleText()
    {
        return string.Join(
            Environment.NewLine,
            Entries.Select(FormatEntry));
    }

    /// <summary>
    /// Brings the visible list up to date. New entries are appended to it; the list is rebuilt from the
    /// store only after a filter change, a clear, or an eviction that a collapsed row cannot follow.
    /// </summary>
    public void Refresh()
    {
        long evictedThrough = Interlocked.Read(ref _evictedThroughId);
        bool collapsedRowEvicted = _isCollapseEnabled && Entries.Count > 0 && Entries[0].Entry.Id <= evictedThrough;
        if (_needsRebuild || collapsedRowEvicted)
        {
            Rebuild();
            return;
        }

        while (Entries.Count > 0 && Entries[0].Entry.Id <= evictedThrough)
        {
            if (ReferenceEquals(Entries[0], SelectedEntry))
            {
                SelectedEntry = null;
            }

            Entries.RemoveAt(0);
        }

        while (_appended.TryDequeue(out LogEntry? entry))
        {
            if (entry.Id <= _lastShownId)
            {
                continue;
            }

            _lastShownId = entry.Id;
            if (!_filter.Matches(entry))
            {
                continue;
            }

            if (_isCollapseEnabled && Entries.Count > 0 && LogEntryCollapser.CanCollapse(Entries[^1].Entry, entry))
            {
                LogEntryItemViewModel last = Entries[^1];
                LogEntryItemViewModel merged = new(new CollapsedLogEntry(last.Entry, last.RepeatCount + 1));
                bool wasSelected = ReferenceEquals(last, SelectedEntry);
                Entries[^1] = merged;
                if (wasSelected)
                {
                    SelectedEntry = merged;
                }

                continue;
            }

            Entries.Add(new LogEntryItemViewModel(new CollapsedLogEntry(entry, 1)));
        }

        PublishCounts();
    }

    private void Rebuild()
    {
        _needsRebuild = false;
        Interlocked.Exchange(ref _evictedThroughId, 0);
        while (_appended.TryDequeue(out _))
        {
        }

        IReadOnlyList<LogEntry> snapshot = _store.Snapshot();
        if (snapshot.Count > 0)
        {
            _lastShownId = snapshot[^1].Id;
        }

        int info = 0;
        int warning = 0;
        int error = 0;
        foreach (LogEntry entry in snapshot)
        {
            switch (entry.Severity)
            {
                case LogSeverity.Warning:
                    warning++;
                    break;
                case LogSeverity.Error:
                    error++;
                    break;
                default:
                    info++;
                    break;
            }
        }

        Interlocked.Exchange(ref _pendingInfo, info);
        Interlocked.Exchange(ref _pendingWarning, warning);
        Interlocked.Exchange(ref _pendingError, error);
        PublishCounts();

        IReadOnlyList<LogEntry> filteredEntries = snapshot
            .Where(_filter.Matches)
            .ToArray();
        IReadOnlyList<CollapsedLogEntry> visibleEntries = _isCollapseEnabled
            ? _collapser.Collapse(filteredEntries)
            : filteredEntries.Select(entry => new CollapsedLogEntry(entry, 1)).ToArray();

        long? selectedId = SelectedEntry?.Entry.Id;
        Entries.Clear();
        foreach (CollapsedLogEntry entry in visibleEntries)
        {
            Entries.Add(new LogEntryItemViewModel(entry));
        }

        SelectedEntry = selectedId is null
            ? null
            : Entries.FirstOrDefault(item => item.Entry.Id == selectedId.Value);
    }

    private void PublishCounts()
    {
        InfoCount = Volatile.Read(ref _pendingInfo);
        WarningCount = Volatile.Read(ref _pendingWarning);
        ErrorCount = Volatile.Read(ref _pendingError);
    }

    private void RequestRebuild()
    {
        _needsRebuild = true;
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _store.EntryAppended -= OnEntryAppended;
        _store.EntryEvicted -= OnEntryEvicted;
        _store.EntriesCleared -= OnEntriesCleared;
        _sourceRegistry.SourcesChanged -= OnSourcesChanged;
        UnsubscribeSources();
        _refreshScheduler.Dispose();
        _disposed = true;
    }

    private void Clear()
    {
        _store.Clear();
    }

    private void CopyVisibleEntries()
    {
        CopyRequested?.Invoke(this, GetVisibleText());
    }

    private void OnEntryAppended(LogEntry entry)
    {
        Interlocked.Increment(ref CounterFor(entry.Severity));
        _appended.Enqueue(entry);
        _refreshScheduler.RequestRefresh();
    }

    private void OnEntryEvicted(LogEntry entry)
    {
        Interlocked.Decrement(ref CounterFor(entry.Severity));
        long current;
        do
        {
            current = Interlocked.Read(ref _evictedThroughId);
            if (entry.Id <= current)
            {
                break;
            }
        }
        while (Interlocked.CompareExchange(ref _evictedThroughId, entry.Id, current) != current);

        _refreshScheduler.RequestRefresh();
    }

    private void OnEntriesCleared()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RequestRebuild();
        }
        else
        {
            Dispatcher.UIThread.Post(RequestRebuild);
        }
    }

    private ref int CounterFor(LogSeverity severity)
    {
        switch (severity)
        {
            case LogSeverity.Warning:
                return ref _pendingWarning;
            case LogSeverity.Error:
                return ref _pendingError;
            default:
                return ref _pendingInfo;
        }
    }

    private void OpenLogFolder()
    {
        string? folder = _logDirectory ?? (_logFilePath is null ? null : Path.GetDirectoryName(_logFilePath));
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
        }
    }

    private void CopyLogPath()
    {
        string? path = _logFilePath ?? _logDirectory;
        if (!string.IsNullOrEmpty(path))
        {
            CopyRequested?.Invoke(this, path);
        }
    }

    private static string FormatDetail(LogEntry entry)
    {
        List<string> origin = new() { entry.Level.ToString(), entry.SourceId };
        if (entry.ProcessId is { } processId)
        {
            origin.Add($"PID {processId}");
        }

        if (entry.Stream != LogStream.None)
        {
            origin.Add(entry.Stream == LogStream.StandardError ? "stderr" : "stdout");
        }

        if (!string.IsNullOrEmpty(entry.Category))
        {
            origin.Add(entry.Category);
        }

        List<string> lines = new()
        {
            $"{entry.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} · {string.Join(" · ", origin)}",
        };
        if (entry.Properties is { Count: > 0 } properties)
        {
            foreach (KeyValuePair<string, string> property in properties)
            {
                lines.Add($"  {property.Key} = {property.Value}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(entry.Detail);
        return string.Join(Environment.NewLine, lines);
    }

    private void OnSourcesChanged()
    {
        _refreshScheduler.RequestRefresh();
        Dispatcher.UIThread.Post(RefreshSources);
    }

    private void RefreshSources()
    {
        UnsubscribeSources();
        Sources.Clear();

        foreach (LogSource source in _sourceRegistry.Sources)
        {
            LogSourceToggleViewModel sourceViewModel = new(source);
            sourceViewModel.EnabledChanged += OnSourceEnabledChanged;
            Sources.Add(sourceViewModel);
            _filter.SetSourceEnabled(source.Id, source.IsEnabled);
        }
    }

    private void UnsubscribeSources()
    {
        foreach (LogSourceToggleViewModel source in Sources)
        {
            source.EnabledChanged -= OnSourceEnabledChanged;
        }
    }

    private void OnSourceEnabledChanged(LogSourceToggleViewModel source)
    {
        _filter.SetSourceEnabled(source.Id, source.IsEnabled);
        RequestRebuild();
    }

    private static string FormatEntry(LogEntryItemViewModel item)
    {
        string repeatSuffix = item.RepeatCount > 1 ? $" x{item.RepeatCount}" : string.Empty;
        return $"[{item.TimestampText}] [{item.Entry.Level}] [{item.SourceId}] {item.Message}{repeatSuffix}";
    }

    private bool SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
