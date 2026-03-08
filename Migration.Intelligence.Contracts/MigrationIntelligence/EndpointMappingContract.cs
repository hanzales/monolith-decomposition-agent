using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class EndpointMappingContract
{
    public required string DomainCandidate { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required EndpointHttpMethod HttpMethod { get; init; }
    public required string RoutePrefix { get; init; }
    public required string Route { get; init; }
    public required EndpointExposure Exposure { get; init; }
    public double OwnershipConfidence { get; init; }
}
