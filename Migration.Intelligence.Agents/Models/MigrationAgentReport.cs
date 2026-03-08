namespace Migration.Intelligence.Agents.Models;

public sealed class MigrationAgentReport
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public int OverallConfidenceScore { get; init; }
    public AgentMode Mode { get; init; } = AgentMode.Deterministic;
    public bool AiReasoningApplied { get; init; }
    public string AiSummary { get; init; } = string.Empty;
    public List<AgentRecommendation> Recommendations { get; init; } = new();
    public List<string> GlobalNotes { get; init; } = new();
}
