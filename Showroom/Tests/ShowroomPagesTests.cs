using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using VladislavTsurikov.Abberia.Showroom.Engine;
using VladislavTsurikov.ConsoleKit.Abberia;

namespace VladislavTsurikov.ConsoleKit.Showroom.Tests;

public sealed class ShowroomPagesTests
{
    private static readonly ShowroomRegistry s_registry = new([ConsoleShowroomCatalog.Instance]);

    [Fact]
    public void EachPublishedConsoleComponentHasExactlyOnePage()
    {
        Type[] components =
        [
            typeof(ConsoleToolbarView),
            typeof(ConsoleEntriesView),
            typeof(ConsoleDetailView),
            typeof(ConsolePanel),
        ];

        foreach (Type component in components)
        {
            Assert.Single(s_registry.Pages, page => page.TargetType == component);
        }

        Assert.Equal(components.Length, s_registry.Pages.Count);
        Assert.Equal(s_registry.Pages.Count, s_registry.Pages.Select(page => page.Id).Distinct().Count());
    }

    [AvaloniaFact]
    public void EveryPageCreatesItsSandboxAndExamples()
    {
        foreach (ShowroomPage page in s_registry.Pages)
        {
            Control sandbox = page.CreateSandbox();
            Assert.IsAssignableFrom(page.TargetType, sandbox);

            foreach (ShowroomExample example in page.Examples)
            {
                Assert.NotNull(example.CreatePreview);
                Assert.IsAssignableFrom(page.TargetType, example.CreatePreview!());
            }
        }
    }
}
