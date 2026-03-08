namespace Migration.Intelligence.Core.Pipeline;

public class AnalysisStepResult
{
    public required AnalysisStepMetadata Metadata { get; init; }
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
}
