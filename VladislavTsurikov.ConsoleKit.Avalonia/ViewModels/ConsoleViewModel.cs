using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly LogSeverityCounter _severityCounter = new();
    private readonly ConsoleRefreshScheduler _refreshScheduler;
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
        _refreshScheduler = new ConsoleRefreshScheduler(
            settings.RefreshIntervalMilliseconds,
            Refresh);

        _store.EntryAppended += OnEntryAppended;
        _store.EntriesCleared += OnEntriesCleared;
        _sourceRegistry.SourcesChanged += OnSourcesChanged;
        RefreshSources();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<string>? CopyRequested;

    public ObservableCollection<LogEntryItemViewModel> Entries { get; }

    public ObservableCollection<LogSourceToggleViewModel> Sources { get; }

    public ICommand ClearCommand { get; }

    public ICommand CopyCommand { get; }

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
            Refresh();
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
            Refresh();
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
            Refresh();
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
            Refresh();
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
            Refresh();
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

    public string SelectedDetail => SelectedEntry?.Detail ?? string.Empty;

    public string GetVisibleText()
    {
        return string.Join(
            Environment.NewLine,
            Entries.Select(FormatEntry));
    }

    public void Refresh()
    {
        IReadOnlyList<LogEntry> snapshot = _store.Snapshot();
        InfoCount = _severityCounter.Count(snapshot, LogSeverity.Info);
        WarningCount = _severityCounter.Count(snapshot, LogSeverity.Warning);
        ErrorCount = _severityCounter.Count(snapshot, LogSeverity.Error);

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _store.EntryAppended -= OnEntryAppended;
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
        _refreshScheduler.RequestRefresh();
    }

    private void OnEntriesCleared()
    {
        _refreshScheduler.RequestRefresh();
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
        Refresh();
    }

    private static string FormatEntry(LogEntryItemViewModel item)
    {
        string repeatSuffix = item.RepeatCount > 1 ? $" x{item.RepeatCount}" : string.Empty;
        return $"[{item.TimestampText}] [{item.Entry.Severity}] [{item.SourceId}] {item.Message}{repeatSuffix}";
    }

    private void SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
