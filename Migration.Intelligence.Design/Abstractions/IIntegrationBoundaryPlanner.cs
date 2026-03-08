using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Plans inbound/outbound integration boundaries and anti-corruption layer needs.
/// </summary>
public interface IIntegrationBoundaryPlanner
{
    IntegrationBoundaryPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary);
}
