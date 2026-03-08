namespace Migration.Intelligence.Contracts.Orchestration;

public sealed class ServiceBlueprintContract
{
    public required string ServiceName { get; init; }
    public required string BoundedContext { get; init; }
    public int ConfidenceScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> SourceHints { get; init; } = new();
}
