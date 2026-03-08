using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Designs API and event contracts for a selected service boundary.
/// </summary>
public interface IServiceContractDesigner
{
    ServiceContractDefinition Design(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary);
}
