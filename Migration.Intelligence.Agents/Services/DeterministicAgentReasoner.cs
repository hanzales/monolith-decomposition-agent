using Migration.Intelligence.Agents.Abstractions;
using Migration.Intelligence.Agents.Models;

namespace Migration.Intelligence.Agents.Services;

/// <summary>
/// Baseline deterministic reasoner with no external calls.
/// </summary>
public sealed class DeterministicAgentReasoner : IAgentReasoner
{
    public Task<AgentReasoningResult> ReasonAsync(
        AgentReasoningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var summary = $"Deterministic planning used for {request.BaseRecommendations.Count} domain recommendation(s).";
        return Task.FromResult(new AgentReasoningResult
        {
            Mode = AgentMode.Deterministic,
            IsSuccessful = true,
            Summary = summary,
            DomainAdvice = new List<DomainReasoningAdvice>()
        });
    }
}
