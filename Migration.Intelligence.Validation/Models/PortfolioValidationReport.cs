namespace Migration.Intelligence.Validation.Models;

public sealed class PortfolioValidationReport
{
    public required ValidationReport IntelligenceReport { get; init; }
    public List<ValidationReport> DomainReports { get; init; } = new();
    public int OverallScore { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool HasErrors => IntelligenceReport.HasErrors || DomainReports.Any(report => report.HasErrors);
}
