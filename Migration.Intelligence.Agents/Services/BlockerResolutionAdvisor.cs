using Migration.Intelligence.Agents.Abstractions;
using Migration.Intelligence.Agents.Models;

namespace Migration.Intelligence.Agents.Services;

public sealed class BlockerResolutionAdvisor : IBlockerResolutionAdvisor
{
    public IReadOnlyList<AgentActionItem> BuildActions(AgentRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        var actions = new List<AgentActionItem>();
        foreach (var blocker in recommendation.Blockers)
        {
            actions.AddRange(BuildActionsForBlocker(blocker));
        }

        if (actions.Count == 0)
        {
            actions.Add(new AgentActionItem
            {
                Title = "Run extraction spike",
                Category = "discovery",
                Description = "Execute a small extraction spike to validate contracts and deployment flow.",
                Priority = 3
            });
        }

        return actions
            .GroupBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(action => action.Priority)
                .First())
            .OrderBy(action => action.Priority)
            .ThenBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<AgentActionItem> BuildActionsForBlocker(string blocker)
    {
        if (blocker.Contains("Shared table", StringComparison.OrdinalIgnoreCase))
        {
            yield return new AgentActionItem
            {
                Title = "Define table ownership split",
                Category = "data",
                Description = "Assign table owners and define phased access migration with compatibility views/APIs.",
                Priority = 1
            };
        }

        if (blocker.Contains("execution chain", StringComparison.OrdinalIgnoreCase))
        {
            yield return new AgentActionItem
            {
                Title = "Close unknown chain gaps",
                Category = "analysis",
                Description = "Trace unresolved controller-service-repository paths and add deterministic mappings.",
                Priority = 1
            };
        }

        if (blocker.Contains("Anti-corruption", StringComparison.OrdinalIgnoreCase)
            || blocker.Contains("legacy", StringComparison.OrdinalIgnoreCase))
        {
            yield return new AgentActionItem
            {
                Title = "Implement anti-corruption adapter",
                Category = "integration",
                Description = "Wrap legacy interfaces and shared dependencies behind stable service contracts.",
                Priority = 1
            };
        }

        if (blocker.Contains("coupling", StringComparison.OrdinalIgnoreCase))
        {
            yield return new AgentActionItem
            {
                Title = "Reduce coupling before cutover",
                Category = "architecture",
                Description = "Break direct dependencies through APIs/events and remove shared runtime assumptions.",
                Priority = 2
            };
        }
    }
}
