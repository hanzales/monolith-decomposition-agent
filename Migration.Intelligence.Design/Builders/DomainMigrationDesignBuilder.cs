using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Design.Services;

namespace Migration.Intelligence.Design.Builders;

public sealed class DomainMigrationDesignBuilder : IDomainMigrationDesignBuilder
{
    private readonly IServiceBoundaryDesigner _serviceBoundaryDesigner;
    private readonly IServiceContractDesigner _serviceContractDesigner;
    private readonly IDataOwnershipPlanner _dataOwnershipPlanner;
    private readonly IIntegrationBoundaryPlanner _integrationBoundaryPlanner;
    private readonly IStranglerMigrationPlanner _stranglerMigrationPlanner;

    public DomainMigrationDesignBuilder(
        IServiceBoundaryDesigner serviceBoundaryDesigner,
        IServiceContractDesigner serviceContractDesigner,
        IDataOwnershipPlanner dataOwnershipPlanner,
        IIntegrationBoundaryPlanner integrationBoundaryPlanner,
        IStranglerMigrationPlanner stranglerMigrationPlanner)
    {
        _serviceBoundaryDesigner = serviceBoundaryDesigner;
        _serviceContractDesigner = serviceContractDesigner;
        _dataOwnershipPlanner = dataOwnershipPlanner;
        _integrationBoundaryPlanner = integrationBoundaryPlanner;
        _stranglerMigrationPlanner = stranglerMigrationPlanner;
    }

