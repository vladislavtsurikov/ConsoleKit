using Avalonia.Controls;
using VladislavTsurikov.Abberia.Showroom.Engine;
using VladislavTsurikov.ConsoleKit.Abberia;

namespace VladislavTsurikov.ConsoleKit.Showroom.Pages;

[ShowroomPage(id: "consolekit-panel", path: "ConsoleKit/ConsolePanel", order: 400)]
public sealed class ConsolePanelPage : ShowroomPage
{
    public override string Title => "ConsolePanel";

    public override Type TargetType => typeof(ConsolePanel);

    public override string Description => "Combined Abberia toolbar, virtualized entries and detail pane.";

    public override string Keywords => "console, panel, toolbar, logs, details";

    public override Control CreateSandbox() => CreatePreview();

    public override IReadOnlyList<ShowroomExample> Examples =>
    [
        new ShowroomExample("Interactive console", "consolekit-panel-sample",
            "Filter, select and inspect sample messages without a running Worker.", CreatePreview),
    ];

    private static Control CreatePreview()
    {
        return new ConsolePanel
        {
            DataContext = ConsoleSamples.CreateViewModel(),
            MinHeight = 480,
            MinWidth = 700,
        };
    }
}
