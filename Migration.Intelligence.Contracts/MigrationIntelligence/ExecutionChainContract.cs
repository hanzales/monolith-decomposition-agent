namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class ExecutionChainContract
{
    public required string DomainCandidate { get; init; }
    public required string Controller { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
}
