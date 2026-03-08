using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Abstractions;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Design.Services;

public sealed class ServiceContractDesigner : IServiceContractDesigner
{
    public ServiceContractDefinition Design(
        MigrationIntelligenceContract intelligence,
        ServiceBoundaryDefinition serviceBoundary)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(serviceBoundary);

        var domain = serviceBoundary.DomainCandidate;
        var endpointMappings = intelligence.EndpointMappings
            .Where(item => DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain))
            .ToList();

        var publicApis = new List<EndpointContractDefinition>();
        var adminApis = new List<EndpointContractDefinition>();
        var internalApis = new List<EndpointContractDefinition>();

        foreach (var endpoint in endpointMappings.OrderBy(item => item.Route, StringComparer.OrdinalIgnoreCase))
        {
            var contract = new EndpointContractDefinition
            {
                Controller = endpoint.Controller,
                Action = endpoint.Action,
                HttpMethod = endpoint.HttpMethod,
                RoutePrefix = endpoint.RoutePrefix,
                Route = endpoint.Route,
                Exposure = endpoint.Exposure,
                OwnershipConfidence = endpoint.OwnershipConfidence
            };

            switch (endpoint.Exposure)
            {
                case EndpointExposure.Admin:
                    adminApis.Add(contract);
                    break;
                case EndpointExposure.Internal:
                    internalApis.Add(contract);
                    break;
                default:
                    publicApis.Add(contract);
                    break;
            }
        }

        var eventContracts = BuildEventContracts(intelligence, domain);
        var contractNotes = BuildContractNotes(serviceBoundary, endpointMappings, eventContracts);
        var contractCompleteness = CalculateContractCompleteness(
            serviceBoundary.Controllers.Count,
            endpointMappings,
            eventContracts.Count);

        return new ServiceContractDefinition
        {
            DomainCandidate = domain,
            PublicApis = publicApis,
            AdminApis = adminApis,
            InternalApis = internalApis,
            EventContracts = eventContracts,
            ContractNotes = contractNotes,
            ContractCompleteness = contractCompleteness
        };
    }

    private static List<EventContractCandidate> BuildEventContracts(
        MigrationIntelligenceContract intelligence,
        string domain)
    {
        var events = new Dictionary<string, EventContractCandidate>(StringComparer.OrdinalIgnoreCase);

        void AddEvent(
            string name,
            EventContractDirection direction,
            string source,
            string queueOrTopic = "",
            string relatedDomain = "",
            double confidence = 0.6,
            string notes = "")
        {
            if (DesignDomainResolver.IsUnknown(name))
            {
                return;
            }

            var key = $"{direction}:{name}:{queueOrTopic}:{relatedDomain}";
            if (events.TryGetValue(key, out var existing))
            {
                if (confidence > existing.Confidence)
                {
                    existing.Confidence = confidence;
                    events[key] = existing;
                }

                return;
            }

            events[key] = new EventContractCandidate
            {
                Name = name.Trim(),
                Direction = direction,
                Source = source,
                QueueOrTopic = queueOrTopic,
                RelatedDomain = relatedDomain,
                Confidence = confidence,
                Notes = notes
            };
        }

        var externalMap = intelligence.ExternalDependencyMaps.FirstOrDefault(item =>
            DesignDomainResolver.IsDomainMatch(item.DomainCandidate, domain));
        if (externalMap is not null)
        {
            foreach (var queueOrEvent in externalMap.QueuesOrEvents)
            {
                var direction = InferDirection(queueOrEvent);
                AddEvent(
                    name: queueOrEvent,
                    direction: direction,
                    source: "ExternalDependencyMap",
                    queueOrTopic: queueOrEvent,
                    confidence: 0.65);
            }
        }

        foreach (var job in intelligence.HangfireJobs.Where(item =>
                     DesignDomainResolver.IsDomainMatch(item.DomainOwner, domain)
                     && !item.IsInfrastructureOnly))
        {
            if (job.Category == HangfireJobCategory.ConsumerJob)
            {
                AddEvent(
                    name: !string.IsNullOrWhiteSpace(job.ConsumedMessageType)
                        ? job.ConsumedMessageType
                        : job.RelatedMessageOrEvent,
                    direction: EventContractDirection.Consume,
                    source: "HangfireConsumerJob",
                    queueOrTopic: string.IsNullOrWhiteSpace(job.QueueOrTopic) ? job.QueueName : job.QueueOrTopic,
                    confidence: job.OwnershipConfidence > 0 ? job.OwnershipConfidence : 0.6,
                    notes: job.JobName);
            }

            if (job.Category == HangfireJobCategory.ProducerJob)
            {
                AddEvent(
                    name: !string.IsNullOrWhiteSpace(job.ProducedMessageType)
                        ? job.ProducedMessageType
                        : job.RelatedMessageOrEvent,
                    direction: EventContractDirection.Publish,
                    source: "HangfireProducerJob",
                    queueOrTopic: string.IsNullOrWhiteSpace(job.QueueOrTopic) ? job.QueueName : job.QueueOrTopic,
                    confidence: job.OwnershipConfidence > 0 ? job.OwnershipConfidence : 0.6,
                    notes: job.JobName);
            }
        }

        foreach (var relation in intelligence.ProducerConsumerRelationships.Where(item =>
                     DesignDomainResolver.IsDomainMatch(item.DomainOwner, domain)))
        {
            AddEvent(
                name: relation.RelationshipType,
                direction: EventContractDirection.Publish,
                source: "ProducerConsumerRelationship",
                relatedDomain: domain,
                confidence: relation.Confidence > 0 ? relation.Confidence : 0.6,
                notes: $"{relation.ProducerJob} -> {relation.ConsumerJob}");
        }

        foreach (var dependency in intelligence.DependencyMatrix)
        {
            var isEventLike = dependency.DependencyKind.Contains("event", StringComparison.OrdinalIgnoreCase)
                              || dependency.DependencyKind.Contains("queue", StringComparison.OrdinalIgnoreCase);
            if (!isEventLike)
            {
                continue;
            }

            if (DesignDomainResolver.IsDomainMatch(dependency.FromDomain, domain))
            {
                AddEvent(
                    name: dependency.DependencyKind,
                    direction: EventContractDirection.Publish,
                    source: "DomainDependencyMatrix",
                    relatedDomain: dependency.ToDomain,
                    confidence: Math.Min(0.95, 0.55 + dependency.Intensity * 0.1));
            }

            if (DesignDomainResolver.IsDomainMatch(dependency.ToDomain, domain))
            {
                AddEvent(
                    name: dependency.DependencyKind,
                    direction: EventContractDirection.Consume,
                    source: "DomainDependencyMatrix",
                    relatedDomain: dependency.FromDomain,
                    confidence: Math.Min(0.95, 0.55 + dependency.Intensity * 0.1));
            }
        }

        return events.Values
            .OrderBy(item => item.Direction)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EventContractDirection InferDirection(string signal)
    {
        if (string.IsNullOrWhiteSpace(signal))
        {
            return EventContractDirection.Unknown;
        }

        if (signal.Contains("consume", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("consumer", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("inbound", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("receive", StringComparison.OrdinalIgnoreCase))
        {
            return EventContractDirection.Consume;
        }

        if (signal.Contains("publish", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("producer", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("emit", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("outbound", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("send", StringComparison.OrdinalIgnoreCase))
        {
            return EventContractDirection.Publish;
        }

        return EventContractDirection.Unknown;
    }

    private static List<string> BuildContractNotes(
        ServiceBoundaryDefinition boundary,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<EventContractCandidate> eventContracts)
    {
        var notes = new List<string>();

        if (endpointMappings.Count == 0)
        {
            notes.Add("No endpoint mappings found; contract must be validated from controllers and workflows.");
        }

        if (eventContracts.Count == 0)
        {
            notes.Add("No explicit event contract was inferred.");
        }

        if (endpointMappings.Count(item => item.Exposure == EndpointExposure.Admin) > 0)
        {
            notes.Add("Admin endpoints detected; operational auth boundaries must be separated from public API.");
        }

        if (boundary.BoundaryConfidence < 0.6)
        {
            notes.Add("Boundary confidence is below 0.60; endpoint and service ownership requires manual validation.");
        }

        return notes;
    }

    private static double CalculateContractCompleteness(
        int boundaryControllerCount,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        int eventContractCount)
    {
        var mappedControllerCount = endpointMappings
            .Select(item => item.Controller)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var endpointCoverage = boundaryControllerCount == 0
            ? (endpointMappings.Count > 0 ? 1.0 : 0.0)
            : DesignDomainResolver.Clamp(mappedControllerCount / (double)boundaryControllerCount, 0, 1);

        var ownershipSignal = endpointMappings.Count == 0
            ? 0.45
            : endpointMappings.Average(item => item.OwnershipConfidence > 0 ? item.OwnershipConfidence : 0.55);

        var eventSignal = eventContractCount == 0 ? 0.4 : 0.9;

        var score = endpointCoverage * 0.45 + ownershipSignal * 0.35 + eventSignal * 0.2;
        return DesignDomainResolver.Clamp(score, 0.1, 1.0);
    }
}
