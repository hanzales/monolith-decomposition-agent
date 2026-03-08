using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Abstractions;

/// <summary>
/// Builds migration design artifacts for a selected domain candidate.
/// </summary>
public interface IDomainMigrationDesignBuilder
{
    DomainMigrationDesign Build(MigrationIntelligenceContract intelligence, string domainCandidate);

    IReadOnlyList<DomainMigrationDesign> BuildAll(
        MigrationIntelligenceContract intelligence,
        IEnumerable<string>? domainCandidates = null);
}
