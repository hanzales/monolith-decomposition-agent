using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Abstractions;

/// <summary>
/// Validates Phase 1 migration intelligence output quality.
/// </summary>
public interface IMigrationIntelligenceValidator
{
    ValidationReport Validate(MigrationIntelligenceContract intelligence);
}
