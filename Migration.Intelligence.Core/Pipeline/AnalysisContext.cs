using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Pipeline;

public class AnalysisContext
{
    public required AnalysisOptions Options { get; init; }
    public RepositoryInventoryContract? Inventory { get; set; }
    public CodeInsightsContract? Insights { get; set; }
    public List<ServiceBlueprintContract> ServiceBlueprints { get; set; } = new();
    public MigrationIntelligenceContract? Intelligence { get; set; }
}
