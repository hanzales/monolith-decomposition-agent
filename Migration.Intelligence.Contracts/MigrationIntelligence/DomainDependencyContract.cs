namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class DomainDependencyContract
{
    public required string FromDomain { get; init; }
    public required string ToDomain { get; init; }
    public required string DependencyKind { get; init; }
    public int Intensity { get; init; }
}
