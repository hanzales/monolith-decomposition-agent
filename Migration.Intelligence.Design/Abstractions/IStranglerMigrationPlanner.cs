using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Produces a phased strangler migration plan for a selected boundary.
/// </summary>
public interface IStranglerMigrationPlanner
{
    StranglerMigrationPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary,
        ServiceContractDefinition serviceContract,
        DataOwnershipPlan dataOwnershipPlan,
        IntegrationBoundaryPlan integrationPlan);
}
