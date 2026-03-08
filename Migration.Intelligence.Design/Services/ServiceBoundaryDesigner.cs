using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Services;

public sealed class ServiceBoundaryDesigner : IServiceBoundaryDesigner
{
    public ServiceBoundaryDefinition Design(MigrationIntelligenceContract intelligence, string domainCandidate)
    {
        ArgumentNullException.ThrowIfNull(intelligence);

        var domain = DesignDomainResolver.ResolveDomain(intelligence, domainCandidate);
        var dossier = intelligence.ServiceDossiers.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.CandidateName, domain));

        var endpoints = intelligence.EndpointMappings
            .Where(item => DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain))
            .ToList();

        var chains = intelligence.ExecutionChains
            .Where(item => DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain))
            .ToList();

        var repositoryMappings = intelligence.RepositoryTableMappings
            .Where(item => DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain))
            .ToList();

        var controllers = DesignDomainResolver.DistinctOrdered(
        [
            .. endpoints.Select(item => item.Controller),
            .. chains.Select(item => item.Controller),
            .. dossier?.RelatedControllers ?? new List<string>()
        ]);

        var services = DesignDomainResolver.DistinctOrdered(
            chains.Select(item => item.Service)
                .Concat(dossier?.RelatedServices ?? new List<string>())
                .Where(value => !DesignDomainResolver.IsUnknown(value)));

        var repositories = DesignDomainResolver.DistinctOrdered(
            chains.Select(item => item.Repository)
                .Concat(repositoryMappings.Select(item => item.RepositoryName))
                .Concat(dossier?.RelatedRepositories ?? new List<string>())
                .Where(value => !DesignDomainResolver.IsUnknown(value)));

        var entities = DesignDomainResolver.DistinctOrdered(dossier?.RelatedEntities ?? new List<string>());

        var tables = DesignDomainResolver.DistinctOrdered(
            chains.Select(item => item.Table)
                .Concat(repositoryMappings.Select(item => item.TableName))
                .Concat(intelligence.TableOwnerships
                .Where(item =>
                    DesignDomainResolver.IsDomainMatch(item.OwnerDomain, domain)
                    || item.ReadDomains.Any(readDomain => DesignDomainResolver.IsDomainMatch(readDomain, domain))
                    || item.WriteDomains.Any(writeDomain => DesignDomainResolver.IsDomainMatch(writeDomain, domain)))
                .Select(item => item.TableName))
                .Concat(dossier?.RelatedTables ?? new List<string>())
                .Where(value => !DesignDomainResolver.IsUnknown(value)));

        var executionChainDefinitions = chains
            .Select(item => new ExecutionChainDefinition
            {
                Controller = item.Controller,
                Service = item.Service,
                Repository = item.Repository,
                Table = item.Table,
                Evidence = item.Evidence,
                IsComplete = IsChainComplete(item)
            })
            .OrderBy(item => item.Controller, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Service, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var inboundDependentDomains = DesignDomainResolver.DistinctOrdered(
            intelligence.DependencyMatrix
                .Where(item => DesignDomainResolver.IsDomainMatch(item.ToDomain, domain))
                .Select(item => item.FromDomain)
                .Where(item => !DesignDomainResolver.IsDomainMatch(item, domain)));

        var outboundDependencies = DesignDomainResolver.DistinctOrdered(
            intelligence.DependencyMatrix
                .Where(item => DesignDomainResolver.IsDomainMatch(item.FromDomain, domain))
                .Select(item => item.ToDomain)
                .Where(item => !DesignDomainResolver.IsDomainMatch(item, domain)));

        var subdomains = intelligence.DomainHierarchies
            .Where(item => DesignDomainResolver.IsDomainMatch(item.Domain, domain))
            .SelectMany(item => item.Subdomains)
            .ToList();
        subdomains = DesignDomainResolver.DistinctOrdered(subdomains);

        var boundaryWarnings = BuildBoundaryWarnings(
            controllers.Count,
            endpoints.Count,
            executionChainDefinitions,
            tables.Count,
            repositoryMappings.Count);

        var boundaryConfidence = CalculateBoundaryConfidence(
            endpoints,
            executionChainDefinitions,
            repositoryMappings,
            dossier,
            inboundDependentDomains.Count,
            outboundDependencies.Count);

        var boundaryRationale = BuildBoundaryRationale(
            domain,
            controllers.Count,
            services.Count,
            repositories.Count,
            tables.Count,
            boundaryConfidence);

        return new ServiceBoundaryDefinition
        {
            DomainCandidate = domain,
            Subdomains = subdomains,
            Controllers = controllers,
            Services = services,
            Repositories = repositories,
            Entities = entities,
            Tables = tables,
            ExecutionChains = executionChainDefinitions,
            InboundDependentDomains = inboundDependentDomains,
            OutboundDependencies = outboundDependencies,
            BoundaryWarnings = boundaryWarnings,
            BoundaryConfidence = boundaryConfidence,
            BoundaryRationale = boundaryRationale
        };
    }

    private static bool IsChainComplete(ExecutionChainContract chain)
    {
        return !DesignDomainResolver.IsUnknown(chain.Controller)
               && !DesignDomainResolver.IsUnknown(chain.Service)
               && !DesignDomainResolver.IsUnknown(chain.Repository)
               && !DesignDomainResolver.IsUnknown(chain.Table);
    }

    private static List<string> BuildBoundaryWarnings(
        int controllerCount,
        int endpointCount,
        IReadOnlyCollection<ExecutionChainDefinition> chains,
        int tableCount,
        int repositoryMappingCount)
    {
        var warnings = new List<string>();
        if (controllerCount == 0)
        {
            warnings.Add("No controllers were mapped to this domain candidate.");
        }

        if (endpointCount == 0)
        {
            warnings.Add("No endpoints were mapped; ownership validation is required.");
        }

        if (chains.Count > 0 && chains.Count(item => !item.IsComplete) > 0)
        {
            warnings.Add("Some execution chains are incomplete and may hide transitive dependencies.");
        }

        if (tableCount == 0 && repositoryMappingCount > 0)
        {
            warnings.Add("Repository mappings exist but table ownership was not resolved.");
        }

        return warnings;
    }

    private static double CalculateBoundaryConfidence(
        IReadOnlyCollection<EndpointMappingContract> endpoints,
        IReadOnlyCollection<ExecutionChainDefinition> chains,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings,
        ServiceDossierContract? dossier,
        int inboundDependencyCount,
        int outboundDependencyCount)
    {
        var endpointSignal = endpoints.Count == 0
            ? 0.45
            : endpoints.Average(item => item.OwnershipConfidence > 0 ? item.OwnershipConfidence : 0.55);

        var chainSignal = chains.Count == 0
            ? 0.45
            : (double)chains.Count(item => item.IsComplete) / chains.Count;

        var repositorySignal = repositoryMappings.Count == 0
            ? 0.4
            : repositoryMappings.Average(item => item.Confidence > 0 ? item.Confidence : 0.5);

        var dossierSignal = dossier is null ? 0.4 : 0.9;
        var dependencySignal = inboundDependencyCount + outboundDependencyCount == 0 ? 0.5 : 0.8;
        var unknownPenalty = chains.Count == 0
            ? 0
            : Math.Min(0.2, chains.Count(item => !item.IsComplete) / (double)chains.Count * 0.25);

        var rawScore =
            0.15 +
            endpointSignal * 0.25 +
            chainSignal * 0.2 +
            repositorySignal * 0.2 +
            dossierSignal * 0.1 +
            dependencySignal * 0.1 -
            unknownPenalty;

        return DesignDomainResolver.Clamp(rawScore, 0.1, 0.98);
    }

    private static string BuildBoundaryRationale(
        string domain,
        int controllerCount,
        int serviceCount,
        int repositoryCount,
        int tableCount,
        double confidence)
    {
        var confidenceText = confidence switch
        {
            >= 0.8 => "high-confidence",
            >= 0.6 => "medium-confidence",
            _ => "low-confidence"
        };

        return $"{domain} boundary inferred from {controllerCount} controllers, {serviceCount} services, " +
               $"{repositoryCount} repositories and {tableCount} tables ({confidenceText}).";
    }
}
