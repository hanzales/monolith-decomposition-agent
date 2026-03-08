namespace Migration.Intelligence.Design.Models;

public sealed class IntegrationBoundaryPlan
{
    public required string DomainCandidate { get; init; }
    public List<IntegrationDependencyDefinition> InboundIntegrations { get; init; } = new();
    public List<IntegrationDependencyDefinition> OutboundIntegrations { get; init; } = new();
    public List<IntegrationDependencyDefinition> InternalServiceDependencies { get; init; } = new();
    public List<string> AntiCorruptionLayerNeeds { get; init; } = new();
    public List<string> IntegrationRisks { get; init; } = new();
    public bool NeedsAntiCorruptionLayer { get; init; }
    public string Summary { get; init; } = string.Empty;
}
