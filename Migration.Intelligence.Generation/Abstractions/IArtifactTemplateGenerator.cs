using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Abstractions;

/// <summary>
/// Generates markdown/json artifacts from a domain migration design.
/// </summary>
public interface IArtifactTemplateGenerator
{
    DomainGenerationPackage Generate(DomainMigrationDesign design, ValidationReport? validationReport = null);
}
