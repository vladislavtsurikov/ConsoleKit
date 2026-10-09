using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;

namespace VladislavTsurikov.ConsoleKit.Avalonia;

public partial class ConsoleView : UserControl
{
    private const double BottomTolerance = 4d;

    private ConsoleViewModel? _viewModel;
    private bool _isAutoScrollEnabled = true;

    public ConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        LogEntriesListBox.PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged -= OnEntriesChanged;
        }

        _viewModel = DataContext as ConsoleViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Entries.CollectionChanged += OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (!_isAutoScrollEnabled || _viewModel is null)
        {
            return;
        }

        LogEntryItemViewModel? lastItem = _viewModel.Entries.LastOrDefault();
        if (lastItem is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => LogEntriesListBox.ScrollIntoView(lastItem));
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs eventArgs)
    {
        if (eventArgs.Delta.Y > 0)
        {
            _isAutoScrollEnabled = false;
            return;
        }

        Dispatcher.UIThread.Post(UpdateAutoScrollFromPosition);
    }

    private void UpdateAutoScrollFromPosition()
    {
        ScrollViewer? scrollViewer = LogEntriesListBox
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();
        if (scrollViewer is null)
        {
            return;
        }

        double remainingDistance =
            scrollViewer.Extent.Height -
            scrollViewer.Viewport.Height -
            scrollViewer.Offset.Y;
        _isAutoScrollEnabled = remainingDistance <= BottomTolerance;
    }
}
