namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class MigrationIntelligenceContract
{
    public DomainEnumerationValidationContract DomainEnumerationValidation { get; init; } = new();
    public List<ComponentClassificationContract> ComponentClassifications { get; init; } = new();
    public List<string> BusinessDomainCandidates { get; init; } = new();
    public List<DomainHierarchyContract> DomainHierarchies { get; init; } = new();
    public List<EndpointMappingContract> EndpointMappings { get; init; } = new();
    public List<ExecutionChainContract> ExecutionChains { get; init; } = new();
    public List<RepositoryTableMappingContract> RepositoryTableMappings { get; init; } = new();
    public List<TableOwnershipContract> TableOwnerships { get; init; } = new();
    public List<string> SharedTables { get; init; } = new();
    public List<string> OwnerlessOrAmbiguousTables { get; init; } = new();
    public List<DomainDependencyContract> DependencyMatrix { get; init; } = new();
    public List<ExternalDependencyMapContract> ExternalDependencyMaps { get; init; } = new();
    public List<WorkflowAnalysisContract> Workflows { get; init; } = new();
    public List<HangfireJobContract> HangfireJobs { get; init; } = new();
    public List<ProducerConsumerRelationshipContract> ProducerConsumerRelationships { get; init; } = new();
    public BackgroundJobValidationContract BackgroundJobValidation { get; init; } = new();
    public List<LegacyRiskDetailContract> LegacyRiskDetails { get; init; } = new();
    public List<ServiceDossierContract> ServiceDossiers { get; init; } = new();
    public List<MigrationOrderRecommendationContract> MigrationOrderRecommendations { get; init; } = new();
    public List<SharedKernelItemContract> SharedKernelItems { get; init; } = new();
}
