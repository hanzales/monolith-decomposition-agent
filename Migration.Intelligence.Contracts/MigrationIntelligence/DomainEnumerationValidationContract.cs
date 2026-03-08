namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class DomainEnumerationValidationContract
{
    public int InferredRootDomainCount { get; init; }
    public int RenderedRootDomainCount { get; init; }
    public int DossierDomainCount { get; init; }
    public int EndpointClusterDomainCount { get; init; }
    public int DependencyDomainCount { get; init; }
    public List<string> MissingDomains { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public bool IsValid { get; init; }
}
