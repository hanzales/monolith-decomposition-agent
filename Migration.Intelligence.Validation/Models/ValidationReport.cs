namespace Migration.Intelligence.Validation.Models;

public sealed class ValidationReport
{
    public required string Scope { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public int QualityScore { get; init; }
    public List<ValidationIssue> Issues { get; init; } = new();
    public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);
    public bool HasWarnings => Issues.Any(issue => issue.Severity == ValidationSeverity.Warning);
}
