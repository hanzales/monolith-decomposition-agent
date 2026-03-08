using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.MigrationIntelligence;

namespace Migration.Intelligence.Design.Models;

public sealed class EndpointContractDefinition
{
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required EndpointHttpMethod HttpMethod { get; init; }
    public required string RoutePrefix { get; init; }
    public required string Route { get; init; }
    public required EndpointExposure Exposure { get; init; }
    public double OwnershipConfidence { get; init; }
}
