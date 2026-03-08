using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Abstractions;

/// <summary>
/// Orchestrates artifact and backlog generation for a domain.
/// </summary>
public interface IDomainGenerationOrchestrator
{
    Task<GenerationWriteResult> GenerateAsync(
        DomainMigrationDesign design,
        string outputRoot,
        ValidationReport? validationReport = null,
        CancellationToken cancellationToken = default);
}
