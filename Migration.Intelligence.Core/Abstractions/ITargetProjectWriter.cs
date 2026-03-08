using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Abstractions;

public interface ITargetProjectWriter
{
    Task WriteAsync(
        List<ServiceBlueprintContract> serviceBlueprints,
        AnalysisOptions options,
        CancellationToken cancellationToken = default);
}
