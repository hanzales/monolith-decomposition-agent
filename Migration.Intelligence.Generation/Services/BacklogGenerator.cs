using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Abstractions;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Services;

public sealed class BacklogGenerator : IBacklogGenerator
{
    public IReadOnlyList<BacklogItem> Generate(DomainMigrationDesign design, ValidationReport? validationReport = null)
    {
        ArgumentNullException.ThrowIfNull(design);
        var domain = design.SelectedDomain;
        var items = new List<BacklogItem>();
        var index = 1;

        foreach (var phase in design.StranglerMigrationPlan.Phases.OrderBy(phase => phase.PhaseOrder))
        {
            items.Add(new BacklogItem
            {
                Id = $"{ToKey(domain)}-PH{phase.PhaseOrder:D2}",
                Title = phase.Name,
                Category = "phase",
                Priority = index++,
                Description = phase.Objective,
                Dependencies = phase.PhaseOrder == 1
                    ? new List<string>()
                    : new List<string> { $"{ToKey(domain)}-PH{phase.PhaseOrder - 1:D2}" },
                ExitCriteria = phase.ExitCriteria.ToList()
            });
        }

        var blockerIndex = 1;
        foreach (var blocker in design.Blockers)
        {
            items.Add(new BacklogItem
            {
                Id = $"{ToKey(domain)}-BL{blockerIndex:D2}",
                Title = $"Resolve blocker {blockerIndex}",
                Category = "blocker",
                Priority = 1,
                Description = blocker,
                Dependencies = new List<string> { $"{ToKey(domain)}-PH01" },
                ExitCriteria = { "Blocker is mitigated or accepted with rollback plan." }
            });
            blockerIndex++;
        }

        if (validationReport is not null)
        {
            var validationIndex = 1;
            foreach (var issue in validationReport.Issues.Where(issue => issue.Severity != ValidationSeverity.Info))
            {
                items.Add(new BacklogItem
                {
                    Id = $"{ToKey(domain)}-VL{validationIndex:D2}",
                    Title = $"Fix validation issue {issue.Code}",
                    Category = "validation",
                    Priority = issue.Severity == ValidationSeverity.Error ? 1 : 2,
                    Description = issue.Message,
                    Dependencies = { $"{ToKey(domain)}-PH01" },
                    ExitCriteria = { $"Validation issue {issue.Code} is closed." }
                });
                validationIndex++;
            }
        }

        return items
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ToKey(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }
}
