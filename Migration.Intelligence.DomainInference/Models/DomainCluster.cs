namespace Migration.Intelligence.DomainInference.Models;

public sealed class DomainCluster
{
    public required string ClusterName { get; init; }
    public List<string> ServiceNames { get; init; } = new();
    public List<string> SharedHints { get; init; } = new();
}
