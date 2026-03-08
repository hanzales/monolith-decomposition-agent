using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Agents.Models;

public sealed class DomainReasoningAdvice
{
    public required string Domain { get; init; }
    public int PriorityAdjustment { get; init; }
    public ExtractionStrategy? SuggestedStrategy { get; init; }
    public List<string> AdditionalReasons { get; init; } = new();
    public List<AgentActionItem> AdditionalActions { get; init; } = new();
}
