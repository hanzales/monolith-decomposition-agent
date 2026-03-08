using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Design.Services;

namespace Migration.Intelligence.Design.Planners;

public sealed class StranglerMigrationPlanner : IStranglerMigrationPlanner
{
    public StranglerMigrationPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary,
        ServiceContractDefinition serviceContract,
        DataOwnershipPlan dataOwnershipPlan,
        IntegrationBoundaryPlan integrationPlan)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(serviceBoundary);
        ArgumentNullException.ThrowIfNull(serviceContract);
        ArgumentNullException.ThrowIfNull(dataOwnershipPlan);
        ArgumentNullException.ThrowIfNull(integrationPlan);

        var domain = serviceBoundary.DomainCandidate;
        var dossier = intelligence.ServiceDossiers.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.CandidateName, domain));
        var recommendation = intelligence.MigrationOrderRecommendations.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.CandidateName, domain));

        var allApis = serviceContract.PublicApis
            .Concat(serviceContract.AdminApis)
            .Concat(serviceContract.InternalApis)
            .ToList();

        var readEndpointCount = allApis.Count(item =>
            item.HttpMethod is EndpointHttpMethod.Get or EndpointHttpMethod.Head or EndpointHttpMethod.Options);
        var readRatio = allApis.Count == 0 ? 0 : readEndpointCount / (double)allApis.Count;

        var couplingScore = dossier?.CouplingScore
                            ?? Math.Min(100, integrationPlan.InternalServiceDependencies.Sum(item => item.Intensity) * 10);
        var unknownChainCount = dossier?.UnknownExecutionChainCount
                                ?? serviceBoundary.ExecutionChains.Count(item => !item.IsComplete);
        var legacyBlockingRisks = intelligence.LegacyRiskDetails
            .Where(item =>
                item.BlocksExtraction
                && item.AffectedDomains.Any(affectedDomain => DesignDomainResolver.IsDomainMatch(affectedDomain, domain)))
            .ToList();

        var extractionStrategy = DetermineStrategy(
            readRatio,
            dataOwnershipPlan.SharedTables.Count,
            integrationPlan.OutboundIntegrations.Count,
            couplingScore,
            unknownChainCount,
            legacyBlockingRisks.Count,
            serviceContract.EventContracts.Count);

        var readOnlyFirst = extractionStrategy == ExtractionStrategy.ReadOnlyFirst
                            || recommendation?.ReadOnlyFirstExtractionPossible == true;
        var stagedMigration = extractionStrategy != ExtractionStrategy.DirectExtraction
                              || recommendation?.StagedMigrationRecommended == true
                              || dataOwnershipPlan.RequiresSharedDatabasePhase;

        var blockers = BuildBlockers(
            dataOwnershipPlan,
            integrationPlan,
            unknownChainCount,
            legacyBlockingRisks,
            couplingScore);

        var monolithRetentionItems = BuildMonolithRetentionItems(intelligence, domain);
        var phases = BuildPhases(
            domain,
            readOnlyFirst,
            dataOwnershipPlan.RequiresSharedDatabasePhase,
            integrationPlan.NeedsAntiCorruptionLayer,
            monolithRetentionItems.Count > 0);
        var rollbackConsiderations = BuildRollbackConsiderations(readRatio, dataOwnershipPlan.SharedTables.Count);
        var notes = BuildMigrationNotes(
            readRatio,
            recommendation,
            couplingScore,
            unknownChainCount,
            legacyBlockingRisks.Count);

        return new StranglerMigrationPlan
        {
            DomainCandidate = domain,
            ExtractionStrategy = extractionStrategy,
            ReadOnlyFirstCandidate = readOnlyFirst,
            StagedMigrationRecommended = stagedMigration,
            Phases = phases,
            MonolithRetentionItems = monolithRetentionItems,
            RollbackConsiderations = rollbackConsiderations,
            MigrationBlockers = blockers,
            MigrationNotes = notes
        };
    }

    private static ExtractionStrategy DetermineStrategy(
        double readRatio,
        int sharedTableCount,
        int outboundIntegrationCount,
        int couplingScore,
        int unknownChainCount,
        int legacyBlockerCount,
        int eventContractCount)
    {
        if (legacyBlockerCount >= 2 || couplingScore >= 80)
        {
            return ExtractionStrategy.DeferredDueToCoupling;
        }

        if (readRatio >= 0.65 && (sharedTableCount > 0 || couplingScore >= 55))
        {
            return ExtractionStrategy.ReadOnlyFirst;
        }

        if (eventContractCount > 0 && outboundIntegrationCount >= 3)
        {
            return ExtractionStrategy.EventCarveOut;
        }

        if (sharedTableCount > 0 || couplingScore >= 50 || unknownChainCount > 0)
        {
            return ExtractionStrategy.StranglerFigPhased;
        }

        return ExtractionStrategy.DirectExtraction;
    }

    private static List<string> BuildBlockers(
        DataOwnershipPlan dataOwnershipPlan,
        IntegrationBoundaryPlan integrationPlan,
        int unknownChainCount,
        IReadOnlyCollection<LegacyRiskDetailContract> legacyBlockingRisks,
        int couplingScore)
    {
        var blockers = new List<string>();

        if (dataOwnershipPlan.SharedTables.Count > 0)
        {
            blockers.Add($"Shared table coupling ({dataOwnershipPlan.SharedTables.Count} table(s)).");
        }

        if (unknownChainCount > 0)
        {
            blockers.Add($"Unknown execution chain count: {unknownChainCount}.");
        }

        if (integrationPlan.NeedsAntiCorruptionLayer)
        {
            blockers.Add("Anti-corruption layer required for legacy/shared dependencies.");
        }

        if (couplingScore >= 70)
        {
            blockers.Add($"High coupling score: {couplingScore}.");
        }

        blockers.AddRange(legacyBlockingRisks
            .Select(item => $"{item.RiskType}: {item.MigrationImpact}")
            .Distinct(StringComparer.OrdinalIgnoreCase));

        return blockers
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildMonolithRetentionItems(MigrationIntelligenceContract intelligence, string domain)
    {
        return intelligence.HangfireJobs
            .Where(item =>
                DesignDomainResolver.IsDomainMatch(item.DomainOwner, domain)
                && (item.MustRemainInMonolithInitially || item.IsLegacyHosted))
            .Select(item =>
            {
                var reason = item.IsLegacyHosted
                    ? item.LegacyHostingReason
                    : "staged migration requirement";
                return $"{item.JobName}: {reason}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<MigrationPhaseDefinition> BuildPhases(
        string domain,
        bool readOnlyFirst,
        bool sharedDatabasePhaseNeeded,
        bool antiCorruptionNeeded,
        bool hasMonolithRetentionItems)
    {
        var phases = new List<MigrationPhaseDefinition>
        {
            new()
            {
                PhaseOrder = 1,
                Name = "Stabilize Service Boundary",
                Objective = $"Freeze and validate contracts for {domain} before extraction.",
                WorkItems =
                {
                    "Validate endpoint ownership and route coverage.",
                    "Capture baseline integration and dependency tests.",
                    "Introduce feature flags for traffic redirection."
                },
                ExitCriteria =
                {
                    "Contract tests are green for public/admin/internal endpoints.",
                    "Canary routing switch is available."
                },
                CanRollback = true,
                RollbackStrategy = "Disable route switch and keep all traffic in monolith."
            }
        };

        phases.Add(readOnlyFirst
            ? new MigrationPhaseDefinition
            {
                PhaseOrder = 2,
                Name = "Read Path Extraction",
                Objective = "Move read-only APIs first to reduce data-write risk.",
                WorkItems =
                {
                    "Extract GET/HEAD endpoint handlers.",
                    "Route read traffic through service facade.",
                    "Keep writes in monolith and validate parity."
                },
                ExitCriteria =
                {
                    "Read API parity and latency targets are met.",
                    "Fallback route switch is verified."
                },
                CanRollback = true,
                RollbackStrategy = "Redirect read traffic back to monolith handlers."
            }
            : new MigrationPhaseDefinition
            {
                PhaseOrder = 2,
                Name = "Capability Carve-Out",
                Objective = $"Extract core {domain} use-cases behind an anti-corruption boundary.",
                WorkItems =
                {
                    "Move service orchestration and repositories for selected capability.",
                    "Expose stable service contracts to dependent domains.",
                    "Keep compatibility facade in monolith host."
                },
                ExitCriteria =
                {
                    "Core capability runs independently in .NET 10 host.",
                    "Dependent consumers switched to new service contract."
                },
                CanRollback = true,
                RollbackStrategy = "Use monolith compatibility facade to re-enable old execution path."
            });

        phases.Add(new MigrationPhaseDefinition
        {
            PhaseOrder = 3,
            Name = "Write Path and Data Transition",
            Objective = sharedDatabasePhaseNeeded
                ? "Transition writes with controlled shared database period."
                : "Move write path and ownership to service database.",
            WorkItems =
            {
                "Migrate command/write handlers and validation logic.",
                sharedDatabasePhaseNeeded
                    ? "Introduce ownership-aware access control for shared tables."
                    : "Cut over table ownership to isolated schema/database.",
                antiCorruptionNeeded
                    ? "Enable anti-corruption APIs for legacy/shared dependencies."
                    : "Remove temporary integration adapters after cutover."
            },
            ExitCriteria =
            {
                "Write consistency checks are green.",
                "Operational rollback path is validated."
            },
            CanRollback = true,
            RollbackStrategy = "Switch write route and job dispatch back to monolith path."
        });

        phases.Add(new MigrationPhaseDefinition
        {
            PhaseOrder = 4,
            Name = "Background Jobs and Monolith Decommission",
            Objective = hasMonolithRetentionItems
                ? "Migrate legacy-hosted jobs and remove monolith runtime dependency."
                : "Finalize cutover and remove obsolete monolith components.",
            WorkItems =
            {
                "Move compatible jobs to service host with new scheduling bootstrap.",
                "Keep legacy-hosted jobs in monolith until config/runtime refactor is complete.",
                "Delete dead code paths and redundant adapters."
            },
            ExitCriteria =
            {
                "No production traffic executes in extracted monolith modules.",
                "Runbook and SLO ownership transferred to service team."
            },
            CanRollback = false,
            RollbackStrategy = "N/A after monolith code path decommission."
        });

        return phases;
    }

    private static List<string> BuildRollbackConsiderations(double readRatio, int sharedTableCount)
    {
        var considerations = new List<string>
        {
            "Maintain route-level feature flags for each migration phase.",
            "Keep deployment artifacts for previous monolith-compatible build."
        };

        if (readRatio >= 0.6)
        {
            considerations.Add("Preserve read path fallback to monolith until parity metrics stabilize.");
        }

        if (sharedTableCount > 0)
        {
            considerations.Add("Guard writes with toggle to disable service-owned mutations on shared tables.");
        }

        return considerations;
    }

    private static List<string> BuildMigrationNotes(
        double readRatio,
        MigrationOrderRecommendationContract? recommendation,
        int couplingScore,
        int unknownChainCount,
        int legacyBlockerCount)
    {
        var notes = new List<string>
        {
            $"Read endpoint ratio: {readRatio:P0}",
            $"Coupling score considered: {couplingScore}",
            $"Unknown chain count considered: {unknownChainCount}",
            $"Legacy blockers considered: {legacyBlockerCount}"
        };

        if (recommendation is not null)
        {
            notes.Add($"Phase 1 recommendation context: {recommendation.WhyFirstOrLater}");
        }

        return notes;
    }
}
