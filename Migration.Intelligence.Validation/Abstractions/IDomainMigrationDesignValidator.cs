using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Abstractions;

/// <summary>
/// Validates a single domain migration design package.
/// </summary>
public interface IDomainMigrationDesignValidator
{
    ValidationReport Validate(DomainMigrationDesign design);
}
