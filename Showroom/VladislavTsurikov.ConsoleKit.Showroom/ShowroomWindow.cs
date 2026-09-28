using Avalonia.Controls;
using VladislavTsurikov.Abberia.Showroom.Catalog;
using VladislavTsurikov.Abberia.Showroom.Engine;

namespace VladislavTsurikov.ConsoleKit.Showroom;

public sealed class ShowroomWindow : Window
{
    private static readonly string[] s_ownAssemblies =
    [
        "VladislavTsurikov.ConsoleKit.Abberia",
        "VladislavTsurikov.ConsoleKit.Avalonia",
        "VladislavTsurikov.Abberia.Core",
        "VladislavTsurikov.Abberia.Controls",
    ];

    public ShowroomWindow()
    {
        Title = "ConsoleKit Showroom";
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;

        ShowroomRegistry registry = new(new[]
        {
            ConsoleShowroomCatalog.Instance,
            CoreShowroomCatalog.Instance,
        });

        ShowroomHostOptions options = new()
        {
            Title = "ConsoleKit Showroom",
            OwnAssemblies = s_ownAssemblies,
        };

        Content = new ShowroomShell(registry, options);
    }
}
