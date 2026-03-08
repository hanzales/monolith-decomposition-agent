namespace Migration.Intelligence.Design.Models;

public sealed class ExecutionChainDefinition
{
    public required string Controller { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Table { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
}
