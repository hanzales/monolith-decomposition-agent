namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class DomainHierarchyContract
{
    public required string Domain { get; init; }
    public List<string> Subdomains { get; init; } = new();
}
