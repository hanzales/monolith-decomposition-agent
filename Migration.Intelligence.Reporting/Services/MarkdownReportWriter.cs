using System.Text;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Reporting.Templates;

namespace Migration.Intelligence.Reporting.Services;

public sealed class MarkdownReportWriter
{
    private readonly MarkdownTemplateBuilder _templateBuilder;

    public MarkdownReportWriter()
        : this(new MarkdownTemplateBuilder())
    {
    }

    public MarkdownReportWriter(MarkdownTemplateBuilder templateBuilder)
    {
        _templateBuilder = templateBuilder;
    }

    public async Task<string> WriteAsync(
        MigrationExecutionContract execution,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "migration-report.md");

        var sections = new List<string>
        {
            BuildExecutionSection(execution),
            BuildInventoryDiscoverySection(execution),
            BuildStructuralAnalysisSection(execution),
            BuildRepositoryTableMappingSection(execution),
            BuildExecutionChainsSection(execution),
            BuildHangfireJobAnalysisSection(execution),
            BuildEndpointClustersSection(execution),
            BuildDomainConsolidationSection(execution),
            BuildDomainValidationSection(execution),
            BuildSharedDataAnalysisSection(execution),
            BuildDatabaseSplitPreparationSection(execution),
            BuildDependencyMatrixSection(execution),
            BuildLegacyRiskAnalysisSection(execution),
            BuildMigrationRecommendationsSection(execution),
            BuildMigrationDesignSection(execution)
        };

        var markdown = _templateBuilder.BuildDocument("Migration Intelligence Report", sections);

