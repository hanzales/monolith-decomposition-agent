using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Agents.Abstractions;

/// <summary>
/// Produces an actionable migration planning report from design and validation outputs.
/// </summary>
public interface IMigrationPlanningAgent
{
    Task<MigrationAgentReport> CreatePlanAsync(
        MigrationIntelligenceContract intelligence,
        IReadOnlyCollection<DomainMigrationDesign> designs,
        AgentPlanningOptions? planningOptions = null,
        PortfolioValidationReport? validationReport = null,
        CancellationToken cancellationToken = default);
}
