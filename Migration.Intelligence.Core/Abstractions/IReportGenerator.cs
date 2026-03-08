using Migration.Intelligence.Contracts.Reporting;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface IReportGenerator
{
    Task<AnalysisReportContract> GenerateAsync(
        MigrationExecutionContract execution,
        AnalysisOptions options,
        CancellationToken cancellationToken = default);
}
