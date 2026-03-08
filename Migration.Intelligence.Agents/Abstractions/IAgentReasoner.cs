using Migration.Intelligence.Agents.Models;

namespace Migration.Intelligence.Agents.Abstractions;

/// <summary>
/// Produces AI-assisted reasoning and recommendation refinement for migration planning.
/// </summary>
public interface IAgentReasoner
{
    Task<AgentReasoningResult> ReasonAsync(
        AgentReasoningRequest request,
        CancellationToken cancellationToken = default);
}
