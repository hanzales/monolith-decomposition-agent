using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.Domain;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.DomainInference.Heuristics;
using Migration.Intelligence.DomainInference.Models;

namespace Migration.Intelligence.DomainInference.Services;

public sealed class MigrationScoringService
{
    private readonly NamingHeuristics _namingHeuristics;
    private readonly CohesionHeuristics _cohesionHeuristics;
    private readonly CouplingHeuristics _couplingHeuristics;
    private readonly DataOwnershipHeuristics _dataOwnershipHeuristics;

    public MigrationScoringService(
        NamingHeuristics namingHeuristics,
        CohesionHeuristics cohesionHeuristics,
        CouplingHeuristics couplingHeuristics,
        DataOwnershipHeuristics dataOwnershipHeuristics)
    {
        _namingHeuristics = namingHeuristics;
        _cohesionHeuristics = cohesionHeuristics;
        _couplingHeuristics = couplingHeuristics;
        _dataOwnershipHeuristics = dataOwnershipHeuristics;
    }

    public List<MigrationScoreCard> Score(
        IReadOnlyCollection<ServiceBlueprintContract> blueprints,
        IReadOnlyCollection<DomainCluster> clusters,
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights)
    {
        var clusterByService = clusters
            .SelectMany(cluster => cluster.ServiceNames.Select(serviceName => (cluster, serviceName)))
            .GroupBy(item => item.serviceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.cluster.ServiceNames.Count)
                    .ThenBy(item => item.cluster.ClusterName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.cluster)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var serviceSignals = BuildServiceSignals(blueprints, inventory, insights);

        var scoreCards = new List<MigrationScoreCard>();

        foreach (var blueprint in blueprints)
        {
            var namingScore = _namingHeuristics.CalculateScore(blueprint.ServiceName);
            var cohesionScore = _cohesionHeuristics.CalculateScore(blueprint.SourceHints.Count);

            var relatedServiceCount = 0;
            if (clusterByService.TryGetValue(blueprint.ServiceName, out var cluster))
            {
                relatedServiceCount = Math.Max(0, cluster.ServiceNames.Count - 1);
            }

            var couplingScore = _couplingHeuristics.CalculateScore(relatedServiceCount, blueprints.Count);
            var dataOwnershipScore = _dataOwnershipHeuristics.CalculateScore(blueprint.SourceHints);

            var signal = serviceSignals.GetValueOrDefault(blueprint.ServiceName, ServiceSignal.Empty);
            var dependencyHealthScore = CalculateDependencyHealth(signal.EstimatedDependencyCount, signal.FileCount);
            var legacyRiskScore = CalculateLegacyRiskScore(signal.EstimatedLegacyRiskCount, signal.FileCount, signal.TopRiskHitCount);

            var overallScore = (int)Math.Round(
                (namingScore * 0.12)
                + (cohesionScore * 0.14)
                + (couplingScore * 0.14)
                + (dataOwnershipScore * 0.14)
                + (dependencyHealthScore * 0.23)
                + (legacyRiskScore * 0.23),
                MidpointRounding.AwayFromZero);

            scoreCards.Add(new MigrationScoreCard
            {
                ServiceName = blueprint.ServiceName,
                NamingScore = namingScore,
                CohesionScore = cohesionScore,
                CouplingScore = couplingScore,
                DataOwnershipScore = dataOwnershipScore,
                DependencyHealthScore = dependencyHealthScore,
                LegacyRiskScore = legacyRiskScore,
                EstimatedDependencyCount = signal.EstimatedDependencyCount,
                EstimatedLegacyRiskCount = signal.EstimatedLegacyRiskCount,
                OverallScore = overallScore,
                RiskLevel = ToRiskLevel(overallScore)
            });
        }

        return scoreCards
            .OrderByDescending(scoreCard => scoreCard.OverallScore)
            .ThenBy(scoreCard => scoreCard.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<MigrationScoreContract> ToContracts(IEnumerable<MigrationScoreCard> scoreCards)
    {
        return scoreCards.Select(scoreCard => new MigrationScoreContract
        {
            ServiceName = scoreCard.ServiceName,
            NamingScore = scoreCard.NamingScore,
            CohesionScore = scoreCard.CohesionScore,
            CouplingScore = scoreCard.CouplingScore,
            DataOwnershipScore = scoreCard.DataOwnershipScore,
            DependencyHealthScore = scoreCard.DependencyHealthScore,
            LegacyRiskScore = scoreCard.LegacyRiskScore,
            EstimatedDependencyCount = scoreCard.EstimatedDependencyCount,
            EstimatedLegacyRiskCount = scoreCard.EstimatedLegacyRiskCount,
            OverallScore = scoreCard.OverallScore,
            RiskLevel = scoreCard.RiskLevel
        }).ToList();
    }

    private static Dictionary<string, ServiceSignal> BuildServiceSignals(
        IReadOnlyCollection<ServiceBlueprintContract> blueprints,
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights)
    {
        var sourcePaths = inventory.SourceFiles
            .Where(file => file.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(file => NormalizePath(file.RelativePath))
            .ToList();

        var riskPaths = insights.TopRiskFiles
            .Select(NormalizePath)
            .ToList();

        var projectRootByName = inventory.Projects
            .GroupBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => NormalizePath(Path.GetDirectoryName(group.First().RelativePath) ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

        var dependencyDensity = insights.TotalSourceFileCount == 0
            ? 0.0
            : (double)insights.DependencyCount / insights.TotalSourceFileCount;

        var legacyDensity = insights.TotalSourceFileCount == 0
            ? 0.0
            : (double)insights.LegacyRiskCount / insights.TotalSourceFileCount;

        var map = new Dictionary<string, ServiceSignal>(StringComparer.OrdinalIgnoreCase);

        foreach (var blueprint in blueprints)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var hint in blueprint.SourceHints)
            {
                if (hint.StartsWith("project-path:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = hint["project-path:".Length..].Trim();
                    var root = NormalizePath(Path.GetDirectoryName(value) ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        roots.Add(root);
                    }

                    continue;
                }

                if (hint.StartsWith("project:", StringComparison.OrdinalIgnoreCase))
                {
                    var projectName = hint["project:".Length..].Trim();
                    if (projectRootByName.TryGetValue(projectName, out var root)
                        && !string.IsNullOrWhiteSpace(root))
                    {
                        roots.Add(root);
                    }

                    continue;
                }

                if (hint.StartsWith("source-folder:", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = NormalizePath(hint["source-folder:".Length..].Trim());
                    if (!string.IsNullOrWhiteSpace(folder))
                    {
                        folders.Add(folder);
                    }
                }
            }

            if (roots.Count == 0 && folders.Count == 0)
            {
                var fallbackFolder = NormalizePath(blueprint.ServiceName);
                if (!string.IsNullOrWhiteSpace(fallbackFolder))
                {
                    folders.Add(fallbackFolder);
                }
            }

            var fileCount = sourcePaths.Count(path => IsPathMatched(path, roots, folders));
            var topRiskHitCount = riskPaths.Count(path => IsPathMatched(path, roots, folders));

            var estimatedDependencies = (int)Math.Round(dependencyDensity * fileCount, MidpointRounding.AwayFromZero);
            var estimatedLegacyRisks = (int)Math.Round(legacyDensity * fileCount, MidpointRounding.AwayFromZero)
                                       + (topRiskHitCount * 2);

            map[blueprint.ServiceName] = new ServiceSignal(
                fileCount,
                Math.Max(0, estimatedDependencies),
                Math.Max(0, estimatedLegacyRisks),
                topRiskHitCount);
        }

        return map;
    }

    private static bool IsPathMatched(string path, IEnumerable<string> roots, IEnumerable<string> folders)
    {
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (path.Equals(folder, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0 && segments[0].Equals(folder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CalculateDependencyHealth(int estimatedDependencyCount, int fileCount)
    {
        if (fileCount <= 0)
        {
            return 50;
        }

        var ratio = (double)estimatedDependencyCount / fileCount;
        var score = 100 - (ratio * 12);
        return Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), 10, 100);
    }

    private static int CalculateLegacyRiskScore(int estimatedLegacyRiskCount, int fileCount, int topRiskHitCount)
    {
        if (fileCount <= 0)
        {
            return 60;
        }

        var ratio = (double)estimatedLegacyRiskCount / fileCount;
        var score = 100 - (ratio * 230) - (topRiskHitCount * 2);

        return Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), 5, 100);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    private static RiskLevel ToRiskLevel(int overallScore)
    {
        if (overallScore >= 80)
        {
            return RiskLevel.Low;
        }

        if (overallScore >= 60)
        {
            return RiskLevel.Medium;
        }

        if (overallScore >= 40)
        {
            return RiskLevel.High;
        }

        return RiskLevel.Critical;
    }

    private readonly record struct ServiceSignal(
        int FileCount,
        int EstimatedDependencyCount,
        int EstimatedLegacyRiskCount,
        int TopRiskHitCount)
    {
        public static ServiceSignal Empty => new(0, 0, 0, 0);
    }
}
