using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Agents.Models;

public sealed class AgentRecommendation
{
    public required string Domain { get; init; }
    public int Rank { get; set; }
    public int PriorityScore { get; set; }
    public ExtractionStrategy Strategy { get; set; }
    public string ReadinessLevel { get; init; } = "Unknown";
    public List<string> Reasons { get; init; } = new();
    public List<string> Blockers { get; init; } = new();
    public List<AgentActionItem> ActionItems { get; set; } = new();
}
