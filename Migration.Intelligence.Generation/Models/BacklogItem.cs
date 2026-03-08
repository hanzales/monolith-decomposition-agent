namespace Migration.Intelligence.Generation.Models;

public sealed class BacklogItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Category { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> Dependencies { get; init; } = new();
    public List<string> ExitCriteria { get; init; } = new();
}
