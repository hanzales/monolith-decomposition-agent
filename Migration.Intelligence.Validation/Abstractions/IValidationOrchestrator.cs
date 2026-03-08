using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Validation.Abstractions;

/// <summary>
/// Validates intelligence and design outputs as a portfolio.
/// </summary>
public interface IValidationOrchestrator
{
    PortfolioValidationReport Validate(
        MigrationIntelligenceContract intelligence,
        IEnumerable<DomainMigrationDesign> designs);
}
