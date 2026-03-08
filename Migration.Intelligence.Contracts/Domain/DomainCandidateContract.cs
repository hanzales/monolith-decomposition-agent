namespace Migration.Intelligence.Contracts.Domain;

public sealed class DomainCandidateContract
{
    public required string ServiceName { get; init; }
    public required string BoundedContext { get; init; }
    public int ConfidenceScore { get; init; }
    public List<string> SourceHints { get; init; } = new();
}
