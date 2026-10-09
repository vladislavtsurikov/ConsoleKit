namespace VladislavTsurikov.ConsoleKit.Tests;

public sealed class ConsoleKitLayoutTests
{
    [Fact]
    public void RepositoryLayout_SeparatesWorkerAndDesktopUiProjects()
    {
        string repositoryRoot = FindRepositoryRoot();

        Assert.True(Directory.Exists(Path.Combine(repositoryRoot, "Worker", "VladislavTsurikov.ConsoleKit.Core")));
        Assert.True(Directory.Exists(Path.Combine(repositoryRoot, "Worker", "VladislavTsurikov.ConsoleKit.Serilog")));
        Assert.True(Directory.Exists(Path.Combine(repositoryRoot, "Worker", "VladislavTsurikov.ConsoleKit.ProcessLogReader")));
        Assert.True(Directory.Exists(Path.Combine(repositoryRoot, "DesktopUI", "VladislavTsurikov.ConsoleKit.Avalonia")));
        Assert.True(Directory.Exists(Path.Combine(repositoryRoot, "DesktopUI", "VladislavTsurikov.ConsoleKit.Abberia")));
    }

    [Fact]
    public void WorkerProjects_DoNotReferenceUiPackages()
    {
        string repositoryRoot = FindRepositoryRoot();
        string workerDirectory = Path.Combine(repositoryRoot, "Worker");
        string[] projectFiles = Directory.GetFiles(workerDirectory, "*.csproj", SearchOption.AllDirectories);

        foreach (string projectFile in projectFiles)
        {
            string projectContent = File.ReadAllText(projectFile);

            Assert.DoesNotContain("Avalonia", projectContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Abberia", projectContent, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string solutionPath = Path.Combine(directory.FullName, "ConsoleKit.slnx");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the ConsoleKit repository root.");
    }
}
