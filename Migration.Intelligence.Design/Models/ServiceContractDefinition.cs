namespace Migration.Intelligence.Design.Models;

public sealed class ServiceContractDefinition
{
    public required string DomainCandidate { get; init; }
    public List<EndpointContractDefinition> PublicApis { get; init; } = new();
    public List<EndpointContractDefinition> AdminApis { get; init; } = new();
    public List<EndpointContractDefinition> InternalApis { get; init; } = new();
    public List<EventContractCandidate> EventContracts { get; init; } = new();
    public List<string> ContractNotes { get; init; } = new();
    public double ContractCompleteness { get; init; }
}
