namespace Migration.Intelligence.Contracts.Domain;

public sealed class DomainClusterContract
{
    public required string ClusterName { get; init; }
    public List<DomainCandidateContract> Candidates { get; init; } = new();
}
