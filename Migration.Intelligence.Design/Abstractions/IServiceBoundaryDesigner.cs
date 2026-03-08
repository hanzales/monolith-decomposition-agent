using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Designs service boundary definitions from Phase 1 migration intelligence output.
/// </summary>
public interface IServiceBoundaryDesigner
{
    ServiceBoundaryDefinition Design(MigrationIntelligenceContract intelligence, string domainCandidate);
}
