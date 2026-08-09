using System.ComponentModel;
using System.Runtime.CompilerServices;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;

public sealed class LogSourceToggleViewModel : INotifyPropertyChanged
{
    private readonly LogSource _source;
    private bool _isEnabled;

    public LogSourceToggleViewModel(LogSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _isEnabled = source.IsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<LogSourceToggleViewModel>? EnabledChanged;

    public string Id => _source.Id;

    public string DisplayName => _source.DisplayName;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            if (value)
            {
                _source.Enable();
            }
            else
            {
                _source.Disable();
            }

            OnPropertyChanged();
            EnabledChanged?.Invoke(this);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
