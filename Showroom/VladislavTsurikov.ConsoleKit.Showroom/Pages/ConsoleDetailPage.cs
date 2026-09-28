using Avalonia.Controls;
using VladislavTsurikov.Abberia.Showroom.Engine;
using VladislavTsurikov.ConsoleKit.Abberia;

namespace VladislavTsurikov.ConsoleKit.Showroom.Pages;

[ShowroomPage(id: "consolekit-detail", path: "ConsoleKit/Detail", order: 300)]
public sealed class ConsoleDetailPage : ShowroomPage
{
    public override string Title => "ConsoleDetailView";

    public override Type TargetType => typeof(ConsoleDetailView);

    public override string Description => "Scrollable details for the selected log entry.";

    public override string Keywords => "console, detail, selected entry";

    public override Control CreateSandbox() => CreatePreview();

    public override IReadOnlyList<ShowroomExample> Examples =>
    [
        new ShowroomExample("Selected log entry", "consolekit-detail-sample",
            "Displays the detail text for a selected message.", CreatePreview),
    ];

    private static Control CreatePreview()
    {
        return new ConsoleDetailView
        {
            DataContext = ConsoleSamples.CreateViewModel(),
            MinHeight = 180,
            MinWidth = 650,
        };
    }
}
