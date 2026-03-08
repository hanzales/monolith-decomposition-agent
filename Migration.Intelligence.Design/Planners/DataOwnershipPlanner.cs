using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Design.Services;

namespace Migration.Intelligence.Design.Planners;

public sealed class DataOwnershipPlanner : IDataOwnershipPlanner
{
    private static readonly HashSet<string> ReadTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "select", "query", "get", "find", "list"
    };

    private static readonly HashSet<string> WriteTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "insert", "update", "delete", "upsert", "save", "create"
    };

    public DataOwnershipPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(serviceBoundary);

        var domain = serviceBoundary.DomainCandidate;
        var repositoryMappings = intelligence.RepositoryTableMappings
            .Where(item => DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain))
            .ToList();

        var relatedOwnershipEntries = intelligence.TableOwnerships
            .Where(item =>
                DesignDomainResolver.IsDomainMatch(item.OwnerDomain, domain)
                || item.ReadDomains.Any(readDomain => DesignDomainResolver.IsDomainMatch(readDomain, domain))
                || item.WriteDomains.Any(writeDomain => DesignDomainResolver.IsDomainMatch(writeDomain, domain))
                || item.CandidateDomains.Any(candidate => DesignDomainResolver.IsDomainMatch(candidate, domain)))
            .ToList();

        var tableNames = DesignDomainResolver.DistinctOrdered(
        [
            .. serviceBoundary.Tables,
            .. repositoryMappings.Select(item => item.TableName),
            .. relatedOwnershipEntries.Select(item => item.TableName)
        ]);

        var ownedTables = new List<TableOwnershipDecision>();
        var sharedTables = new List<TableOwnershipDecision>();
        var referencedTables = new List<TableOwnershipDecision>();

        foreach (var tableName in tableNames)
        {
            var ownershipEntry = relatedOwnershipEntries.FirstOrDefault(item =>
                item.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            var tableMappings = repositoryMappings
                .Where(item => item.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var accessType = ResolveAccessType(domain, ownershipEntry, tableMappings);
            var role = ResolveRole(domain, ownershipEntry, tableMappings, intelligence.SharedTables);
            var confidence = ResolveConfidence(ownershipEntry, tableMappings);
            var referencedByDomains = ResolveReferencedByDomains(domain, ownershipEntry);
            var isShared = ownershipEntry?.IsShared == true
                           || intelligence.SharedTables.Any(item =>
                               item.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            var canMoveIndependently = role == TableRole.Owned && !isShared && confidence >= 0.6;

            var decision = new TableOwnershipDecision
            {
                TableName = tableName,
                Role = role,
                OwnerDomain = ownershipEntry?.OwnerDomain ?? domain,
                AccessType = accessType,
                IsShared = isShared,
                Confidence = confidence,
                CanMoveIndependently = canMoveIndependently,
                ReferencedByDomains = referencedByDomains,
                Notes = BuildTableNotes(role, accessType, ownershipEntry, tableMappings.Count)
            };

            switch (role)
            {
                case TableRole.Owned:
                    ownedTables.Add(decision);
                    break;
                case TableRole.Shared:
                    sharedTables.Add(decision);
                    break;
                default:
                    referencedTables.Add(decision);
                    break;
            }
        }

        ownedTables = ownedTables.OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase).ToList();
        sharedTables = sharedTables.OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase).ToList();
        referencedTables = referencedTables.OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase).ToList();

        var sharedDependencies = BuildSharedDependencies(intelligence, domain, sharedTables.Count);
        var requiresSharedDatabasePhase = sharedTables.Count > 0 || referencedTables.Count > 0;
        var supportsImmediateIsolation = sharedTables.Count == 0 && referencedTables.Count == 0 && ownedTables.Count > 0;

        var migrationNotes = BuildMigrationNotes(
            ownedTables.Count,
            sharedTables.Count,
            referencedTables.Count,
            intelligence.OwnerlessOrAmbiguousTables,
            tableNames);

        return new DataOwnershipPlan
        {
            DomainCandidate = domain,
            OwnedTables = ownedTables,
            SharedTables = sharedTables,
            ReferencedTables = referencedTables,
            SharedDependencies = sharedDependencies,
            RequiresSharedDatabasePhase = requiresSharedDatabasePhase,
            SupportsImmediateIsolation = supportsImmediateIsolation,
            DatabaseSplitStrategy = BuildDatabaseSplitStrategy(
                ownedTables.Count,
                sharedTables.Count,
                referencedTables.Count),
            MigrationNotes = migrationNotes
        };
    }

    private static TableRole ResolveRole(
        string domain,
        TableOwnershipContract? ownershipEntry,
        IReadOnlyCollection<RepositoryTableMappingContract> mappings,
        IReadOnlyCollection<string> sharedTables)
    {
        if (ownershipEntry is not null)
        {
            var isOwned = DesignDomainResolver.IsDomainMatch(ownershipEntry.OwnerDomain, domain);
            var isReferenced = ownershipEntry.ReadDomains.Any(item => DesignDomainResolver.IsDomainMatch(item, domain))
                               || ownershipEntry.WriteDomains.Any(item => DesignDomainResolver.IsDomainMatch(item, domain));

            if (isOwned && !ownershipEntry.IsShared)
            {
                return TableRole.Owned;
            }

            if (ownershipEntry.IsShared
                || sharedTables.Any(item => item.Equals(ownershipEntry.TableName, StringComparison.OrdinalIgnoreCase)))
            {
                return TableRole.Shared;
            }

            if (isReferenced)
            {
                return TableRole.Referenced;
            }

            if (ownershipEntry.CandidateDomains.Any(item => DesignDomainResolver.IsDomainMatch(item, domain)))
            {
                return TableRole.Ambiguous;
            }
        }

        if (mappings.Count > 0)
        {
            return TableRole.Owned;
        }

        return TableRole.Ambiguous;
    }

    private static TableAccessType ResolveAccessType(
        string domain,
        TableOwnershipContract? ownershipEntry,
        IReadOnlyCollection<RepositoryTableMappingContract> mappings)
    {
        var reads = ownershipEntry?.ReadDomains.Any(item => DesignDomainResolver.IsDomainMatch(item, domain)) == true;
        var writes = ownershipEntry?.WriteDomains.Any(item => DesignDomainResolver.IsDomainMatch(item, domain)) == true;

        foreach (var mapping in mappings)
        {
            var signal = $"{mapping.AccessPattern} {mapping.Evidence}";
            if (ContainsAnyToken(signal, ReadTokens))
            {
                reads = true;
            }

            if (ContainsAnyToken(signal, WriteTokens))
            {
                writes = true;
            }
        }

        if (reads && writes)
        {
            return TableAccessType.ReadWrite;
        }

        if (writes)
        {
            return TableAccessType.Write;
        }

        return reads ? TableAccessType.Read : TableAccessType.Unknown;
    }

    private static bool ContainsAnyToken(string value, IReadOnlyCollection<string> tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static double ResolveConfidence(
        TableOwnershipContract? ownershipEntry,
        IReadOnlyCollection<RepositoryTableMappingContract> mappings)
    {
        if (ownershipEntry is not null && ownershipEntry.Confidence > 0)
        {
            return DesignDomainResolver.Clamp(ownershipEntry.Confidence, 0.1, 1.0);
        }

        if (mappings.Count > 0)
        {
            return DesignDomainResolver.Clamp(
                mappings.Average(item => item.Confidence > 0 ? item.Confidence : 0.5),
                0.1,
                1.0);
        }

        return 0.35;
    }

    private static List<string> ResolveReferencedByDomains(string domain, TableOwnershipContract? ownershipEntry)
    {
        if (ownershipEntry is null)
        {
            return [];
        }

        return DesignDomainResolver.DistinctOrdered(
        [
            .. ownershipEntry.ReadDomains.Where(item => !DesignDomainResolver.IsDomainMatch(item, domain)),
            .. ownershipEntry.WriteDomains.Where(item => !DesignDomainResolver.IsDomainMatch(item, domain)),
            .. ownershipEntry.CandidateDomains.Where(item => !DesignDomainResolver.IsDomainMatch(item, domain))
        ]);
    }

    private static string BuildTableNotes(
        TableRole role,
        TableAccessType accessType,
        TableOwnershipContract? ownershipEntry,
        int mappingCount)
    {
        if (ownershipEntry is null && mappingCount > 0)
        {
            return "Ownership inferred from repository-to-table mapping only.";
        }

        return role switch
        {
            TableRole.Owned when accessType == TableAccessType.ReadWrite =>
                "Primary owned table; suitable for early extraction.",
            TableRole.Shared =>
                "Shared table; extraction should include anti-corruption strategy and staged ownership split.",
            TableRole.Referenced =>
                "Referenced table owned by another domain; keep API facade during early phases.",
            TableRole.Ambiguous =>
                "Ownership is ambiguous and needs manual data stewardship decision.",
            _ =>
                "Table usage detected."
        };
    }

    private static List<SharedDependencyDefinition> BuildSharedDependencies(
        MigrationIntelligenceContract intelligence,
        string domain,
        int sharedTableCount)
    {
        var selected = intelligence.SharedKernelItems
            .Where(item =>
                item.ComponentName.Contains(domain, StringComparison.OrdinalIgnoreCase)
                || item.ComponentType.Contains("config", StringComparison.OrdinalIgnoreCase)
                || item.ComponentType.Contains("context", StringComparison.OrdinalIgnoreCase)
                || item.ComponentType.Contains("dto", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (selected.Count == 0 && sharedTableCount > 0)
        {
            selected = intelligence.SharedKernelItems
                .Take(5)
                .ToList();
        }

        return selected
            .Select(item => new SharedDependencyDefinition
            {
                ComponentName = item.ComponentName,
                ComponentType = item.ComponentType,
                Recommendation = item.Recommendation,
                Rationale = item.Rationale
            })
            .OrderBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildDatabaseSplitStrategy(
        int ownedTableCount,
        int sharedTableCount,
        int referencedTableCount)
    {
        if (ownedTableCount == 0)
        {
            return "No clearly owned tables detected; start extraction with API contract and workflow isolation first.";
        }

        if (sharedTableCount == 0 && referencedTableCount == 0)
        {
            return "Domain can move with isolated database ownership in the first extraction wave.";
        }

        if (sharedTableCount > 0)
        {
            return "Move owned tables first, keep shared tables in monolith DB temporarily, and phase ownership split with outbox/replication.";
        }

        return "Extract service with owned tables while retaining referenced foreign tables behind anti-corruption APIs.";
    }

    private static List<string> BuildMigrationNotes(
        int ownedTableCount,
        int sharedTableCount,
        int referencedTableCount,
        IReadOnlyCollection<string> ownerlessTables,
        IReadOnlyCollection<string> domainTableNames)
    {
        var notes = new List<string>
        {
            $"Owned tables: {ownedTableCount}",
            $"Shared tables: {sharedTableCount}",
            $"Referenced tables: {referencedTableCount}"
        };

        var ownerlessOverlap = ownerlessTables
            .Where(table => domainTableNames.Contains(table, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (ownerlessOverlap.Count > 0)
        {
            notes.Add($"Ownerless or ambiguous tables affecting this domain: {string.Join(", ", ownerlessOverlap)}");
        }

        if (sharedTableCount > 0)
        {
            notes.Add("Shared table coupling requires staged migration and temporary shared database period.");
        }

        return notes;
    }
}
