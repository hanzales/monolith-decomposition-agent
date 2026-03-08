using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Agents.Models;

public sealed class AgentReasoningRequest
{
    public required AgentPlanningOptions PlanningOptions { get; init; }
    public required MigrationIntelligenceContract Intelligence { get; init; }
    public required IReadOnlyCollection<DomainMigrationDesign> Designs { get; init; }
    public required IReadOnlyCollection<AgentRecommendation> BaseRecommendations { get; init; }
    public PortfolioValidationReport? ValidationReport { get; init; }
}
