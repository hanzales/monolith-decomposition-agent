using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Abstractions;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Services;

public sealed class ValidationOrchestrator : IValidationOrchestrator
{
    private readonly IMigrationIntelligenceValidator _intelligenceValidator;
    private readonly IDomainMigrationDesignValidator _designValidator;

    public ValidationOrchestrator(
        IMigrationIntelligenceValidator intelligenceValidator,
        IDomainMigrationDesignValidator designValidator)
    {
        _intelligenceValidator = intelligenceValidator;
        _designValidator = designValidator;
    }

    public PortfolioValidationReport Validate(
        MigrationIntelligenceContract intelligence,
        IEnumerable<DomainMigrationDesign> designs)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(designs);

        var intelligenceReport = _intelligenceValidator.Validate(intelligence);
        var domainReports = designs
            .Select(_designValidator.Validate)
            .OrderBy(report => report.Scope, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var score = (int)Math.Round(
            domainReports.Count == 0
                ? intelligenceReport.QualityScore
                : (intelligenceReport.QualityScore + domainReports.Average(report => report.QualityScore)) / 2.0,
            MidpointRounding.AwayFromZero);

        return new PortfolioValidationReport
        {
            IntelligenceReport = intelligenceReport,
            DomainReports = domainReports,
            OverallScore = score,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
