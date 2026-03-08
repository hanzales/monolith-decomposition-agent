using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface ICodeAnalyzer
{
    Task<CodeInsightsContract> AnalyzeAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken = default);
}
