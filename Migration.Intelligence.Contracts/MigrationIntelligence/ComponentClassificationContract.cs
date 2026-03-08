namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class ComponentClassificationContract
{
    public required string ComponentName { get; init; }
    public required ComponentCategory Category { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string ValidationStatus { get; init; } = string.Empty;
}
