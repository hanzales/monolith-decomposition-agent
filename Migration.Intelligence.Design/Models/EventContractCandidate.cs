namespace Migration.Intelligence.Design.Models;

public sealed class EventContractCandidate
{
    public required string Name { get; init; }
    public required EventContractDirection Direction { get; init; }
    public string QueueOrTopic { get; init; } = string.Empty;
    public string RelatedDomain { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public double Confidence { get; set; }
    public string Notes { get; init; } = string.Empty;
}
