namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class RepositoryTableMappingContract
{
    public required string RepositoryName { get; init; }
    public required string TableName { get; init; }
    public required string DomainCandidate { get; init; }
    public double Confidence { get; init; }
    public string AccessPattern { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
}
