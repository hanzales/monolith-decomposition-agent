using Migration.Intelligence.Agents.Abstractions;

namespace Migration.Intelligence.Agents.Services;

/// <summary>
/// Composition entry point for migration planning agents.
/// </summary>
public static class AgentComposition
{
    public static IMigrationPlanningAgent CreateDefaultPlanningAgent()
    {
        var prioritizationAgent = new DomainPrioritizationAgent();
        var blockerAdvisor = new BlockerResolutionAdvisor();
        var deterministicReasoner = new DeterministicAgentReasoner();
        var llmReasoner = new OpenAiCompatibleAgentReasoner();
        return new MigrationPlanningAgent(
            prioritizationAgent,
            blockerAdvisor,
            deterministicReasoner,
            llmReasoner);
    }
}
