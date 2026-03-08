using Migration.Intelligence.CodeAnalysis.Heuristics;
using Migration.Intelligence.CodeAnalysis.Services;
using Migration.Intelligence.CodeAnalysis.Visitors;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class SolutionCodeAnalyzer : ICodeAnalyzer
{
    private readonly ControllerHeuristics _controllerHeuristics = new();
    private readonly RepositoryHeuristics _repositoryHeuristics = new();
    private readonly EndpointAnalyzer _endpointAnalyzer;
    private readonly DependencyAnalyzer _dependencyAnalyzer;
    private readonly DataAccessAnalyzer _dataAccessAnalyzer;
    private readonly LegacyRiskAnalyzer _legacyRiskAnalyzer;

    public SolutionCodeAnalyzer()
    {
        var controllerVisitor = new ControllerVisitor(_controllerHeuristics);
        var dependencyVisitor = new DependencyVisitor();
        var sqlUsageVisitor = new SqlUsageVisitor();

        _endpointAnalyzer = new EndpointAnalyzer(controllerVisitor, _controllerHeuristics);
        _dependencyAnalyzer = new DependencyAnalyzer(dependencyVisitor);
        _dataAccessAnalyzer = new DataAccessAnalyzer(sqlUsageVisitor);
        _legacyRiskAnalyzer = new LegacyRiskAnalyzer(new LegacyFrameworkHeuristics());
    }

    public async Task<CodeInsightsContract> AnalyzeAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var sourceCodeFiles = inventory.SourceFiles
            .Where(file => file.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var controllerCount = sourceCodeFiles.Count(file =>
            _controllerHeuristics.IsControllerFile(file.RelativePath));

        var repositoryCount = sourceCodeFiles.Count(file =>
            _repositoryHeuristics.IsRepositoryFile(file.RelativePath));

        var topFolders = sourceCodeFiles
            .Select(file => ExtractMeaningfulTopFolder(file.RelativePath))
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Select(folder => folder!)
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Take(15)
            .Select(group => group.Key)
            .ToList();

        var endpointContracts = await _endpointAnalyzer.AnalyzeEndpointsAsync(inventory, cancellationToken);
        var dependencyContracts = await _dependencyAnalyzer.AnalyzeDependenciesAsync(inventory, options, cancellationToken);
        var tableUsages = await _dataAccessAnalyzer.AnalyzeTableUsageAsync(inventory, options, cancellationToken);
        var legacyRisks = await _legacyRiskAnalyzer.AnalyzeAsync(inventory, options, cancellationToken);

        var topRiskFiles = legacyRisks
            .GroupBy(risk => risk.RelativePath, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .Select(group => group.Key)
            .ToList();

        var insights = new CodeInsightsContract
        {
            TotalSourceFileCount = sourceCodeFiles.Count,
            ControllerLikeFileCount = controllerCount,
            RepositoryLikeFileCount = repositoryCount,
            EndpointCount = endpointContracts.Count,
            DependencyCount = dependencyContracts.Count,
            TableUsageCount = tableUsages.Count,
            ExternalCallCount = dependencyContracts.Count(x => x.IsExternalDependency),
            LegacyRiskCount = legacyRisks.Count,
            TopLevelSourceFolders = topFolders,
            TopRiskFiles = topRiskFiles
        };

        return insights;
    }

    private static string? ExtractMeaningfulTopFolder(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        // Last segment is the file name.
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var segment = parts[i];
            if (IsNoiseSegment(segment))
            {
                continue;
            }

            return segment;
        }

        return null;
    }

    private static bool IsNoiseSegment(string segment)
    {
        if (segment.Equals("src", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("source", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("packages", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("package", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (segment.Length >= 2 && (segment[0] == 'v' || segment[0] == 'V'))
        {
            return segment[1..].All(char.IsDigit);
        }

        return false;
    }
}
