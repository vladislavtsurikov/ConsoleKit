using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VladislavTsurikov.Abberia.Core.Metrics;
using VladislavTsurikov.Abberia.Theming.Engine;
using VladislavTsurikov.ThemeKit.Schema;

namespace VladislavTsurikov.ConsoleKit.Showroom;

public sealed class App : Application
{
    public static ThemeManager ThemeManager { get; } = ThemeManager.CreateDefault();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AbberiaMarker.Load();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeManager.ApplyBuiltIn(BuiltInThemeDocuments.AbberiaUIDarkThemeName);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ShowroomWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
