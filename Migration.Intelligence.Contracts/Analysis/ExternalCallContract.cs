namespace Migration.Intelligence.Contracts.Analysis;

public sealed class ExternalCallContract
{
    public required string CallerType { get; init; }
    public required string DependencyName { get; init; }
    public string Protocol { get; init; } = string.Empty;
}
