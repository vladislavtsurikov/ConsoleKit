using System.Text.RegularExpressions;

namespace VladislavTsurikov.ConsoleKit.Tests;

/// <summary>
/// Worker/ holds the headless journal: it must stay usable from a console app, a service or a Worker
/// process, so it references no UI framework and no process-runner library of a host.
/// </summary>
public sealed class DependencyBoundaryTests
{
    private static readonly string[] s_forbidden = ["Avalonia", "Abberia", "ProcessControl", "ThemeKit"];

    [Fact]
    public void Worker_projects_reference_no_ui_and_no_host_libraries()
    {
        string worker = Path.Combine(RepositoryRoot(), "Worker");
        string[] projects = Directory.GetFiles(worker, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(projects);

        List<string> violations = new();
        foreach (string project in projects)
        {
            string text = File.ReadAllText(project);
            foreach (Match reference in Regex.Matches(text, @"<(?:ProjectReference|PackageReference)\s+Include=""([^""]+)"""))
            {
                string include = reference.Groups[1].Value;
                if (s_forbidden.Any(name => include.Contains(name, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add($"{Path.GetFileName(project)} -> {include}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void The_repository_has_the_mcpfamily_layout()
    {
        string root = RepositoryRoot();
        foreach (string folder in new[] { "Worker", "DesktopUI", "Showroom", "tests", "docs", "scripts" })
        {
            Assert.True(Directory.Exists(Path.Combine(root, folder)), $"missing folder {folder}");
        }

        string[] strayProjects = Directory.GetDirectories(root, "VladislavTsurikov.ConsoleKit.*", SearchOption.TopDirectoryOnly);
        Assert.Empty(strayProjects);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ConsoleKit.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ConsoleKit.slnx was not found above the test output.");
    }
}
