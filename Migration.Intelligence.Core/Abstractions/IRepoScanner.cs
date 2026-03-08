using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface IRepoScanner
{
    Task<RepositoryInventoryContract> ScanAsync(AnalysisOptions options, CancellationToken cancellationToken = default);
}
