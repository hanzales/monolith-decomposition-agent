namespace Migration.Intelligence.Design.Models;

public sealed class DomainMigrationDesign
{
    public required string SelectedDomain { get; init; }
    public required ServiceBlueprint ServiceBlueprint { get; init; }
    public required ServiceBoundaryDefinition ServiceBoundary { get; init; }
    public required ServiceContractDefinition ServiceContract { get; init; }
    public required DataOwnershipPlan DataOwnershipPlan { get; init; }
    public required IntegrationBoundaryPlan IntegrationBoundaryPlan { get; init; }
    public required StranglerMigrationPlan StranglerMigrationPlan { get; init; }
    public List<string> Blockers { get; init; } = new();
    public List<string> ReadinessNotes { get; init; } = new();
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
