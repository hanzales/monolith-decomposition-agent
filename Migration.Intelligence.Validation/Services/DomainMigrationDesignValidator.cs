using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Abstractions;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Services;

public sealed class DomainMigrationDesignValidator : IDomainMigrationDesignValidator
{
    public ValidationReport Validate(DomainMigrationDesign design)
    {
        ArgumentNullException.ThrowIfNull(design);
        var issues = new List<ValidationIssue>();
        var scope = $"DomainDesign:{design.SelectedDomain}";

        if (design.ServiceBoundary.BoundaryConfidence < 0.4)
        {
            issues.Add(Error("DD001", "Boundary confidence is critically low.", scope));
        }
        else if (design.ServiceBoundary.BoundaryConfidence < 0.6)
        {
            issues.Add(Warning("DD002", "Boundary confidence is low; ownership should be verified manually.", scope));
        }

        var endpointCount =
            design.ServiceContract.PublicApis.Count +
            design.ServiceContract.AdminApis.Count +
            design.ServiceContract.InternalApis.Count;
        if (endpointCount == 0)
        {
            issues.Add(Warning("DD003", "No API endpoints mapped to this domain design.", scope));
        }

        if (design.ServiceBoundary.Controllers.Count == 0
            && design.ServiceBoundary.Repositories.Count == 0
            && design.ServiceBoundary.Tables.Count == 0)
        {
            issues.Add(Error("DD004", "Design has no structural components (controllers/repositories/tables).", scope));
        }

        var dataCount = design.DataOwnershipPlan.OwnedTables.Count
                        + design.DataOwnershipPlan.SharedTables.Count
                        + design.DataOwnershipPlan.ReferencedTables.Count;
        if (dataCount == 0)
        {
            issues.Add(Warning("DD005", "No table ownership information found for this domain.", scope));
        }

        if (design.StranglerMigrationPlan.Phases.Count < 3)
        {
            issues.Add(Error("DD006", "Strangler plan has insufficient phase decomposition.", scope));
        }

        if (design.StranglerMigrationPlan.ExtractionStrategy == ExtractionStrategy.DirectExtraction
            && design.ServiceBoundary.BoundaryConfidence < 0.65)
        {
            issues.Add(Warning("DD007", "Direct extraction strategy may be optimistic given boundary confidence.", scope));
        }

        if (design.Blockers.Count == 0
            && design.ServiceBoundary.BoundaryWarnings.Count > 0)
        {
            issues.Add(Info("DD008", "Boundary warnings exist but blockers list is empty.", scope));
        }

        var score = CalculateScore(issues);
        return new ValidationReport
        {
            Scope = scope,
            Issues = issues,
            QualityScore = score,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static int CalculateScore(IEnumerable<ValidationIssue> issues)
    {
        var score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                ValidationSeverity.Error => 18,
                ValidationSeverity.Warning => 7,
                _ => 2
            };
        }

        return Math.Max(0, score);
    }

    private static ValidationIssue Info(string code, string message, string scope)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Info,
            Message = message,
            Scope = scope
        };
    }

    private static ValidationIssue Warning(string code, string message, string scope)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Warning,
            Message = message,
            Scope = scope
        };
    }

    private static ValidationIssue Error(string code, string message, string scope)
    {
        return new ValidationIssue
        {
            Code = code,
            Severity = ValidationSeverity.Error,
            Message = message,
            Scope = scope
        };
    }
}
