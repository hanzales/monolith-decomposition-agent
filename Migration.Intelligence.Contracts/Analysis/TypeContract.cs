namespace Migration.Intelligence.Contracts.Analysis;

public sealed class TypeContract
{
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string RelativePath { get; init; }
    public List<ConstructorDependencyContract> ConstructorDependencies { get; init; } = new();
    public List<DependencyContract> Dependencies { get; init; } = new();
    public List<EndpointContract> Endpoints { get; init; } = new();
    public List<TableUsageContract> TableUsages { get; init; } = new();
    public List<ExternalCallContract> ExternalCalls { get; init; } = new();
    public List<LegacyRiskContract> LegacyRisks { get; init; } = new();
}
