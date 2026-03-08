using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Agents.Abstractions;

/// <summary>
/// Computes ranked migration recommendations for domains.
/// </summary>
public interface IDomainPrioritizationAgent
{
    IReadOnlyList<AgentRecommendation> RankDomains(
        MigrationIntelligenceContract intelligence,
        IReadOnlyCollection<DomainMigrationDesign> designs,
        PortfolioValidationReport? validationReport = null);
}
