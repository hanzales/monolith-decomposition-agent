using System.Text.RegularExpressions;
using Migration.Intelligence.Contracts.Discovery;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.Core.Utilities;

namespace Migration.Intelligence.Scanner.Services;

public sealed class SolutionDiscoveryService
{
    private static readonly Regex ProjectPathRegex =
        new("\"(?<path>[^\"]+\\.csproj)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public List<SolutionContract> DiscoverSolutions(
        string sourceRoot,
        IReadOnlyCollection<ProjectContract> projects,
        ScannerOptions options)
    {
        var projectLookup = projects.ToDictionary(
            project => NormalizePath(project.RelativePath),
            project => project,
            StringComparer.OrdinalIgnoreCase);

        var solutions = new List<SolutionContract>();

        foreach (var solutionPath in Directory.EnumerateFiles(sourceRoot, "*.sln", SearchOption.AllDirectories)
                     .Where(path => !PathUtility.IsExcludedPath(Path.GetRelativePath(sourceRoot, path), options.ExcludedDirectories)))
        {
            var mappedProjects = DiscoverProjectsForSolution(sourceRoot, solutionPath, projectLookup);

            solutions.Add(new SolutionContract
            {
                RelativePath = Path.GetRelativePath(sourceRoot, solutionPath),
                Projects = mappedProjects
            });
        }

        return solutions;
    }

    private static List<ProjectContract> DiscoverProjectsForSolution(
        string sourceRoot,
        string solutionPath,
        IReadOnlyDictionary<string, ProjectContract> projectLookup)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? sourceRoot;
        var content = File.ReadAllText(solutionPath);

        var projects = new List<ProjectContract>();

        foreach (Match match in ProjectPathRegex.Matches(content))
        {
            var relativeProjectPath = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
            var fullProjectPath = Path.GetFullPath(Path.Combine(solutionDirectory, relativeProjectPath));
            var normalizedProjectPath = NormalizePath(Path.GetRelativePath(sourceRoot, fullProjectPath));

            if (projectLookup.TryGetValue(normalizedProjectPath, out var project)
                && projects.All(existing => !existing.RelativePath.Equals(project.RelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                projects.Add(project);
            }
        }

        if (projects.Count > 0)
        {
            return projects;
        }

        // Fallback when solution parsing fails.
        return projectLookup.Values.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }
}