        await File.WriteAllTextAsync(path, markdown, cancellationToken);
        return path;
    }

    private static string BuildExecutionSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Execution");
        builder.AppendLine($"- Executed At (UTC): `{execution.ExecutedAtUtc:O}`");
        builder.AppendLine($"- Source Path: `{execution.SourcePath}`");
        builder.AppendLine($"- Target Path: `{execution.TargetPath}`");
        builder.AppendLine($"- Dry Run: `{execution.DryRun}`");

        return builder.ToString().TrimEnd();
    }

    private static string BuildInventoryDiscoverySection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Inventory / Discovery");
        builder.AppendLine($"- Solutions: `{execution.Inventory.Solutions.Count}`");
        builder.AppendLine($"- Projects: `{execution.Inventory.Projects.Count}`");
        builder.AppendLine($"- Source Files: `{execution.Inventory.SourceFiles.Count}`");
        builder.AppendLine($"- Markdown Files: `{execution.Inventory.MarkdownFiles.Count}`");
        builder.AppendLine($"- Controller-like Files: `{execution.Insights.ControllerLikeFileCount}`");
        builder.AppendLine($"- Repository-like Files: `{execution.Insights.RepositoryLikeFileCount}`");
        builder.AppendLine($"- Endpoint Candidates: `{execution.Insights.EndpointCount}`");
        builder.AppendLine($"- Dependency Signals: `{execution.Insights.DependencyCount}`");
        builder.AppendLine($"- Legacy Risk Signals: `{execution.Insights.LegacyRiskCount}`");
        builder.AppendLine();

        builder.AppendLine("### Projects and Classification");
        var classificationByName = execution.Intelligence.ComponentClassifications
            .GroupBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var project in execution.Inventory.Projects
                     .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var framework = string.IsNullOrWhiteSpace(project.TargetFramework)
                ? "n/a"
                : project.TargetFramework;

            classificationByName.TryGetValue(project.Name, out var classification);
            var category = classification?.Category.ToString() ?? "Unclassified";
            builder.AppendLine($"- `{project.Name}` (`{framework}`) - `{project.RelativePath}` -> `{category}`");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildStructuralAnalysisSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;

        builder.AppendLine("## Structural Analysis");
        builder.AppendLine($"- Consolidated Domain Candidates: `{intelligence.BusinessDomainCandidates.Count}`");
        builder.AppendLine($"- Domain Hierarchies: `{intelligence.DomainHierarchies.Count}`");
        builder.AppendLine($"- Endpoint Mappings: `{intelligence.EndpointMappings.Count}`");
        builder.AppendLine($"- Repository-to-Table Mappings: `{intelligence.RepositoryTableMappings.Count}`");
        builder.AppendLine($"- Execution Chains: `{intelligence.ExecutionChains.Count}`");
        builder.AppendLine($"- Hangfire Jobs: `{intelligence.HangfireJobs.Count}`");
        builder.AppendLine($"- Producer->Consumer Relationships: `{intelligence.ProducerConsumerRelationships.Count}`");
        builder.AppendLine($"- Scheduled Jobs (Resolved/Unresolved): `{intelligence.BackgroundJobValidation.ScheduledJobsWithResolvedSchedule}`/`{intelligence.BackgroundJobValidation.ScheduledJobsWithUnresolvedSchedule}`");
        builder.AppendLine($"- Workflows: `{intelligence.Workflows.Count}`");
        builder.AppendLine();

        builder.AppendLine("### Component Layer Summary");
        foreach (var group in intelligence.ComponentClassifications
                     .GroupBy(item => item.Category)
                     .OrderBy(group => group.Key))
        {
            builder.AppendLine($"- `{group.Key}`: `{group.Count()}` component(s)");
        }

        builder.AppendLine();
        builder.AppendLine("### External Integration Snapshot");
        foreach (var map in intelligence.ExternalDependencyMaps
                     .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase))
        {
            var integrationCount = map.HttpClients.Count + map.ThirdPartyIntegrations.Count + map.ExternalApis.Count + map.QueuesOrEvents.Count;
            builder.AppendLine($"- `{map.DomainCandidate}`: `{integrationCount}` external signal(s)");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildRepositoryTableMappingSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var mappings = execution.Intelligence.RepositoryTableMappings;

        builder.AppendLine("## Repository -> Table Mapping");
        if (mappings.Count == 0)
        {
            builder.AppendLine("- No repository-to-table mappings detected.");
            return builder.ToString().TrimEnd();
        }

        var orderedMappings = mappings
            .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < orderedMappings.Count; i++)
        {
            if (i == 0 || i % 500 == 0)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"_Repository->Table Continuation ({i + 1}-{Math.Min(i + 500, orderedMappings.Count)} of {orderedMappings.Count})_");
                }

                builder.AppendLine("| Domain | Repository | Table | Access Pattern | Confidence | Evidence |");
                builder.AppendLine("| --- | --- | --- | --- | ---: | --- |");
            }

            var mapping = orderedMappings[i];
            builder.AppendLine($"| `{mapping.DomainCandidate}` | `{mapping.RepositoryName}` | `{mapping.TableName}` | `{mapping.AccessPattern}` | `{mapping.Confidence:F2}` | {EscapePipes(mapping.Evidence)} |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildExecutionChainsSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var chains = execution.Intelligence.ExecutionChains;

        builder.AppendLine("## Execution Chains");
        if (chains.Count == 0)
        {
            builder.AppendLine("- No controller->service->repository->table chains detected.");
            return builder.ToString().TrimEnd();
        }

        var unknownChainCount = chains.Count(chain =>
            string.IsNullOrWhiteSpace(chain.Service)
            || string.IsNullOrWhiteSpace(chain.Repository)
            || string.IsNullOrWhiteSpace(chain.Table));
        builder.AppendLine($"- Unknown/partial chain count: `{unknownChainCount}` of `{chains.Count}`");
        builder.AppendLine();

        var orderedChains = chains
            .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Controller, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < orderedChains.Count; i++)
        {
            if (i == 0 || i % 500 == 0)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"_Execution Chain Continuation ({i + 1}-{Math.Min(i + 500, orderedChains.Count)} of {orderedChains.Count})_");
                }

                builder.AppendLine("| Domain | Controller | Service | Repository | Table | Evidence |");
                builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            }

            var chain = orderedChains[i];
            builder.AppendLine($"| `{chain.DomainCandidate}` | `{chain.Controller}` | `{ValueOrUnknown(chain.Service)}` | `{ValueOrUnknown(chain.Repository)}` | `{ValueOrUnknown(chain.Table)}` | {EscapePipes(chain.Evidence)} |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildHangfireJobAnalysisSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var jobs = execution.Intelligence.HangfireJobs
            .OrderBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine("## Hangfire Job Analysis");
        if (jobs.Count == 0)
        {
            builder.AppendLine("- No Hangfire-related job signals detected.");
            return builder.ToString().TrimEnd();
        }

        var consumerCount = jobs.Count(item => item.Category == HangfireJobCategory.ConsumerJob);
        var scheduledCount = jobs.Count(item => item.Category == HangfireJobCategory.ScheduledJob);
        var triggeredCount = jobs.Count(item => item.Category == HangfireJobCategory.TriggeredJob);
        var producerCount = jobs.Count(item => item.Category == HangfireJobCategory.ProducerJob);
        var normalCount = scheduledCount + triggeredCount + producerCount;
        var infrastructureOnlyCount = jobs.Count(item => item.IsInfrastructureOnly);
        var validation = execution.Intelligence.BackgroundJobValidation;

        builder.AppendLine($"- Total Jobs: `{jobs.Count}`");
        builder.AppendLine($"- Consumer Jobs: `{consumerCount}`");
        builder.AppendLine($"- Scheduled Jobs: `{scheduledCount}`");
        builder.AppendLine($"- Triggered Jobs: `{triggeredCount}`");
        builder.AppendLine($"- Producer Jobs: `{producerCount}`");
        builder.AppendLine($"- Normal / Non-Consumer Jobs: `{normalCount}`");
        builder.AppendLine($"- Infrastructure-only Jobs: `{infrastructureOnlyCount}`");
        builder.AppendLine($"- Validation: discovered `{validation.DiscoveredJobCount}`, typed `{validation.TypedJobCount}`, scheduled resolved `{validation.ScheduledJobsWithResolvedSchedule}`, scheduled unresolved `{validation.ScheduledJobsWithUnresolvedSchedule}`, mapped `{validation.DomainMappedJobCount}`, unmapped `{validation.UnmappedJobCount}`");

        if (validation.Warnings.Count > 0)
        {
            builder.AppendLine("- Validation Warnings:");
            foreach (var warning in validation.Warnings)
            {
                builder.AppendLine($"  - {warning}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### A. Background Job Inventory");
        builder.AppendLine("| Job | Type | Trigger | Domain | Queue/Topic | Message | Raw Schedule Key | Resolved Schedule | Schedule Status | Registration Source | Legacy Hosted |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var job in jobs)
        {
            var message = !string.IsNullOrWhiteSpace(job.ConsumedMessageType) ? job.ConsumedMessageType : job.ProducedMessageType;
            builder.AppendLine($"| `{job.JobName}` | `{job.Category}` | `{job.TriggerType}` | `{job.DomainOwner}` | `{ValueOrUnknown(job.QueueOrTopic)}` | `{ValueOrUnknown(message)}` | `{ValueOrUnknown(job.RawScheduleKey)}` | `{ValueOrUnknown(job.ResolvedScheduleExpression)}` | `{job.ScheduleResolutionStatus}` | `{ValueOrUnknown(job.RegistrationSource)}` | `{YesNo(job.IsLegacyHosted)}` |");
        }

        builder.AppendLine();
        builder.AppendLine("### B. Jobs by Domain");
        builder.AppendLine("| Domain | Consumer Jobs | Normal Jobs | Scheduled | Triggered | Producer | Legacy Hosted |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var group in jobs
                     .GroupBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var domainConsumer = group.Count(item => item.Category == HangfireJobCategory.ConsumerJob);
            var domainScheduled = group.Count(item => item.Category == HangfireJobCategory.ScheduledJob);
            var domainTriggered = group.Count(item => item.Category == HangfireJobCategory.TriggeredJob);
            var domainProducer = group.Count(item => item.Category == HangfireJobCategory.ProducerJob);
            var domainNormal = domainScheduled + domainTriggered + domainProducer;
            var domainLegacyHosted = group.Count(item => item.IsLegacyHosted);
            builder.AppendLine($"| `{group.Key}` | `{domainConsumer}` | `{domainNormal}` | `{domainScheduled}` | `{domainTriggered}` | `{domainProducer}` | `{domainLegacyHosted}` |");
        }

        builder.AppendLine();
        builder.AppendLine("### C. Consumer Jobs");
        var consumerJobs = jobs
            .Where(item => item.Category == HangfireJobCategory.ConsumerJob)
            .OrderBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (consumerJobs.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            builder.AppendLine("| Job | Domain | Consumed Message | Queue/Topic | Producer Job | Confidence |");
            builder.AppendLine("| --- | --- | --- | --- | --- | ---: |");
            foreach (var job in consumerJobs)
            {
                builder.AppendLine($"| `{job.JobName}` | `{job.DomainOwner}` | `{ValueOrUnknown(job.ConsumedMessageType)}` | `{ValueOrUnknown(job.QueueOrTopic)}` | `{ValueOrUnknown(job.ProducerJob)}` | `{job.OwnershipConfidence:F2}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### D. Scheduled Jobs");
        var scheduledJobs = jobs
            .Where(item => item.Category == HangfireJobCategory.ScheduledJob)
            .OrderBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (scheduledJobs.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            builder.AppendLine("| Job | Domain | Raw Key | Resolved Schedule | Resolution Status | Source Type | Source |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var job in scheduledJobs)
            {
                builder.AppendLine($"| `{job.JobName}` | `{job.DomainOwner}` | `{ValueOrUnknown(job.RawScheduleKey)}` | `{ValueOrUnknown(job.ResolvedScheduleExpression)}` | `{job.ScheduleResolutionStatus}` | `{job.ScheduleSourceType}` | `{ValueOrUnknown(job.ScheduleSource)}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### E. Producer -> Consumer Relationships");
        var relationships = execution.Intelligence.ProducerConsumerRelationships
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (relationships.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            builder.AppendLine("| Producer Job | Consumer Job | Domain | Relationship Type | Confidence |");
            builder.AppendLine("| --- | --- | --- | --- | ---: |");
            foreach (var relationship in relationships)
            {
                builder.AppendLine($"| `{relationship.ProducerJob}` | `{relationship.ConsumerJob}` | `{relationship.DomainOwner}` | `{relationship.RelationshipType}` | `{relationship.Confidence:F2}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### F. Unmapped / Low-Confidence Jobs");
        var lowConfidenceJobs = jobs
            .Where(item => item.DomainOwner.Equals("Unmapped", StringComparison.OrdinalIgnoreCase) || item.OwnershipConfidence < 0.55)
            .OrderBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lowConfidenceJobs.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var job in lowConfidenceJobs)
            {
                builder.AppendLine($"- `{job.JobName}` (`{job.DomainOwner}`): confidence `{job.OwnershipConfidence:F2}`, top candidates `{JoinOrNone(job.TopCandidateDomains)}`, reason `{ValueOrUnknown(job.UnmappedReason)}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### G. Legacy-Hosted Jobs");
        var legacyHostedJobs = jobs
            .Where(item => item.IsLegacyHosted)
            .OrderBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (legacyHostedJobs.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            builder.AppendLine("| Job | Domain | Reason | Needs Refactor |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var job in legacyHostedJobs)
            {
                builder.AppendLine($"| `{job.JobName}` | `{job.DomainOwner}` | {EscapePipes(ValueOrUnknown(job.LegacyHostingReason))} | `{YesNo(job.NeedsRefactorBeforeMigration)}` |");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildEndpointClustersSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;

        builder.AppendLine("## Endpoint Clusters");
        if (intelligence.BusinessDomainCandidates.Count == 0)
        {
            builder.AppendLine("- No consolidated domain candidates available for endpoint clustering.");
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("| Domain | Controllers | Route Prefixes | Endpoint Count | Public | Admin | Internal |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: |");

        foreach (var domain in intelligence.BusinessDomainCandidates
                     .OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var endpoints = intelligence.EndpointMappings
                .Where(item => item.DomainCandidate.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var controllers = endpoints.Select(item => item.Controller).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            var routePrefixes = endpoints.Select(item => item.RoutePrefix).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            var publicCount = endpoints.Count(item => item.Exposure == EndpointExposure.Public);
            var adminCount = endpoints.Count(item => item.Exposure == EndpointExposure.Admin);
            var internalCount = endpoints.Count(item => item.Exposure == EndpointExposure.Internal);

            builder.AppendLine($"| `{domain}` | `{JoinOrNone(controllers, ", ")}` | `{JoinOrNone(routePrefixes, ", ")}` | `{endpoints.Count}` | `{publicCount}` | `{adminCount}` | `{internalCount}` |");
        }

        builder.AppendLine();
        builder.AppendLine("### Endpoint Ownership Details");
        if (intelligence.EndpointMappings.Count == 0)
        {
            builder.AppendLine("- No endpoint ownership mapping detected.");
        }
        else
        {
            var orderedEndpoints = intelligence.EndpointMappings
                .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Route, StringComparer.OrdinalIgnoreCase)
                .ToList();

            builder.AppendLine("| Endpoint | Controller | Domain Owner | Endpoint Type | Ownership Confidence |");
            builder.AppendLine("| --- | --- | --- | --- | ---: |");
            for (var i = 0; i < orderedEndpoints.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"_Endpoint Ownership Continuation ({i + 1}-{Math.Min(i + 500, orderedEndpoints.Count)} of {orderedEndpoints.Count})_");
                    builder.AppendLine("| Endpoint | Controller | Domain Owner | Endpoint Type | Ownership Confidence |");
                    builder.AppendLine("| --- | --- | --- | --- | ---: |");
                }

                var endpoint = orderedEndpoints[i];
                builder.AppendLine($"| `{endpoint.HttpMethod} {endpoint.Route}` | `{endpoint.Controller}` | `{endpoint.DomainCandidate}` | `{endpoint.Exposure}` | `{endpoint.OwnershipConfidence:F2}` |");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildDomainConsolidationSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;
        var inferredDomains = intelligence.BusinessDomainCandidates
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hierarchyByDomain = intelligence.DomainHierarchies
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var renderedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        builder.AppendLine("## Domain Consolidation");
        builder.AppendLine("- Final candidates are consolidated bounded-context roots, not raw entity-level fragments.");
        builder.AppendLine("- Subdomain/components remain visible for planning but extraction decisions should be made at root-domain level.");
        builder.AppendLine();

        if (inferredDomains.Count == 0)
        {
            builder.AppendLine("- No domain hierarchy generated.");
            return builder.ToString().TrimEnd();
        }

        foreach (var domain in inferredDomains)
        {
            renderedDomains.Add(domain);
            hierarchyByDomain.TryGetValue(domain, out var hierarchy);
            var subdomains = hierarchy?.Subdomains
                                .Where(item => !string.IsNullOrWhiteSpace(item))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                                .ToList()
                            ?? new List<string>();

            builder.AppendLine($"### {domain}");
            if (subdomains.Count == 0)
            {
                builder.AppendLine("- Subdomains/components: none");
            }
            else
            {
                builder.AppendLine($"- Subdomains/components: {JoinOrNone(subdomains)}");
            }

            builder.AppendLine();
        }

        var missingUnrendered = inferredDomains
            .Except(renderedDomains, StringComparer.OrdinalIgnoreCase)
            .Concat(intelligence.DomainEnumerationValidation.MissingDomains)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine("### Missing / Unrendered Domains");
        if (missingUnrendered.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var missing in missingUnrendered)
            {
                builder.AppendLine($"- `{missing}`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildDomainValidationSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;
        var validation = intelligence.DomainEnumerationValidation;
        var allMissing = validation.MissingDomains
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var renderedRootDomainCount = validation.RenderedRootDomainCount;
        var isValid = validation.IsValid
                      && allMissing.Count == 0
                      && renderedRootDomainCount == validation.InferredRootDomainCount;

        builder.AppendLine("## Domain Enumeration Validation");
        builder.AppendLine($"- Inferred Root Domains: `{validation.InferredRootDomainCount}`");
        builder.AppendLine($"- Rendered Root Domains: `{renderedRootDomainCount}`");
        builder.AppendLine($"- Domains with Dossiers: `{validation.DossierDomainCount}`");
        builder.AppendLine($"- Domains with Endpoint Clusters: `{validation.EndpointClusterDomainCount}`");
        builder.AppendLine($"- Domains with Dependency Entries: `{validation.DependencyDomainCount}`");
        builder.AppendLine($"- Validation Status: `{(isValid ? "valid" : "warning")}`");

        if (validation.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("### Validation Warnings");
            foreach (var warning in validation.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Missing / Unrendered Domains");
        if (allMissing.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var domain in allMissing)
            {
                builder.AppendLine($"- `{domain}`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSharedDataAnalysisSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;

        builder.AppendLine("## Shared Data Analysis");
        builder.AppendLine($"- Table Ownership Entries: `{intelligence.TableOwnerships.Count}`");
        builder.AppendLine($"- Shared Tables: `{intelligence.SharedTables.Count}`");
        builder.AppendLine($"- Ownerless/Ambiguous Tables: `{intelligence.OwnerlessOrAmbiguousTables.Count}`");
        builder.AppendLine();

        if (intelligence.TableOwnerships.Count > 0)
        {
            var orderedOwnerships = intelligence.TableOwnerships
                .OrderBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < orderedOwnerships.Count; i++)
            {
                if (i == 0 || i % 500 == 0)
                {
                    if (i > 0)
                    {
                        builder.AppendLine();
                        builder.AppendLine($"_Table Ownership Continuation ({i + 1}-{Math.Min(i + 500, orderedOwnerships.Count)} of {orderedOwnerships.Count})_");
                    }

                    builder.AppendLine("| Table | Owner Domain | Confidence | Shared | Read Domains | Write Domains | Candidate Domains |");
                    builder.AppendLine("| --- | --- | ---: | --- | --- | --- | --- |");
                }

                var ownership = orderedOwnerships[i];
                builder.AppendLine($"| `{ownership.TableName}` | `{ValueOrUnknown(ownership.OwnerDomain)}` | `{ownership.Confidence:F2}` | `{YesNo(ownership.IsShared)}` | `{JoinOrNone(ownership.ReadDomains, ", ")}` | `{JoinOrNone(ownership.WriteDomains, ", ")}` | `{JoinOrNone(ownership.CandidateDomains, ", ")}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Shared Table List");
        if (intelligence.SharedTables.Count == 0)
        {
            builder.AppendLine("- No strongly shared tables inferred.");
        }
        else
        {
            foreach (var table in intelligence.SharedTables.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{table}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Ownerless Table List");
        if (intelligence.OwnerlessOrAmbiguousTables.Count == 0)
        {
            builder.AppendLine("- No ownerless/ambiguous tables inferred.");
        }
        else
        {
            foreach (var table in intelligence.OwnerlessOrAmbiguousTables.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- `{table}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Shared Kernel Detection");
        if (intelligence.SharedKernelItems.Count == 0)
        {
            builder.AppendLine("- No shared kernel items inferred.");
        }
        else
        {
            var orderedItems = intelligence.SharedKernelItems
                .OrderBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = 0; i < orderedItems.Count; i++)
            {
                if (i == 0 || i % 500 == 0)
                {
                    if (i > 0)
                    {
                        builder.AppendLine();
                        builder.AppendLine($"_Shared Kernel Continuation ({i + 1}-{Math.Min(i + 500, orderedItems.Count)} of {orderedItems.Count})_");
                    }

                    builder.AppendLine("| Component | Type | Recommendation | Rationale |");
                    builder.AppendLine("| --- | --- | --- | --- |");
                }

                var item = orderedItems[i];
                builder.AppendLine($"| `{item.ComponentName}` | `{item.ComponentType}` | `{item.Recommendation}` | {EscapePipes(item.Rationale)} |");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildDatabaseSplitPreparationSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var ownerships = execution.Intelligence.TableOwnerships;

        builder.AppendLine("## Database Split Preparation");
        if (ownerships.Count == 0)
        {
            builder.AppendLine("- No table ownership data available for split preparation.");
            return builder.ToString().TrimEnd();
        }

        var moveImmediately = ownerships
            .Where(item => !item.IsShared
                           && !string.IsNullOrWhiteSpace(item.OwnerDomain)
                           && item.Confidence >= 0.75
                           && item.WriteDomains.Count <= 1)
            .Select(item => item.TableName)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var remainShared = ownerships
            .Where(item => item.IsShared || string.IsNullOrWhiteSpace(item.OwnerDomain) || item.Confidence < 0.55)
            .Select(item => item.TableName)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var crossDomainUsage = ownerships
            .Where(item => item.CandidateDomains.Count > 1 || item.ReadDomains.Count > 1 || item.WriteDomains.Count > 1)
            .OrderByDescending(item => item.CandidateDomains.Count)
            .ThenBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine($"- Tables moveable immediately: `{moveImmediately.Count}`");
        builder.AppendLine($"- Tables remaining shared: `{remainShared.Count}`");
        builder.AppendLine($"- Tables with cross-domain usage: `{crossDomainUsage.Count}`");

        builder.AppendLine();
        builder.AppendLine("### Move Immediately");
        if (moveImmediately.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            for (var i = 0; i < moveImmediately.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Move Immediately Continuation ({i + 1}-{Math.Min(i + 500, moveImmediately.Count)} of {moveImmediately.Count})_");
                }

                var table = moveImmediately[i];
                builder.AppendLine($"- `{table}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Remain Shared");
        if (remainShared.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            for (var i = 0; i < remainShared.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Remain Shared Continuation ({i + 1}-{Math.Min(i + 500, remainShared.Count)} of {remainShared.Count})_");
                }

                var table = remainShared[i];
                builder.AppendLine($"- `{table}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Cross-Domain Table Usage");
        if (crossDomainUsage.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            for (var i = 0; i < crossDomainUsage.Count; i++)
            {
                if (i == 0 || i % 500 == 0)
                {
                    if (i > 0)
                    {
                        builder.AppendLine();
                        builder.AppendLine($"_Cross-Domain Table Usage Continuation ({i + 1}-{Math.Min(i + 500, crossDomainUsage.Count)} of {crossDomainUsage.Count})_");
                    }

                    builder.AppendLine("| Table | Owner | Referenced By | Read Domains | Write Domains | Confidence |");
                    builder.AppendLine("| --- | --- | --- | --- | --- | ---: |");
                }

                var item = crossDomainUsage[i];
                var referencedBy = item.CandidateDomains
                    .Where(domain => !domain.Equals(item.OwnerDomain, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                builder.AppendLine($"| `{item.TableName}` | `{ValueOrUnknown(item.OwnerDomain)}` | `{JoinOrNone(referencedBy, ", ")}` | `{JoinOrNone(item.ReadDomains, ", ")}` | `{JoinOrNone(item.WriteDomains, ", ")}` | `{item.Confidence:F2}` |");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildDependencyMatrixSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var matrix = execution.Intelligence.DependencyMatrix;

        builder.AppendLine("## Dependency Matrix");
        if (matrix.Count == 0)
        {
            builder.AppendLine("- No cross-domain dependency edges inferred.");
            return builder.ToString().TrimEnd();
        }

        var orderedDependencies = matrix
            .OrderByDescending(item => item.Intensity)
            .ThenBy(item => item.FromDomain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ToDomain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < orderedDependencies.Count; i++)
        {
            if (i == 0 || i % 500 == 0)
            {
                if (i > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"_Dependency Matrix Continuation ({i + 1}-{Math.Min(i + 500, orderedDependencies.Count)} of {orderedDependencies.Count})_");
                }

                builder.AppendLine("| From Domain | To Domain | Dependency Type | Intensity |");
                builder.AppendLine("| --- | --- | --- | ---: |");
            }

            var dependency = orderedDependencies[i];
            builder.AppendLine($"| `{dependency.FromDomain}` | `{dependency.ToDomain}` | `{dependency.DependencyKind}` | `{dependency.Intensity}` |");
        }

        builder.AppendLine();
        builder.AppendLine("### Dependency Graph");
        builder.AppendLine("```mermaid");
        builder.AppendLine("graph LR");
        foreach (var edge in orderedDependencies)
        {
            var from = SanitizeMermaidNode(edge.FromDomain);
            var to = SanitizeMermaidNode(edge.ToDomain);
            builder.AppendLine($"    {from}[\"{edge.FromDomain}\"] -->|{edge.DependencyKind}:{edge.Intensity}| {to}[\"{edge.ToDomain}\"]");
        }

        builder.AppendLine("```");

        return builder.ToString().TrimEnd();
    }

    private static string BuildLegacyRiskAnalysisSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var risks = execution.Intelligence.LegacyRiskDetails;

        builder.AppendLine("## Legacy Risk Analysis");
        builder.AppendLine($"- Risk Types Detected: `{risks.Count}`");

        if (risks.Count == 0)
        {
            builder.AppendLine("- No major legacy risk patterns inferred.");
            return builder.ToString().TrimEnd();
        }

        foreach (var risk in risks.OrderByDescending(item => item.ImpactedFiles.Count).ThenBy(item => item.RiskType, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.AppendLine($"### {risk.RiskType}");
            builder.AppendLine($"- Why Risky: {risk.WhyRisky}");
            builder.AppendLine($"- Migration Impact: {risk.MigrationImpact}");
            builder.AppendLine($"- Recommended Remediation: {risk.RecommendedRemediation}");
            builder.AppendLine($"- Affected Domain Candidates: {JoinOrNone(risk.AffectedDomains)}");
            builder.AppendLine($"- Blocks Extraction: `{YesNo(risk.BlocksExtraction)}`");
            builder.AppendLine($"- ACL/Refactor Needed: `{YesNo(risk.RequiresAntiCorruptionLayerOrRefactor)}`");
            builder.AppendLine("- Impacted Files:");
            for (var i = 0; i < risk.ImpactedFiles.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"  - _Impacted Files Continuation ({i + 1}-{Math.Min(i + 500, risk.ImpactedFiles.Count)} of {risk.ImpactedFiles.Count})_");
                }

                var file = risk.ImpactedFiles[i];
                builder.AppendLine($"  - `{file}`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildMigrationRecommendationsSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var recommendations = execution.Intelligence.MigrationOrderRecommendations;
        var dossiers = execution.Intelligence.ServiceDossiers
            .GroupBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        builder.AppendLine("## Migration Recommendations");

        if (recommendations.Count == 0)
        {
            builder.AppendLine("- No migration order recommendation available.");
        }
        else
        {
            builder.AppendLine("### Migration Order Recommendation");
            builder.AppendLine("| Rank | Candidate | Readiness | Level | Why First/Later | Major Blockers | Read-only First | Staged |");
            builder.AppendLine("| ---: | --- | ---: | --- | --- | --- | --- | --- |");

            foreach (var recommendation in recommendations.OrderBy(item => item.Rank))
            {
                dossiers.TryGetValue(recommendation.CandidateName, out var dossier);
                var readiness = dossier?.MigrationReadinessScore ?? 0;
                var level = dossier?.MigrationReadinessLevel ?? "Unknown";

                builder.AppendLine($"| `{recommendation.Rank}` | `{recommendation.CandidateName}` | `{readiness}` | `{level}` | {EscapePipes(recommendation.WhyFirstOrLater)} | `{JoinOrNone(recommendation.MajorBlockers, "; ")}` | `{YesNo(recommendation.ReadOnlyFirstExtractionPossible)}` | `{YesNo(recommendation.StagedMigrationRecommended)}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Service Dossiers");
        foreach (var dossier in execution.Intelligence.ServiceDossiers
                     .OrderByDescending(item => item.MigrationReadinessScore)
                     .ThenBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase))
        {
            var publicCount = dossier.RelatedEndpoints.Count(item => item.Exposure == EndpointExposure.Public);
            var adminCount = dossier.RelatedEndpoints.Count(item => item.Exposure == EndpointExposure.Admin);
            var internalCount = dossier.RelatedEndpoints.Count(item => item.Exposure == EndpointExposure.Internal);
            var missingSections = new List<string>();
            if (dossier.RelatedControllers.Count == 0)
            {
                missingSections.Add("controllers");
            }

            if (dossier.RelatedEndpoints.Count == 0)
            {
                missingSections.Add("endpoints");
            }

            if (dossier.RelatedRepositories.Count == 0)
            {
                missingSections.Add("repositories");
            }

            if (dossier.RelatedTables.Count == 0)
            {
                missingSections.Add("tables");
            }

            if (dossier.ExternalDependencies.Count == 0)
            {
                missingSections.Add("external dependencies");
            }

            builder.AppendLine($"#### {dossier.CandidateName}");
            builder.AppendLine($"- Why Detected: {dossier.DetectionRationale}");
            builder.AppendLine($"- Subdomains/Components: {JoinOrNone(dossier.Subdomains)}");
            builder.AppendLine($"- Dossier Completeness: `{(missingSections.Count == 0 ? "complete" : $"partial ({string.Join(", ", missingSections)})")}`");
            builder.AppendLine($"- Controllers: {JoinOrNone(dossier.RelatedControllers)}");
            builder.AppendLine($"- Endpoints: `{dossier.RelatedEndpoints.Count}` (public `{publicCount}`, admin `{adminCount}`, internal `{internalCount}`)");
            builder.AppendLine($"- Services: {JoinOrNone(dossier.RelatedServices)}");
            builder.AppendLine($"- Repositories: {JoinOrNone(dossier.RelatedRepositories)}");
            builder.AppendLine($"- Entities: {JoinOrNone(dossier.RelatedEntities)}");
            builder.AppendLine($"- Tables: {JoinOrNone(dossier.RelatedTables)}");
            builder.AppendLine($"- Background Jobs: `{dossier.BackgroundJobCount}` (consumer `{dossier.ConsumerJobCount}`, scheduled `{dossier.ScheduledJobCount}`, triggered `{dossier.TriggeredJobCount}`, producer `{dossier.ProducerJobCount}`, normal `{dossier.NormalJobCount}`, legacy-hosted `{dossier.LegacyHostedJobCount}`)");
            builder.AppendLine($"- Consumer Jobs: {JoinOrNone(dossier.ConsumerJobs)}");
            builder.AppendLine($"- Scheduled Jobs: {JoinOrNone(dossier.ScheduledJobs)}");
            builder.AppendLine($"- Triggered Jobs: {JoinOrNone(dossier.TriggeredJobs)}");
            builder.AppendLine($"- Producer Jobs: {JoinOrNone(dossier.ProducerJobs)}");
            builder.AppendLine($"- Normal Jobs: {JoinOrNone(dossier.NormalJobs)}");
            builder.AppendLine($"- Job Scheduling Dependencies: {JoinOrNone(dossier.JobSchedulingDependencies)}");
            builder.AppendLine($"- External Dependencies: {JoinOrNone(dossier.ExternalDependencies)}");
            builder.AppendLine($"- Shared Dependencies: {JoinOrNone(dossier.SharedDependencies)}");
            builder.AppendLine($"- Legacy Risks: {JoinOrNone(dossier.LegacyRisks)}");
            builder.AppendLine($"- Coupling Score: `{dossier.CouplingScore}`");
            builder.AppendLine($"- Cohesion Score: `{dossier.CohesionScore}`");
            builder.AppendLine($"- Unknown Chain Count: `{dossier.UnknownExecutionChainCount}`");
            builder.AppendLine($"- Migration Readiness: `{dossier.MigrationReadinessScore}` ({dossier.MigrationReadinessLevel})");
            builder.AppendLine($"- Readiness Explanation: {dossier.MigrationReadinessExplanation}");
            builder.AppendLine($"- Extraction Strategy: {dossier.RecommendedFirstExtractionStrategy}");
            builder.AppendLine($"- Read-only First Extraction: `{YesNo(dossier.ReadOnlyFirstExtractionPossible)}`");
            builder.AppendLine($"- Staged Migration: `{YesNo(dossier.StagedMigrationRecommended)}`");
            builder.AppendLine($"- Major Blockers: {JoinOrNone(dossier.MajorBlockers)}");
            builder.AppendLine();
        }

        builder.AppendLine("### Conclusion Notes");
        builder.AppendLine("- Initial candidate detected from naming/path signals should be validated with dependency and table ownership evidence before extraction decisions.");
        builder.AppendLine("- Likely business boundaries with shared data and unknown chains should be migrated in staged increments, not in a single cutover.");

        return builder.ToString().TrimEnd();
    }

    private static string BuildMigrationDesignSection(MigrationExecutionContract execution)
    {
        var builder = new StringBuilder();
        var intelligence = execution.Intelligence;

        builder.AppendLine("## Migration Design");

        var domains = intelligence.BusinessDomainCandidates
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dependencies = intelligence.DependencyMatrix
            .OrderByDescending(item => item.Intensity)
            .ToList();
        var sharedTables = intelligence.SharedTables
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var riskTypes = intelligence.LegacyRiskDetails
            .OrderByDescending(item => item.ImpactedFiles.Count)
            .Select(item => item.RiskType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var extractionOrder = intelligence.MigrationOrderRecommendations
            .OrderBy(item => item.Rank)
            .ToList();
        var jobsByDomain = intelligence.HangfireJobs
            .GroupBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        builder.AppendLine("### Domain Boundaries");
        builder.AppendLine($"- Final domains detected: `{domains.Count}`");
        if (domains.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var i = 0; i < domains.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Domain Boundary Continuation ({i + 1}-{Math.Min(i + 500, domains.Count)} of {domains.Count})_");
                }

                builder.AppendLine($"- `{domains[i]}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Background Job Migration");
        if (intelligence.HangfireJobs.Count == 0)
        {
            builder.AppendLine("- No Hangfire jobs detected for migration design.");
        }
        else
        {
            foreach (var domain in domains)
            {
                jobsByDomain.TryGetValue(domain, out var domainJobs);
                domainJobs ??= new List<HangfireJobContract>();
                var consumerJobs = domainJobs.Count(item => item.Category == HangfireJobCategory.ConsumerJob);
                var scheduledJobs = domainJobs.Count(item => item.Category == HangfireJobCategory.ScheduledJob);
                var triggeredJobs = domainJobs.Count(item => item.Category == HangfireJobCategory.TriggeredJob);
                var producerJobs = domainJobs.Count(item => item.Category == HangfireJobCategory.ProducerJob);
                var normalJobs = scheduledJobs + triggeredJobs + producerJobs;
                var legacyHosted = domainJobs.Count(item => item.MustRemainInMonolithInitially);
                var canMove = domainJobs.Count(item => item.CanMoveWithService);
                var schedulingDependencies = domainJobs
                    .Select(item => item.ScheduleSource)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var domainRelationships = intelligence.ProducerConsumerRelationships
                    .Where(item => item.DomainOwner.Equals(domain, StringComparison.OrdinalIgnoreCase))
                    .Select(item => $"{item.ProducerJob}->{item.ConsumerJob} ({item.RelationshipType})")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                builder.AppendLine($"- `{domain}`: consumer `{consumerJobs}`, scheduled `{scheduledJobs}`, triggered `{triggeredJobs}`, producer `{producerJobs}`, normal `{normalJobs}`, legacy-schedule `{legacyHosted}`, move-with-service `{canMove}`");
                builder.AppendLine($"  scheduling-source: {JoinOrNone(schedulingDependencies, "; ")}");
                builder.AppendLine($"  producer-consumer: {JoinOrNone(domainRelationships, "; ")}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Domain Dependencies");
        if (dependencies.Count == 0)
        {
            builder.AppendLine("- No strong domain dependencies inferred.");
        }
        else
        {
            for (var i = 0; i < dependencies.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Dependency Continuation ({i + 1}-{Math.Min(i + 500, dependencies.Count)} of {dependencies.Count})_");
                }

                var dependency = dependencies[i];
                builder.AppendLine($"- `{dependency.FromDomain}` -> `{dependency.ToDomain}` ({dependency.DependencyKind}, intensity `{dependency.Intensity}`)");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Database Split Strategy");
        builder.AppendLine($"- Shared tables requiring staged strategy: `{sharedTables.Count}`");
        if (sharedTables.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var i = 0; i < sharedTables.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Shared Table Continuation ({i + 1}-{Math.Min(i + 500, sharedTables.Count)} of {sharedTables.Count})_");
                }

                builder.AppendLine($"- `{sharedTables[i]}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Recommended Extraction Order");
        if (extractionOrder.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var i = 0; i < extractionOrder.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Extraction Order Continuation ({i + 1}-{Math.Min(i + 500, extractionOrder.Count)} of {extractionOrder.Count})_");
                }

                var item = extractionOrder[i];
                builder.AppendLine($"- `{item.Rank}`. `{item.CandidateName}` - {item.WhyFirstOrLater}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### Migration Risks");
        if (riskTypes.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            for (var i = 0; i < riskTypes.Count; i++)
            {
                if (i > 0 && i % 500 == 0)
                {
                    builder.AppendLine($"- _Risk Continuation ({i + 1}-{Math.Min(i + 500, riskTypes.Count)} of {riskTypes.Count})_");
                }

                builder.AppendLine($"- `{riskTypes[i]}`");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string JoinOrNone(IEnumerable<string> values, string separator = ", ")
    {
        var list = values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count == 0 ? "none" : string.Join(separator, list);
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string ValueOrUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private static string EscapePipes(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string SanitizeMermaidNode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "UnknownNode";
        }

        var sanitized = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "UnknownNode" : sanitized;
    }
}
