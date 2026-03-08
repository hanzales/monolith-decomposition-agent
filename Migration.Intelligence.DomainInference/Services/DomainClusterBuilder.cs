using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.DomainInference.Models;

namespace Migration.Intelligence.DomainInference.Services;

public sealed class DomainClusterBuilder
{
    public List<DomainCluster> BuildClusters(IReadOnlyCollection<ServiceBlueprintContract> blueprints)
    {
        var clusterMap = new Dictionary<string, DomainCluster>(StringComparer.OrdinalIgnoreCase);

        foreach (var blueprint in blueprints)
        {
            var clusterName = DetermineClusterName(blueprint);
            if (!clusterMap.TryGetValue(clusterName, out var cluster))
            {
                cluster = new DomainCluster
                {
                    ClusterName = clusterName
                };

                clusterMap[clusterName] = cluster;
            }

            if (!cluster.ServiceNames.Contains(blueprint.ServiceName, StringComparer.OrdinalIgnoreCase))
            {
                cluster.ServiceNames.Add(blueprint.ServiceName);
            }

            foreach (var hint in blueprint.SourceHints)
            {
                if (!cluster.SharedHints.Contains(hint, StringComparer.OrdinalIgnoreCase))
                {
                    cluster.SharedHints.Add(hint);
                }
            }
        }

        return clusterMap.Values
            .OrderByDescending(cluster => cluster.ServiceNames.Count)
            .ThenBy(cluster => cluster.ClusterName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string DetermineClusterName(ServiceBlueprintContract blueprint)
    {
        var projectHint = blueprint.SourceHints
            .FirstOrDefault(hint => hint.StartsWith("project:", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(projectHint))
        {
            var value = projectHint["project:".Length..];
            var tokens = value.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length >= 2)
            {
                return tokens[0] + "." + tokens[1];
            }

            if (tokens.Length == 1)
            {
                return tokens[0];
            }
        }

        return blueprint.ServiceName;
    }
}
