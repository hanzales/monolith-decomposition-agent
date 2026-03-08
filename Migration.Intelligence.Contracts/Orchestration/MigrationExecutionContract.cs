using Migration.Intelligence.Contracts.MigrationIntelligence;

namespace Migration.Intelligence.Contracts.Orchestration;

public sealed class MigrationExecutionContract
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public List<string> ArchitectureDocs { get; init; } = new();
    public required RepositoryInventoryContract Inventory { get; init; }
    public required CodeInsightsContract Insights { get; init; }
    public required MigrationIntelligenceContract Intelligence { get; init; }
    public List<ServiceBlueprintContract> ServiceBlueprints { get; init; } = new();
    public bool DryRun { get; init; }
    public DateTimeOffset ExecutedAtUtc { get; init; }
}
