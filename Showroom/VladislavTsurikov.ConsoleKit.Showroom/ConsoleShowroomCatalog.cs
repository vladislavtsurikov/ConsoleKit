using VladislavTsurikov.Abberia.Showroom.Engine;

namespace VladislavTsurikov.ConsoleKit.Showroom;

public static class ConsoleShowroomCatalog
{
    public static ShowroomCatalog Instance { get; } = new(
        "consolekit",
        "ConsoleKit",
        typeof(ConsoleShowroomCatalog).Assembly,
        "CONSOLEKIT");
}
