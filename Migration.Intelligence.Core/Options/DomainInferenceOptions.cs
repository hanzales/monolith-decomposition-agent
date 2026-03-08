namespace Migration.Intelligence.Core.Options;

public class DomainInferenceOptions
{
    public int MaxServiceCount { get; init; } = 20;
    public int MinimumConfidenceScore { get; init; } = 6;
    public bool EnableControllerSignal { get; init; } = true;
}
