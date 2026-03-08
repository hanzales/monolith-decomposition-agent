using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface IDomainInferenceEngine
{
    Task<List<ServiceBlueprintContract>> InferServicesAsync(
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights,
        AnalysisOptions options,
        CancellationToken cancellationToken = default);
}
