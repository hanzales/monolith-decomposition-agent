namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class SharedKernelItemContract
{
    public required string ComponentName { get; init; }
    public required string ComponentType { get; init; }
    public required string Recommendation { get; init; }
    public string Rationale { get; init; } = string.Empty;
}
