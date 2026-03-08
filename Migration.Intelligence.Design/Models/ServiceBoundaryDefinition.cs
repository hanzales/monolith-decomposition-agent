namespace Migration.Intelligence.Design.Models;

public sealed class ServiceBoundaryDefinition
{
    public required string DomainCandidate { get; init; }
    public List<string> Subdomains { get; init; } = new();
    public List<string> Controllers { get; init; } = new();
    public List<string> Services { get; init; } = new();
    public List<string> Repositories { get; init; } = new();
    public List<string> Entities { get; init; } = new();
    public List<string> Tables { get; init; } = new();
    public List<ExecutionChainDefinition> ExecutionChains { get; init; } = new();
    public List<string> InboundDependentDomains { get; init; } = new();
    public List<string> OutboundDependencies { get; init; } = new();
    public List<string> BoundaryWarnings { get; init; } = new();
    public double BoundaryConfidence { get; init; }
    public string BoundaryRationale { get; init; } = string.Empty;
}
