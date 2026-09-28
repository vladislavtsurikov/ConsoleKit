using Avalonia.Controls;
using VladislavTsurikov.Abberia.Showroom.Engine;
using VladislavTsurikov.ConsoleKit.Abberia;

namespace VladislavTsurikov.ConsoleKit.Showroom.Pages;

[ShowroomPage(id: "consolekit-entries", path: "ConsoleKit/Entries", order: 200)]
public sealed class ConsoleEntriesPage : ShowroomPage
{
    public override string Title => "ConsoleEntriesView";

    public override Type TargetType => typeof(ConsoleEntriesView);

    public override string Description => "Virtualized log list with selection, repeat markers and auto-scroll.";

    public override string Keywords => "console, log, list, selection, virtualized";

    public override Control CreateSandbox() => CreatePreview();

    public override IReadOnlyList<ShowroomExample> Examples =>
    [
        new ShowroomExample("Info, warning and error", "consolekit-entries-sample",
            "Select a message to inspect its detail in the full console.", CreatePreview),
    ];

    private static Control CreatePreview()
    {
        return new ConsoleEntriesView
        {
            DataContext = ConsoleSamples.CreateViewModel(),
            MinHeight = 250,
            MinWidth = 650,
        };
    }
}
