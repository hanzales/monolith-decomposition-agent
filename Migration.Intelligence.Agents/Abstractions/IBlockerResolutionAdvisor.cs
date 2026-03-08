using Migration.Intelligence.Agents.Models;

namespace Migration.Intelligence.Agents.Abstractions;

/// <summary>
/// Converts blocker signals into deterministic remediation action items.
/// </summary>
public interface IBlockerResolutionAdvisor
{
    IReadOnlyList<AgentActionItem> BuildActions(AgentRecommendation recommendation);
}
