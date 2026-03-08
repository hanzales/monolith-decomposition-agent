namespace Migration.Intelligence.Validation.Models;

public sealed class ValidationIssue
{
    public required string Code { get; init; }
    public required ValidationSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string Scope { get; init; } = string.Empty;
    public List<string> Evidence { get; init; } = new();
}
