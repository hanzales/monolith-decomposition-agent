namespace Migration.Intelligence.Agents.Models;

public sealed class AgentPlanningOptions
{
    public AgentMode Mode { get; init; } = AgentMode.Deterministic;
    public LlmAgentOptions Llm { get; init; } = new();
}
