using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface IMigrationIntelligenceAnalyzer
{
    Task<MigrationIntelligenceContract> AnalyzeAsync(
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights,
        IReadOnlyCollection<ServiceBlueprintContract> serviceBlueprints,
        AnalysisOptions options,
        CancellationToken cancellationToken = default);
}
