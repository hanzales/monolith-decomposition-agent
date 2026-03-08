namespace Migration.Intelligence.Design.Models;

public sealed class IntegrationDependencyDefinition
{
    public required string Name { get; init; }
    public required string Direction { get; init; }
    public required string DependencyType { get; init; }
    public string RelatedDomain { get; init; } = string.Empty;
    public string DependencyKind { get; init; } = string.Empty;
    public int Intensity { get; init; }
    public double Confidence { get; init; }
    public string Notes { get; init; } = string.Empty;
}
