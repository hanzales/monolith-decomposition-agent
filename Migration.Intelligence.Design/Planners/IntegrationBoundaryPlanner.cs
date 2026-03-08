using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Design.Services;

namespace Migration.Intelligence.Design.Planners;

public sealed class IntegrationBoundaryPlanner : IIntegrationBoundaryPlanner
{
    public IntegrationBoundaryPlan Plan(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(serviceBoundary);

        var domain = serviceBoundary.DomainCandidate;
        var outboundIntegrations = new List<IntegrationDependencyDefinition>();
        var inboundIntegrations = new List<IntegrationDependencyDefinition>();
        var internalDependencies = new List<IntegrationDependencyDefinition>();

        var externalMap = intelligence.ExternalDependencyMaps.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain));
        if (externalMap is not null)
        {
            outboundIntegrations.AddRange(externalMap.HttpClients.Select(client => new IntegrationDependencyDefinition
            {
                Name = client,
                Direction = "Outbound",
                DependencyType = "http-client",
                Confidence = 0.75,
                Notes = "HTTP client dependency from external map."
            }));

            outboundIntegrations.AddRange(externalMap.ThirdPartyIntegrations.Select(item => new IntegrationDependencyDefinition
            {
                Name = item,
                Direction = "Outbound",
                DependencyType = "third-party",
                Confidence = 0.75
            }));

            outboundIntegrations.AddRange(externalMap.ExternalApis.Select(item => new IntegrationDependencyDefinition
            {
                Name = item,
                Direction = "Outbound",
                DependencyType = "external-api",
                Confidence = 0.8
            }));

            outboundIntegrations.AddRange(externalMap.QueuesOrEvents.Select(item => new IntegrationDependencyDefinition
            {
                Name = item,
                Direction = "Outbound",
                DependencyType = "queue-event",
                Confidence = 0.7
            }));

            internalDependencies.AddRange(externalMap.InternalServiceCalls.Select(item => new IntegrationDependencyDefinition
            {
                Name = item,
                Direction = "Internal",
                DependencyType = "internal-call",
                Confidence = 0.7
            }));
        }

        foreach (var dependency in intelligence.DependencyMatrix)
        {
            if (DesignDomainResolver.IsDomainMatch(dependency.FromDomain, domain)
                && !DesignDomainResolver.IsDomainMatch(dependency.ToDomain, domain))
            {
                var outbound = new IntegrationDependencyDefinition
                {
                    Name = dependency.ToDomain,
                    Direction = "Outbound",
                    DependencyType = MapDependencyType(dependency.DependencyKind),
                    RelatedDomain = dependency.ToDomain,
                    DependencyKind = dependency.DependencyKind,
                    Intensity = dependency.Intensity,
                    Confidence = CalculateDependencyConfidence(dependency.Intensity)
                };

                outboundIntegrations.Add(outbound);
                internalDependencies.Add(new IntegrationDependencyDefinition
                {
                    Name = dependency.ToDomain,
                    Direction = "Internal",
                    DependencyType = "domain-dependency",
                    RelatedDomain = dependency.ToDomain,
                    DependencyKind = dependency.DependencyKind,
                    Intensity = dependency.Intensity,
                    Confidence = outbound.Confidence
                });
            }

            if (DesignDomainResolver.IsDomainMatch(dependency.ToDomain, domain)
                && !DesignDomainResolver.IsDomainMatch(dependency.FromDomain, domain))
            {
                var inbound = new IntegrationDependencyDefinition
                {
                    Name = dependency.FromDomain,
                    Direction = "Inbound",
                    DependencyType = MapDependencyType(dependency.DependencyKind),
                    RelatedDomain = dependency.FromDomain,
                    DependencyKind = dependency.DependencyKind,
                    Intensity = dependency.Intensity,
                    Confidence = CalculateDependencyConfidence(dependency.Intensity)
                };

                inboundIntegrations.Add(inbound);
                internalDependencies.Add(new IntegrationDependencyDefinition
                {
                    Name = dependency.FromDomain,
                    Direction = "Internal",
                    DependencyType = "domain-dependent",
                    RelatedDomain = dependency.FromDomain,
                    DependencyKind = dependency.DependencyKind,
                    Intensity = dependency.Intensity,
                    Confidence = inbound.Confidence
                });
            }
        }

        outboundIntegrations = Deduplicate(outboundIntegrations);
        inboundIntegrations = Deduplicate(inboundIntegrations);
        internalDependencies = Deduplicate(internalDependencies);

        var antiCorruptionNeeds = BuildAntiCorruptionNeeds(intelligence, domain, outboundIntegrations);
        var integrationRisks = BuildIntegrationRisks(outboundIntegrations, inboundIntegrations, internalDependencies);

        return new IntegrationBoundaryPlan
        {
            DomainCandidate = domain,
            OutboundIntegrations = outboundIntegrations,
            InboundIntegrations = inboundIntegrations,
            InternalServiceDependencies = internalDependencies,
            AntiCorruptionLayerNeeds = antiCorruptionNeeds,
            NeedsAntiCorruptionLayer = antiCorruptionNeeds.Count > 0,
            IntegrationRisks = integrationRisks,
            Summary = BuildSummary(outboundIntegrations.Count, inboundIntegrations.Count, antiCorruptionNeeds.Count)
        };
    }

    private static string MapDependencyType(string dependencyKind)
    {
        if (dependencyKind.Contains("event", StringComparison.OrdinalIgnoreCase)
            || dependencyKind.Contains("queue", StringComparison.OrdinalIgnoreCase))
        {
            return "event";
        }

        if (dependencyKind.Contains("external", StringComparison.OrdinalIgnoreCase))
        {
            return "external-call";
        }

        if (dependencyKind.Contains("read", StringComparison.OrdinalIgnoreCase)
            || dependencyKind.Contains("write", StringComparison.OrdinalIgnoreCase))
        {
            return "shared-data";
        }

        return "domain-call";
    }

    private static double CalculateDependencyConfidence(int intensity)
    {
        return DesignDomainResolver.Clamp(0.45 + intensity * 0.1, 0.45, 0.95);
    }

    private static List<IntegrationDependencyDefinition> Deduplicate(
        IEnumerable<IntegrationDependencyDefinition> dependencies)
    {
        return dependencies
            .GroupBy(item => $"{item.Direction}|{item.DependencyType}|{item.Name}|{item.RelatedDomain}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Confidence)
                .ThenByDescending(item => item.Intensity)
                .First())
            .OrderBy(item => item.Direction, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> BuildAntiCorruptionNeeds(
        MigrationIntelligenceContract intelligence,
        string domain,
        IReadOnlyCollection<IntegrationDependencyDefinition> outboundIntegrations)
    {
        var needs = new List<string>();

        var aclRisks = intelligence.LegacyRiskDetails
            .Where(item =>
                item.RequiresAntiCorruptionLayerOrRefactor
                && item.AffectedDomains.Any(affectedDomain => DesignDomainResolver.IsDomainMatch(affectedDomain, domain)))
            .Select(item => item.RiskType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (aclRisks.Count > 0)
        {
            needs.Add($"Legacy adapters required for risks: {string.Join(", ", aclRisks)}");
        }

        if (outboundIntegrations.Any(item => item.DependencyType == "shared-data"))
        {
            needs.Add("Shared-data dependencies require anti-corruption API facade instead of direct table access.");
        }

        if (outboundIntegrations.Any(item => item.DependencyType == "external-api" || item.DependencyType == "third-party"))
        {
            needs.Add("External API dependencies should be wrapped with stable gateway contracts.");
        }

        return needs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildIntegrationRisks(
        IReadOnlyCollection<IntegrationDependencyDefinition> outboundIntegrations,
        IReadOnlyCollection<IntegrationDependencyDefinition> inboundIntegrations,
        IReadOnlyCollection<IntegrationDependencyDefinition> internalDependencies)
    {
        var risks = new List<string>();

        if (outboundIntegrations.Count >= 6)
        {
            risks.Add("High outbound integration count may delay independent deployment.");
        }

        if (inboundIntegrations.Count >= 4)
        {
            risks.Add("Multiple inbound dependencies increase cutover coordination complexity.");
        }

        if (internalDependencies.Count(item => item.DependencyType.Contains("shared-data", StringComparison.OrdinalIgnoreCase)) > 0)
        {
            risks.Add("Shared-data interactions indicate coupling that should be converted to service contracts.");
        }

        return risks;
    }

    private static string BuildSummary(int outboundCount, int inboundCount, int aclNeedCount)
    {
        return $"{outboundCount} outbound and {inboundCount} inbound integration links detected; " +
               $"{aclNeedCount} anti-corruption requirement(s) inferred.";
    }
}
