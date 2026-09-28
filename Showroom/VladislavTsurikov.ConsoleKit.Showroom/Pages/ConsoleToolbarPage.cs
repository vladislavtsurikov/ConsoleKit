using Avalonia.Controls;
using VladislavTsurikov.Abberia.Showroom.Engine;
using VladislavTsurikov.ConsoleKit.Abberia;

namespace VladislavTsurikov.ConsoleKit.Showroom.Pages;

[ShowroomPage(id: "consolekit-toolbar", path: "ConsoleKit/Toolbar", order: 100)]
public sealed class ConsoleToolbarPage : ShowroomPage
{
    public override string Title => "ConsoleToolbarView";

    public override Type TargetType => typeof(ConsoleToolbarView);

    public override string Description => "Clear, collapse, search, sources and severity filters.";

    public override string Keywords => "console, toolbar, search, severity, filter";

    public override Control CreateSandbox() => CreatePreview();

    public override IReadOnlyList<ShowroomExample> Examples =>
    [
        new ShowroomExample("With sample logs", "consolekit-toolbar-sample",
            "Try the search field, severity toggles and collapse action.", CreatePreview),
    ];

    private static Control CreatePreview()
    {
        return new ConsoleToolbarView
        {
            DataContext = ConsoleSamples.CreateViewModel(),
            MinWidth = 650,
        };
    }
}
