using Migration.Intelligence.Core.Pipeline;

namespace Migration.Intelligence.Core.Abstractions;

public interface IAnalysisStep
{
    string StepName { get; }

    Task<AnalysisStepResult> ExecuteAsync(
        AnalysisContext context,
        CancellationToken cancellationToken = default);
}