    public DomainMigrationDesign Build(MigrationIntelligenceContract intelligence, string domainCandidate)
    {
        ArgumentNullException.ThrowIfNull(intelligence);

        var serviceBoundary = _serviceBoundaryDesigner.Design(intelligence, domainCandidate);
        var serviceContract = _serviceContractDesigner.Design(intelligence, serviceBoundary);
        var dataOwnershipPlan = _dataOwnershipPlanner.Plan(intelligence, serviceBoundary);
        var integrationBoundaryPlan = _integrationBoundaryPlanner.Plan(intelligence, serviceBoundary);
        var stranglerMigrationPlan = _stranglerMigrationPlanner.Plan(
            intelligence,
            serviceBoundary,
            serviceContract,
            dataOwnershipPlan,
            integrationBoundaryPlan);

        var serviceBlueprint = BuildServiceBlueprint(
            intelligence,
            serviceBoundary,
            dataOwnershipPlan,
            integrationBoundaryPlan);
        var blockers = BuildBlockers(intelligence, serviceBoundary.DomainCandidate, stranglerMigrationPlan);
        var readinessNotes = BuildReadinessNotes(
            intelligence,
            serviceBoundary.DomainCandidate,
            serviceBoundary.BoundaryConfidence,
            serviceContract.ContractCompleteness,
            dataOwnershipPlan,
            integrationBoundaryPlan,
            stranglerMigrationPlan);

        return new DomainMigrationDesign
        {
            SelectedDomain = serviceBoundary.DomainCandidate,
            ServiceBlueprint = serviceBlueprint,
            ServiceBoundary = serviceBoundary,
            ServiceContract = serviceContract,
            DataOwnershipPlan = dataOwnershipPlan,
            IntegrationBoundaryPlan = integrationBoundaryPlan,
            StranglerMigrationPlan = stranglerMigrationPlan,
            Blockers = blockers,
            ReadinessNotes = readinessNotes,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public IReadOnlyList<DomainMigrationDesign> BuildAll(
        MigrationIntelligenceContract intelligence,
        IEnumerable<string>? domainCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(intelligence);

        var requestedDomains = domainCandidates?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        var targets = requestedDomains is { Count: > 0 }
            ? requestedDomains.Select(item => DesignDomainResolver.ResolveDomain(intelligence, item)).ToList()
            : DesignDomainResolver.DistinctOrdered(
            [
                .. intelligence.BusinessDomainCandidates,
                .. intelligence.ServiceDossiers.Select(item => item.CandidateName)
            ]);

        return targets
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Select(domain => Build(intelligence, domain))
            .ToList();
    }

    private static ServiceBlueprint BuildServiceBlueprint(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary,
        DataOwnershipPlan dataOwnershipPlan,
        IntegrationBoundaryPlan integrationBoundaryPlan)
    {
        var domain = serviceBoundary.DomainCandidate;
        var dossier = intelligence.ServiceDossiers.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.CandidateName, domain));

        var capabilityHints = intelligence.Workflows
            .Where(item => item.RelatedDomains.Any(relatedDomain => DesignDomainResolver.IsDomainMatch(relatedDomain, domain)))
            .Select(item => item.WorkflowName)
            .ToList();
        capabilityHints.AddRange(serviceBoundary.Subdomains);
        capabilityHints.AddRange(serviceBoundary.Controllers
            .Select(controller => controller.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                ? controller[..^"Controller".Length]
                : controller));
        capabilityHints = DesignDomainResolver.DistinctOrdered(capabilityHints);

        var description = dossier?.DetectionRationale;
        if (string.IsNullOrWhiteSpace(description))
        {
            description = serviceBoundary.BoundaryRationale;
        }

        return new ServiceBlueprint
        {
            CandidateName = domain,
            BoundedContextName = $"{domain}Context",
            Description = description ?? string.Empty,
            CoreCapabilities = capabilityHints,
            PrimaryControllers = serviceBoundary.Controllers,
            PrimaryRepositories = serviceBoundary.Repositories,
            PrimaryTables = dataOwnershipPlan.OwnedTables
                .Select(item => item.TableName)
                .Concat(dataOwnershipPlan.SharedTables.Select(item => item.TableName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CohesionScore = dossier?.CohesionScore ?? EstimateCohesion(serviceBoundary),
            CouplingScore = dossier?.CouplingScore ?? EstimateCoupling(integrationBoundaryPlan),
            MigrationReadinessScore = dossier?.MigrationReadinessScore ?? EstimateReadiness(
                serviceBoundary.BoundaryConfidence,
                dataOwnershipPlan.SharedTables.Count,
                integrationBoundaryPlan.OutboundIntegrations.Count),
            MigrationReadinessLevel = dossier?.MigrationReadinessLevel ?? "Unknown"
        };
    }

    private static int EstimateCohesion(ServiceBoundaryDefinition boundary)
    {
        var chainCount = boundary.ExecutionChains.Count;
        if (chainCount == 0)
        {
            return 40;
        }

        var completenessRatio = boundary.ExecutionChains.Count(item => item.IsComplete) / (double)chainCount;
        return (int)Math.Round(DesignDomainResolver.Clamp(40 + completenessRatio * 60, 1, 100));
    }

    private static int EstimateCoupling(IntegrationBoundaryPlan integrationPlan)
    {
        var dependencyCount = integrationPlan.OutboundIntegrations.Count + integrationPlan.InboundIntegrations.Count;
        var aclPenalty = integrationPlan.NeedsAntiCorruptionLayer ? 15 : 0;
        return (int)Math.Round(DesignDomainResolver.Clamp(dependencyCount * 10 + aclPenalty, 1, 100));
    }

    private static int EstimateReadiness(double boundaryConfidence, int sharedTableCount, int outboundIntegrationCount)
    {
        var baseScore = boundaryConfidence * 100;
        var penalty = sharedTableCount * 8 + Math.Max(0, outboundIntegrationCount - 2) * 4;
        return (int)Math.Round(DesignDomainResolver.Clamp(baseScore - penalty, 1, 100));
    }

    private static List<string> BuildBlockers(
        MigrationIntelligenceContract intelligence,
        string domain,
        StranglerMigrationPlan stranglerMigrationPlan)
    {
        var blockers = new List<string>();
        blockers.AddRange(stranglerMigrationPlan.MigrationBlockers);

        var additionalRiskBlockers = intelligence.LegacyRiskDetails
            .Where(item =>
                item.BlocksExtraction
                && item.AffectedDomains.Any(affectedDomain => DesignDomainResolver.IsDomainMatch(affectedDomain, domain)))
            .Select(item => $"{item.RiskType}: {item.WhyRisky}");
        blockers.AddRange(additionalRiskBlockers);

        return blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildReadinessNotes(
        MigrationIntelligenceContract intelligence,
        string domain,
        double boundaryConfidence,
        double contractCompleteness,
        DataOwnershipPlan dataOwnershipPlan,
        IntegrationBoundaryPlan integrationBoundaryPlan,
        StranglerMigrationPlan stranglerMigrationPlan)
    {
        var notes = new List<string>
        {
            $"Boundary confidence: {boundaryConfidence:P0}",
            $"Contract completeness: {contractCompleteness:P0}",
            dataOwnershipPlan.DatabaseSplitStrategy,
            integrationBoundaryPlan.Summary,
            $"Extraction strategy: {stranglerMigrationPlan.ExtractionStrategy}"
        };

        var dossier = intelligence.ServiceDossiers.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.CandidateName, domain));
        if (dossier is not null)
        {
            notes.Add($"Readiness ({dossier.MigrationReadinessLevel}): {dossier.MigrationReadinessExplanation}");
        }

        return notes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
