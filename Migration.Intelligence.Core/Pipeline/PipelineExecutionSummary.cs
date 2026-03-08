namespace Migration.Intelligence.Core.Pipeline;

public class PipelineExecutionSummary
{
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public List<AnalysisStepResult> StepResults { get; init; } = new();

    public bool IsSuccess => StepResults.All(step => step.IsSuccess);
    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}
