using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Abstractions;

/// <summary>
/// Converts migration design phases and blockers into backlog items.
/// </summary>
public interface IBacklogGenerator
{
    IReadOnlyList<BacklogItem> Generate(DomainMigrationDesign design, ValidationReport? validationReport = null);
}
