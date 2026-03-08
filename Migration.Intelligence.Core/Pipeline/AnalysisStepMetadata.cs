namespace Migration.Intelligence.Core.Pipeline;

public class AnalysisStepMetadata
{
    public required string StepName { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }

    public TimeSpan Duration => (CompletedAtUtc ?? DateTimeOffset.UtcNow) - StartedAtUtc;
}
