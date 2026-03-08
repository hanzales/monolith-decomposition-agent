using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Planners;
using Migration.Intelligence.Design.Services;

namespace Migration.Intelligence.Design.Builders;

/// <summary>
/// Composition entry point for the migration design layer.
/// </summary>
public static class DesignComposition
{
    /// <summary>
    /// Creates the default deterministic migration design builder.
    /// </summary>
    public static IDomainMigrationDesignBuilder CreateDefaultBuilder()
    {
        var serviceBoundaryDesigner = new ServiceBoundaryDesigner();
        var serviceContractDesigner = new ServiceContractDesigner();
        var dataOwnershipPlanner = new DataOwnershipPlanner();
        var integrationBoundaryPlanner = new IntegrationBoundaryPlanner();
        var stranglerMigrationPlanner = new StranglerMigrationPlanner();

        return new DomainMigrationDesignBuilder(
            serviceBoundaryDesigner,
            serviceContractDesigner,
            dataOwnershipPlanner,
            integrationBoundaryPlanner,
            stranglerMigrationPlanner);
    }
}
