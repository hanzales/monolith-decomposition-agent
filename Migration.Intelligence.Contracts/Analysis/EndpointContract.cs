using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.Contracts.Analysis;

public sealed class EndpointContract
{
    public required string ControllerName { get; init; }
    public required string ActionName { get; init; }
    public required string Route { get; init; }
    public EndpointHttpMethod HttpMethod { get; init; } = EndpointHttpMethod.Unknown;
}
