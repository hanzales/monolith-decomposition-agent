using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Validation.Abstractions;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Services;

public sealed class MigrationIntelligenceValidator : IMigrationIntelligenceValidator
{
    private static readonly HashSet<string> InvalidTableTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "join", "where", "set", "dbo", "a", "b", "c", "p", "r", "x", "v", "cte", "ct"
    };

    public ValidationReport Validate(MigrationIntelligenceContract intelligence)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        var issues = new List<ValidationIssue>();

        if (intelligence.BusinessDomainCandidates.Count == 0)
        {
            issues.Add(Error("MI001", "No business domain candidates detected.", "Intelligence"));
        }

        if (!intelligence.DomainEnumerationValidation.IsValid)
        {
            var evidence = intelligence.DomainEnumerationValidation.MissingDomains.Count > 0
                ? intelligence.DomainEnumerationValidation.MissingDomains
                : intelligence.DomainEnumerationValidation.Warnings;
            issues.Add(Error(
                "MI002",
                "Domain enumeration validation failed; inferred and rendered domain sets diverge.",
                "DomainEnumeration",
                evidence));
        }

        var invalidTableMappings = intelligence.RepositoryTableMappings
            .Where(mapping => IsInvalidTableSignal(mapping.TableName))
            .Select(mapping => $"{mapping.RepositoryName}->{mapping.TableName}")
            .Take(25)
            .ToList();
        if (invalidTableMappings.Count > 0)
        {
            issues.Add(Warning(
                "MI003",
                "Repository-to-table mapping contains SQL artifacts or alias-like tokens.",
                "RepositoryTableMapping",
                invalidTableMappings));
        }

        var uncoveredDomains = intelligence.BusinessDomainCandidates
            .Where(domain => intelligence.ServiceDossiers.All(dossier =>
                !dossier.CandidateName.Equals(domain, StringComparison.OrdinalIgnoreCase)))
            .Take(25)
            .ToList();
        if (uncoveredDomains.Count > 0)
        {
            issues.Add(Warning(
                "MI004",
                "Some inferred domains do not have service dossiers.",
                "ServiceDossiers",
                uncoveredDomains));
        }

        var scheduledWithoutResolution = intelligence.HangfireJobs
            .Where(job => job.Category == HangfireJobCategory.ScheduledJob
                          && job.ScheduleResolutionStatus is HangfireScheduleResolutionStatus.Unresolved
                              or HangfireScheduleResolutionStatus.Partial)
            .Select(job => $"{job.JobName}:{job.RawScheduleKey}")
            .Take(25)
            .ToList();
        if (scheduledWithoutResolution.Count > 0)
        {
            issues.Add(Warning(
                "MI005",
                "Some scheduled Hangfire jobs do not have fully resolved schedule definitions.",
                "BackgroundJobs",
                scheduledWithoutResolution));
        }

        var dossierWithoutEndpoints = intelligence.ServiceDossiers
            .Where(dossier => dossier.RelatedEndpoints.Count == 0)
            .Select(dossier => dossier.CandidateName)
            .Take(25)
            .ToList();
        if (dossierWithoutEndpoints.Count > 0)
        {
            issues.Add(Info(
                "MI006",
                "Some service dossiers have no mapped endpoints.",
                "ServiceDossiers",
                dossierWithoutEndpoints));
        }

        var qualityScore = CalculateScore(issues);
        return new ValidationReport
        {
            Scope = "MigrationIntelligence",
            Issues = issues,
            QualityScore = qualityScore,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static bool IsInvalidTableSignal(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return true;
        }

        var trimmed = tableName.Trim();
        if (trimmed.Contains(' ') || trimmed.Contains('(') || trimmed.Contains(')'))
        {
            return true;
        }

        return InvalidTableTokens.Contains(trimmed);
    }

    private static int CalculateScore(IEnumerable<ValidationIssue> issues)
    {
        var score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                ValidationSeverity.Error => 20,
                ValidationSeverity.Warning => 8,
                _ => 2
            };
        }

        return Math.Max(0, score);
    }

    private static ValidationIssue Info(string code, string message, string scope, List<string>? evidence = null)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Info,
            Message = message,
            Scope = scope,
            Evidence = evidence ?? new List<string>()
        };
    }

    private static ValidationIssue Warning(string code, string message, string scope, List<string>? evidence = null)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Warning,
            Message = message,
            Scope = scope,
            Evidence = evidence ?? new List<string>()
        };
    }

    private static ValidationIssue Error(string code, string message, string scope, List<string>? evidence = null)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Error,
            Message = message,
            Scope = scope,
            Evidence = evidence ?? new List<string>()
        };
    }
}
