using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Plans data ownership and database split strategy for a selected boundary.
/// </summary>
public interface IDataOwnershipPlanner
{
    DataOwnershipPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary);
}
