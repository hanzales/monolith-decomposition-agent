namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class ExternalDependencyMapContract
{
    public required string DomainCandidate { get; init; }
    public List<string> HttpClients { get; init; } = new();
    public List<string> ThirdPartyIntegrations { get; init; } = new();
    public List<string> ExternalApis { get; init; } = new();
    public List<string> QueuesOrEvents { get; init; } = new();
    public List<string> InternalServiceCalls { get; init; } = new();
}
