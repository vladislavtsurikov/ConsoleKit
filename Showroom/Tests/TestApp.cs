using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(VladislavTsurikov.ConsoleKit.Showroom.Tests.TestApp))]

namespace VladislavTsurikov.ConsoleKit.Showroom.Tests;

public sealed class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://VladislavTsurikov.ConsoleKit.Showroom/"))
        {
            Source = new Uri("avares://VladislavTsurikov.Abberia.DefaultSkin/Index.axaml"),
        });
    }
}
