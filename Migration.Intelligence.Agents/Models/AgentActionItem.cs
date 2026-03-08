namespace Migration.Intelligence.Agents.Models;

public sealed class AgentActionItem
{
    public required string Title { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Priority { get; init; }
}
