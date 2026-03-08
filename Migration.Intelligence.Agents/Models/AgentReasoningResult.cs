namespace Migration.Intelligence.Agents.Models;

public sealed class AgentReasoningResult
{
    public AgentMode Mode { get; set; } = AgentMode.Deterministic;
    public bool IsSuccessful { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public List<DomainReasoningAdvice> DomainAdvice { get; set; } = new();
}
