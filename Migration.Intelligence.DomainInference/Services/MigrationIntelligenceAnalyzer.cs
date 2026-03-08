using System.Text;
using System.Text.RegularExpressions;
using Migration.Intelligence.Contracts.Common;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.Core.Utilities;

namespace Migration.Intelligence.DomainInference.Services;

public sealed class MigrationIntelligenceAnalyzer : IMigrationIntelligenceAnalyzer
{
    private static readonly HashSet<string> TechnicalNonDomainNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Api", "Web", "ApiGateway", "Asset", "Hangfire", "HttpClients",
        "Gateway", "Ui", "Core", "Data", "Domain", "Infrastructure", "Helper",
        "Helpers", "Shared", "Common", "Library", "Libraries", "Integration", "Service", "Services", "Repository", "Repositories",
        "Host", "Hosting", "Bootstrap", "Config", "Configuration", "Runner",
        "Background", "Worker", "Consumer", "Producer", "Base", "Generic",
        "Cluster", "Candidate", "Evidence", "Builder", "Analyzer", "Factory", "Manager",
        "Dependency", "Dependencies", "Scanner", "Discovery", "Migration", "Scoring", "Score",
        "Contract", "Contracts", "Pipeline", "Engine", "Context"
    };

    private static readonly HashSet<string> HostTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "web", "gateway", "hangfire", "worker", "ui", "host", "runner", "console", "job"
    };

    private static readonly HashSet<string> InfrastructureTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "core", "helper", "shared", "common", "data", "domain", "infra", "infrastructure", "utility", "config",
        "configuration", "bootstrap", "base", "generic", "abstraction", "contract", "dto", "model", "entity"
    };

    private static readonly HashSet<string> IntegrationTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "httpclient", "httpclients", "integration", "client", "adapter", "connector", "event", "queue", "bus",
        "kafka", "rabbit", "consumer", "producer", "bridge", "proxy"
    };

    private static readonly HashSet<string> InvalidDomainTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "B", "C", "P", "R", "X", "V", "Ct", "SET",
        "Select", "From", "Join", "Where", "And", "Or", "As",
        "Table", "Entity", "Model", "Dto", "Item", "Cluster", "Candidate", "Evidence", "Builder", "Analyzer",
        "Dependency", "Dependencies", "Scanner", "Discovery", "Migration", "Scoring", "Score",
        "Contract", "Contracts", "Pipeline", "Engine", "Context"
    };

    private static readonly HashSet<string> SqlKeywordTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "SET", "FROM", "JOIN", "WHERE", "GROUP", "ORDER", "HAVING", "INTO", "UPDATE",
        "DELETE", "INSERT", "MERGE", "TOP", "DISTINCT", "AS", "ON", "WITH", "CASE", "WHEN", "THEN", "END"
    };

    private static readonly HashSet<string> SqlAliasTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "A", "B", "C", "P", "R", "X", "V", "T", "S", "CT", "CTE"
    };

    private static readonly HashSet<string> InvalidTableTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "dbo", "db", "sys", "schema", "information_schema", "tempdb",
        "cte", "result", "results", "value", "values", "record", "records",
        "tmp", "temp", "dual", "rownum", "rowid"
    };

    private static readonly HashSet<string> IgnoredControllerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseController", "BaseApiController", "GenericController", "CommonController", "ApiController"
    };

    private static readonly HashSet<string> IgnoredServiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseService", "CommonService", "GenericService"
    };

    private static readonly HashSet<string> IgnoredRepositoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseRepository", "RepositoryBase"
    };

    private static readonly HashSet<string> ReadOnlyAccessTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "read", "select", "query", "get", "find", "list"
    };

    private static readonly HashSet<string> WriteAccessTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "write", "insert", "update", "delete", "upsert", "save", "create"
    };

    private static readonly HashSet<string> JobSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Job", "Consumer", "Handler", "Worker", "Runner", "Processor"
    };

    private static readonly HashSet<string> ConsumerIndicatorTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "consumer", "consume", "queue", "event", "handler", "kafka", "rabbit", "bus", "message", "topic"
    };

    private static readonly HashSet<string> ProducerIndicatorTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "producer", "publish", "enqueue", "dispatch", "emit", "send", "produce"
    };

    private static readonly HashSet<string> NonJobClassSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dto", "Request", "Response", "Map", "Mapper", "Entity", "Model", "Command", "Query",
        "Options", "Option", "Config", "Configuration", "Settings", "Constants", "Enum", "Helper",
        "Converter", "Factory", "Builder", "Context", "Client", "Wrapper", "Result", "Data",
        "Info", "Attribute", "Exception", "Tests", "Test", "Validator", "Profile"
    };

    private static readonly HashSet<string> IgnoredJobClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ArabamJob", "ConsumerJob", "BaseJob", "JobBase"
    };

    private static readonly Regex ClassRegex =
        new(@"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex =
        new(@"^\s*namespace\s+(?<name>[A-Za-z0-9_.]+)", RegexOptions.Compiled | RegexOptions.Multiline);

    private const string ConstructorRegexTemplate = @"public\s+{0}\s*\((?<params>[^)]*)\)";

    private static readonly Regex ClassInheritanceRegex =
        new(@"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<bases>[^{\r\n]+)", RegexOptions.Compiled);

    private static readonly Regex DiRegistrationRegex =
        new(@"Add(?:Scoped|Transient|Singleton)\s*<\s*(?<interface>[A-Za-z0-9_.]+)\s*,\s*(?<implementation>[A-Za-z0-9_.]+)\s*>", RegexOptions.Compiled);

    private static readonly Regex NewRegex =
        new(@"new\s+(?<type>[A-Za-z_][A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);

    private static readonly Regex UsingRegex =
        new(@"^\s*using\s+(?<ns>[A-Za-z0-9_.]+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex SqlTableRegex =
        new(@"\b(?<verb>from|join|into|update|merge\s+into|delete\s+from)\s+(?<table>(?:\[?[A-Za-z_][A-Za-z0-9_]*\]?\.)?(?:\#?\[?[A-Za-z_][A-Za-z0-9_]*\]?))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DbSetRegex =
        new(@"\bDbSet<(?<entity>[A-Za-z_][A-Za-z0-9_]*)>", RegexOptions.Compiled);

    private static readonly Regex CteRegex =
        new(@"\bwith\s+(?<cte>[A-Za-z_][A-Za-z0-9_]*)\s+as\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CteContinuationRegex =
        new(@",\s*(?<cte>[A-Za-z_][A-Za-z0-9_]*)\s+as\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ClassRouteRegex =
        new(@"\[Route\(\s*""(?<route>[^""]+)""\s*\)\]", RegexOptions.Compiled);

    private static readonly Regex HttpAttributeRegex =
        new(@"\[Http(?<method>Get|Post|Put|Patch|Delete|Head|Options)(?:\(\s*""(?<route>[^""]*)""\s*\))?\]", RegexOptions.Compiled);

    private static readonly Regex ActionRegex =
        new(@"public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|IActionResult|ActionResult(?:<[^>]+>)?|IResult|JsonResult|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    private static readonly Regex JobMethodRegex =
        new(@"\bpublic\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void|bool|int|long|string|decimal|double|float)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    private static readonly Regex RecurringJobRegistrationRegex =
        new(@"\bAddOrUpdate(?:<(?<jobType>[A-Za-z0-9_.]+)>)?\s*\((?<args>[\s\S]{0,800}?)\)\s*;", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BackgroundJobRegistrationRegex =
        new(@"\b(?<operation>Enqueue|Schedule|ContinueJobWith)(?:<(?<jobType>[A-Za-z0-9_.]+)>)?\s*\((?<args>[\s\S]{0,800}?)\)\s*;", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LambdaJobCallRegex =
        new(@"=>\s*(?<instance>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    private static readonly Regex StaticJobCallRegex =
        new(@"(?<class>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    private static readonly Regex CronLiteralRegex =
        new(@"(?<cron>(?:\*|[0-9\/,\-\?]+)(?:\s+(?:\*|[0-9\/,\-\?]+)){4,6})", RegexOptions.Compiled);

    private static readonly Regex MessageOrEventTypeRegex =
        new(@"\b(?<type>[A-Za-z_][A-Za-z0-9_]*(?:Message|Event|Dto|Command|Notification))\b", RegexOptions.Compiled);

    private static readonly Regex QueueAttributeRegex =
        new(@"\[Queue\(\s*""(?<queue>[^""]+)""\s*\)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AppSettingLookupRegex =
        new(@"AppSettings\s*\[\s*""(?<key>[^""]+)""\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ConfigAddRegex =
        new(@"<add\s+key\s*=\s*""(?<key>[^""]+)""\s+value\s*=\s*""(?<value>[^""]+)""\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JsonStringPairRegex =
        new(@"""(?<key>[^""]+)""\s*:\s*""(?<value>[^""]+)""", RegexOptions.Compiled);

    private static readonly Regex ConstStringRegex =
        new(@"\bconst\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]+)""\s*;", RegexOptions.Compiled);

    private static readonly Regex StaticReadonlyStringRegex =
        new(@"\b(?:public|private|internal|protected)?\s*static\s+readonly\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]+)""\s*;", RegexOptions.Compiled);

    private static readonly Regex NamedQueueRegex =
        new(@"queue\s*:\s*""(?<queue>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnqueuedStateQueueRegex =
        new(@"EnqueuedState\s*\(\s*""(?<queue>[^""]+)""\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<MigrationIntelligenceContract> AnalyzeAsync(
        RepositoryInventoryContract inventory,
        CodeInsightsContract insights,
        IReadOnlyCollection<ServiceBlueprintContract> serviceBlueprints,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var fileObservations = await BuildFileObservationsAsync(inventory, options, cancellationToken);

        var controllerObservations = fileObservations
            .Where(file => !string.IsNullOrWhiteSpace(file.ControllerName))
            .ToList();

        var serviceObservations = fileObservations
            .Where(file => !string.IsNullOrWhiteSpace(file.ServiceName))
            .ToList();

        var repositoryObservations = fileObservations
            .Where(file => !string.IsNullOrWhiteSpace(file.RepositoryName))
            .ToList();

        var consolidation = DetectBusinessDomainCandidates(
            serviceBlueprints,
            controllerObservations,
            serviceObservations,
            repositoryObservations,
            fileObservations);
        var businessDomains = consolidation.Domains;
        var domainSet = businessDomains.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var componentClassifications = BuildComponentClassifications(inventory, serviceBlueprints, domainSet);
        var endpointMappings = BuildEndpointMappings(controllerObservations, businessDomains, consolidation.AliasToDomainMap);
        var repositoryTableMappings = BuildRepositoryTableMappings(repositoryObservations, businessDomains, consolidation.AliasToDomainMap);
        var tableOwnership = BuildTableOwnership(repositoryTableMappings);
        var hangfireJobs = await BuildHangfireJobsAsync(
            inventory,
            fileObservations,
            businessDomains,
            consolidation.AliasToDomainMap,
            repositoryTableMappings,
            options,
            cancellationToken);
        var producerConsumerRelationships = BuildProducerConsumerRelationships(hangfireJobs);
        var executionChains = BuildExecutionChains(
            controllerObservations,
            serviceObservations,
            repositoryObservations,
            fileObservations,
            repositoryTableMappings,
            businessDomains,
            consolidation.AliasToDomainMap);
        var dependencyMatrix = BuildDependencyMatrix(fileObservations, businessDomains, endpointMappings, repositoryTableMappings);
        dependencyMatrix = EnrichDependencyMatrixWithHangfireJobs(dependencyMatrix, hangfireJobs);
        var externalDependencyMaps = BuildExternalDependencyMaps(
            fileObservations,
            businessDomains,
            endpointMappings,
            repositoryTableMappings,
            dependencyMatrix);
        var workflows = BuildWorkflowAnalyses(endpointMappings, executionChains);
        var legacyRisks = BuildLegacyRiskDetails(fileObservations, businessDomains, endpointMappings, executionChains);
        var sharedKernelItems = BuildSharedKernelItems(fileObservations, businessDomains);
        var backgroundJobValidation = BuildBackgroundJobValidation(hangfireJobs, producerConsumerRelationships);
        var dossiers = BuildServiceDossiers(
            businessDomains,
            endpointMappings,
            executionChains,
            repositoryTableMappings,
            tableOwnership,
            dependencyMatrix,
            externalDependencyMaps,
            workflows,
            hangfireJobs,
            legacyRisks,
            sharedKernelItems,
            fileObservations,
            consolidation.DomainHierarchy);
        var migrationOrder = BuildMigrationOrderRecommendations(dossiers);
        var domainValidation = BuildDomainEnumerationValidation(
            businessDomains,
            dossiers,
            endpointMappings,
            dependencyMatrix);

        return new MigrationIntelligenceContract
        {
            DomainEnumerationValidation = domainValidation,
            ComponentClassifications = componentClassifications,
            BusinessDomainCandidates = businessDomains,
            DomainHierarchies = consolidation.DomainHierarchy
                .Select(item => new DomainHierarchyContract
                {
                    Domain = item.Key,
                    Subdomains = item.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .OrderBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            EndpointMappings = endpointMappings,
            ExecutionChains = executionChains,
            RepositoryTableMappings = repositoryTableMappings,
            TableOwnerships = tableOwnership,
            SharedTables = tableOwnership.Where(item => item.IsShared).Select(item => item.TableName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            OwnerlessOrAmbiguousTables = tableOwnership
                .Where(item => string.IsNullOrWhiteSpace(item.OwnerDomain) || item.Confidence < 0.55)
                .Select(item => item.TableName)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DependencyMatrix = dependencyMatrix,
            ExternalDependencyMaps = externalDependencyMaps,
            Workflows = workflows,
            HangfireJobs = hangfireJobs,
            ProducerConsumerRelationships = producerConsumerRelationships,
            BackgroundJobValidation = backgroundJobValidation,
            LegacyRiskDetails = legacyRisks,
            ServiceDossiers = dossiers,
            MigrationOrderRecommendations = migrationOrder,
            SharedKernelItems = sharedKernelItems
        };
    }

    private static async Task<List<FileObservation>> BuildFileObservationsAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var observations = new List<FileObservation>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!options.CodeAnalysis.IncludeGeneratedFiles
                && (sourceFile.RelativePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                    || sourceFile.RelativePath.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var classNames = ClassRegex.Matches(content)
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var namespaceName = NamespaceRegex.Match(content).Success
                ? NamespaceRegex.Match(content).Groups["name"].Value
                : string.Empty;

            var primaryClass = classNames.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(sourceFile.RelativePath);
            var controllerName = classNames.FirstOrDefault(name =>
                name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) && !IsFrameworkController(name));
            var serviceName = classNames.FirstOrDefault(name =>
                name.EndsWith("Service", StringComparison.OrdinalIgnoreCase) && !IsFrameworkService(name));
            var repositoryName = classNames.FirstOrDefault(name =>
                name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) && !IsFrameworkRepository(name));
            var implementedInterfaces = ExtractImplementedInterfaces(content);

            var constructorDependencies = ExtractConstructorDependencies(content, classNames);
            foreach (var dependency in NewRegex.Matches(content).Select(match => match.Groups["type"].Value))
            {
                constructorDependencies.Add(NormalizeTypeName(dependency));
            }

            var endpoints = string.IsNullOrWhiteSpace(controllerName)
                ? new List<EndpointObservation>()
                : ExtractEndpoints(content, controllerName);
            endpoints = EnrichEndpointExposure(endpoints, sourceFile.RelativePath, namespaceName, content);

            var tables = ExtractTableObservations(content, repositoryName);
            var riskTypes = DetectLegacyRiskTypes(content);
            var externalDependencies = ExtractExternalDependencies(content, constructorDependencies);
            var diRegistrations = ExtractDiRegistrations(content);

            observations.Add(new FileObservation
            {
                RelativePath = sourceFile.RelativePath,
                NamespaceName = namespaceName,
                ClassNames = classNames,
                PrimaryClassName = primaryClass,
                ControllerName = controllerName,
                ServiceName = serviceName,
                RepositoryName = repositoryName,
                ConstructorDependencies = constructorDependencies.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Endpoints = endpoints,
                TableObservations = tables,
                LegacyRiskTypes = riskTypes,
                ExternalDependencies = externalDependencies,
                ProjectName = ExtractProjectName(sourceFile.RelativePath),
                ImplementedInterfaces = implementedInterfaces,
                DiRegistrations = diRegistrations
            });
        }

        return observations;
    }

    private static DomainConsolidationResult DetectBusinessDomainCandidates(
        IReadOnlyCollection<ServiceBlueprintContract> serviceBlueprints,
        IReadOnlyCollection<FileObservation> controllerObservations,
        IReadOnlyCollection<FileObservation> serviceObservations,
        IReadOnlyCollection<FileObservation> repositoryObservations,
        IReadOnlyCollection<FileObservation> allFiles)
    {
        var evidenceMap = new Dictionary<string, CandidateEvidence>(StringComparer.OrdinalIgnoreCase);

        foreach (var blueprint in serviceBlueprints)
        {
            var candidate = NormalizeCandidateName(blueprint.ServiceName);
            if (string.IsNullOrWhiteSpace(candidate) || IsInvalidDomainCandidateName(candidate))
            {
                continue;
            }

            var evidence = GetOrCreateCandidate(evidenceMap, candidate);
            evidence.WeakSignalScore += 2;
            evidence.Reasons.Add("Initial candidate detected from naming/path signals.");
        }

        foreach (var controller in controllerObservations)
        {
            if (string.IsNullOrWhiteSpace(controller.ControllerName))
            {
                continue;
            }

            var candidate = NormalizeCandidateName(RemoveSuffix(controller.ControllerName, "Controller"));
            if (string.IsNullOrWhiteSpace(candidate) || IsInvalidDomainCandidateName(candidate))
            {
                continue;
            }

            var evidence = GetOrCreateCandidate(evidenceMap, candidate);
            evidence.ControllerCount++;
            evidence.StructuralScore += 8;
            evidence.Reasons.Add("Detected via controller clustering.");
        }

        foreach (var service in serviceObservations)
        {
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                continue;
            }

            var candidate = NormalizeCandidateName(RemoveSuffix(service.ServiceName, "Service"));
            if (string.IsNullOrWhiteSpace(candidate) || IsInvalidDomainCandidateName(candidate))
            {
                continue;
            }

            var evidence = GetOrCreateCandidate(evidenceMap, candidate);
            evidence.ServiceCount++;
            evidence.StructuralScore += 5;
            evidence.Reasons.Add("Detected via service class naming plus constructor graph.");

            var namespaceCandidate = ExtractNamespaceCandidate(service.NamespaceName);
            if (!string.IsNullOrWhiteSpace(namespaceCandidate) && !IsInvalidDomainCandidateName(namespaceCandidate))
            {
                var nsEvidence = GetOrCreateCandidate(evidenceMap, namespaceCandidate);
                nsEvidence.StructuralScore += 2;
                nsEvidence.Reasons.Add("Detected via namespace clustering.");
            }
        }

        foreach (var repository in repositoryObservations)
        {
            if (string.IsNullOrWhiteSpace(repository.RepositoryName))
            {
                continue;
            }

            var candidate = NormalizeCandidateName(RemoveSuffix(repository.RepositoryName, "Repository"));
            if (string.IsNullOrWhiteSpace(candidate) || IsInvalidDomainCandidateName(candidate))
            {
                continue;
            }

            var evidence = GetOrCreateCandidate(evidenceMap, candidate);
            evidence.RepositoryCount++;
            evidence.StructuralScore += 6;
            evidence.Reasons.Add("Detected via repository ownership signals.");

            foreach (var table in repository.TableObservations)
            {
                if (!IsLikelyRealTable(table.TableName))
                {
                    continue;
                }

                var tableCandidate = NormalizeCandidateName(NormalizeTableName(table.TableName));
                if (string.IsNullOrWhiteSpace(tableCandidate) || IsInvalidDomainCandidateName(tableCandidate))
                {
                    continue;
                }

                var tableEvidence = GetOrCreateCandidate(evidenceMap, tableCandidate);
                tableEvidence.TableCount++;
                tableEvidence.StructuralScore += table.Confidence >= 0.75 ? 3 : 1;
                tableEvidence.Reasons.Add("Detected via table usage co-occurrence.");
            }
        }

        foreach (var file in allFiles)
        {
            foreach (var entityCandidate in ExtractEntityCandidates(file))
            {
                if (IsInvalidDomainCandidateName(entityCandidate))
                {
                    continue;
                }

                var evidence = GetOrCreateCandidate(evidenceMap, entityCandidate);
                evidence.StructuralScore += 2;
                evidence.Reasons.Add("Detected via entity/model naming.");
            }

            foreach (var routeCandidate in file.Endpoints
                         .SelectMany(endpoint => ExtractRouteCandidates(endpoint.RoutePrefix).Concat(ExtractRouteCandidates(endpoint.Route))))
            {
                if (IsInvalidDomainCandidateName(routeCandidate))
                {
                    continue;
                }

                var evidence = GetOrCreateCandidate(evidenceMap, routeCandidate);
                evidence.StructuralScore += 4;
                evidence.ControllerCount += file.ControllerName is null ? 0 : 1;
                evidence.Reasons.Add("Detected via route prefix ownership signal.");
            }

            if (LooksLikeHangfireRelatedFile(file))
            {
                foreach (var className in file.ClassNames.Where(LooksLikeJobToken))
                {
                    var jobCandidate = NormalizeJobRootName(className);
                    if (string.IsNullOrWhiteSpace(jobCandidate) || IsInvalidDomainCandidateName(jobCandidate))
                    {
                        continue;
                    }

                    var jobEvidence = GetOrCreateCandidate(evidenceMap, jobCandidate);
                    jobEvidence.StructuralScore += 5;
                    jobEvidence.ServiceCount += 1;
                    jobEvidence.Reasons.Add("Detected via Hangfire background job ownership signal.");
                }
            }
        }

        foreach (var service in serviceObservations)
        {
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                continue;
            }

            var sourceCandidate = NormalizeCandidateName(RemoveSuffix(service.ServiceName, "Service"));
            if (IsInvalidDomainCandidateName(sourceCandidate))
            {
                continue;
            }

            foreach (var dependency in service.ConstructorDependencies.Where(IsServiceType))
            {
                var targetCandidate = NormalizeCandidateName(RemoveSuffix(dependency, "Service"));
                if (IsInvalidDomainCandidateName(targetCandidate))
                {
                    continue;
                }

                var sourceEvidence = GetOrCreateCandidate(evidenceMap, sourceCandidate);
                sourceEvidence.StructuralScore += 1;
                sourceEvidence.Reasons.Add("Detected via inter-service call pattern.");

                var targetEvidence = GetOrCreateCandidate(evidenceMap, targetCandidate);
                targetEvidence.StructuralScore += 1;
                targetEvidence.Reasons.Add("Detected via incoming service call pattern.");
            }
        }

        var selected = evidenceMap.Values
            .Where(candidate => IsLikelyBusinessCandidate(candidate))
            .Where(candidate => !IsTechnicalNonDomain(candidate.Name))
            .Where(candidate => candidate.StructuralScore >= 7)
            .Where(candidate => candidate.ControllerCount > 0 || candidate.RepositoryCount > 0 || candidate.ServiceCount > 0)
            .OrderByDescending(candidate => candidate.StructuralScore)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selected.Count == 0)
        {
            selected = evidenceMap.Values
                .Where(candidate => IsLikelyBusinessCandidate(candidate))
                .Where(candidate => !IsTechnicalNonDomain(candidate.Name))
                .OrderByDescending(candidate => candidate.StructuralScore + candidate.WeakSignalScore)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
        }

        if (selected.Count == 0)
        {
            selected = serviceBlueprints
                .Select(blueprint => NormalizeCandidateName(blueprint.ServiceName))
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Where(candidate => !IsInvalidDomainCandidateName(candidate))
                .Where(candidate => !IsTechnicalNonDomain(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(candidate => new CandidateEvidence(candidate)
                {
                    StructuralScore = 3,
                    WeakSignalScore = 2
                })
                .ToList();
        }

        return ConsolidateDomainCandidates(selected);
    }

    private static List<ComponentClassificationContract> BuildComponentClassifications(
        RepositoryInventoryContract inventory,
        IReadOnlyCollection<ServiceBlueprintContract> serviceBlueprints,
        ISet<string> domainCandidates)
    {
        var results = new List<ComponentClassificationContract>();

        foreach (var project in inventory.Projects)
        {
            var name = NormalizeCandidateName(project.Name);
            var category = ClassifyComponent(name);

            if (domainCandidates.Contains(name))
            {
                category = ComponentCategory.BusinessDomainCandidate;
            }

            results.Add(new ComponentClassificationContract
            {
                ComponentName = project.Name,
                Category = category,
                Evidence = BuildClassificationEvidence(project.Name, category),
                ValidationStatus = category == ComponentCategory.BusinessDomainCandidate
                    ? "Likely business boundary, but shared data and contracts must be hardened first."
                    : "Initial candidate detected from naming/path signals. Requires validation via dependency and table ownership analysis."
            });
        }

        foreach (var blueprint in serviceBlueprints)
        {
            if (results.Any(item => item.ComponentName.Equals(blueprint.ServiceName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var category = domainCandidates.Contains(blueprint.ServiceName)
                ? ComponentCategory.BusinessDomainCandidate
                : ClassifyComponent(blueprint.ServiceName);

            results.Add(new ComponentClassificationContract
            {
                ComponentName = blueprint.ServiceName,
                Category = category,
                Evidence = BuildClassificationEvidence(blueprint.ServiceName, category),
                ValidationStatus = category == ComponentCategory.BusinessDomainCandidate
                    ? "Likely business boundary, but shared data and contracts must be hardened first."
                    : "Initial candidate detected from naming/path signals. Requires validation via dependency and table ownership analysis."
            });
        }

        return results
            .OrderBy(item => item.Category)
            .ThenBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    private static List<EndpointMappingContract> BuildEndpointMappings(
        IReadOnlyCollection<FileObservation> controllerObservations,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyDictionary<string, string> domainAliasMap)
    {
        var results = new List<EndpointMappingContract>();

        foreach (var controller in controllerObservations)
        {
            if (string.IsNullOrWhiteSpace(controller.ControllerName))
            {
                continue;
            }

            if (IsFrameworkController(controller.ControllerName))
            {
                continue;
            }

            var controllerCandidate = NormalizeCandidateName(RemoveSuffix(controller.ControllerName, "Controller"));
            if (IsInvalidDomainCandidateName(controllerCandidate))
            {
                continue;
            }

            var domainFromName = ResolveDomainFromName(controllerCandidate, domainCandidates);
            var domainFromRoute = ResolveDomainFromRoute(controller.Endpoints.Select(endpoint => endpoint.RoutePrefix), domainCandidates);
            var domainFromAlias = domainAliasMap.GetValueOrDefault(controllerCandidate, string.Empty);
            var domain = domainFromName
                         ?? domainFromRoute
                         ?? domainFromAlias
                         ?? "Unmapped";

            domain = NormalizeConsolidatedDomain(domain, domainAliasMap);
            var ownershipConfidence = CalculateEndpointOwnershipConfidence(domainFromName, domainFromRoute, domainFromAlias, domain);

            if (controller.Endpoints.Count == 0)
            {
                continue;
            }

            foreach (var endpoint in controller.Endpoints)
            {
                results.Add(new EndpointMappingContract
                {
                    DomainCandidate = domain,
                    Controller = controller.ControllerName,
                    Action = endpoint.Action,
                    HttpMethod = endpoint.HttpMethod,
                    RoutePrefix = endpoint.RoutePrefix,
                    Route = endpoint.Route,
                    Exposure = endpoint.Exposure,
                    OwnershipConfidence = ownershipConfidence
                });
            }
        }

        return results
            .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Controller, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Route, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<RepositoryTableMappingContract> BuildRepositoryTableMappings(
        IReadOnlyCollection<FileObservation> repositoryObservations,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyDictionary<string, string> domainAliasMap)
    {
        var mappings = new List<RepositoryTableMappingContract>();

        foreach (var repository in repositoryObservations)
        {
            if (string.IsNullOrWhiteSpace(repository.RepositoryName))
            {
                continue;
            }

            if (IsFrameworkRepository(repository.RepositoryName))
            {
                continue;
            }

            var repositoryCandidate = NormalizeCandidateName(RemoveSuffix(repository.RepositoryName, "Repository"));
            if (IsInvalidDomainCandidateName(repositoryCandidate))
            {
                continue;
            }

            var domain = ResolveDomainFromName(repositoryCandidate, domainCandidates)
                         ?? domainAliasMap.GetValueOrDefault(repositoryCandidate, string.Empty)
                         ?? "Unmapped";
            domain = NormalizeConsolidatedDomain(domain, domainAliasMap);
            if (repository.TableObservations.Count == 0)
            {
                var fallbackTable = NormalizeTableName(RemoveSuffix(repository.RepositoryName, "Repository"));
                if (!IsLikelyRealTable(fallbackTable))
                {
                    continue;
                }

                mappings.Add(new RepositoryTableMappingContract
                {
                    RepositoryName = repository.RepositoryName,
                    TableName = fallbackTable,
                    DomainCandidate = domain,
                    Confidence = 0.45,
                    AccessPattern = "unknown",
                    Evidence = "Inferred from repository naming. Requires validation with SQL/table ownership analysis."
                });
                continue;
            }

            foreach (var table in repository.TableObservations)
            {
                var normalizedTable = NormalizeTableName(table.TableName);
                if (!IsLikelyRealTable(normalizedTable))
                {
                    continue;
                }

                var confidence = table.Confidence;
                if (confidence < 0.3)
                {
                    continue;
                }

                mappings.Add(new RepositoryTableMappingContract
                {
                    RepositoryName = repository.RepositoryName,
                    TableName = normalizedTable,
                    DomainCandidate = domain,
                    Confidence = confidence,
                    AccessPattern = table.AccessPattern,
                    Evidence = table.Evidence
                });
            }
        }

        return mappings
            .GroupBy(item =>
                $"{item.RepositoryName}|{item.TableName}|{item.DomainCandidate}|{item.AccessPattern}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var best = group.OrderByDescending(item => item.Confidence).First();
                return new RepositoryTableMappingContract
                {
                    RepositoryName = best.RepositoryName,
                    TableName = best.TableName,
                    DomainCandidate = best.DomainCandidate,
                    Confidence = best.Confidence,
                    AccessPattern = best.AccessPattern,
                    Evidence = best.Evidence
                };
            })
            .OrderBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<HangfireJobContract>> BuildHangfireJobsAsync(
        RepositoryInventoryContract inventory,
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyDictionary<string, string> domainAliasMap,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var jobs = new List<HangfireJobContract>();
        var registrationHints = await ExtractHangfireRegistrationHintsAsync(inventory, options, cancellationToken);
        var configHints = await ExtractConfigScheduleHintsAsync(inventory, options, cancellationToken);
        var scheduleConstants = await ExtractScheduleConstantHintsAsync(inventory, options, cancellationToken);

        var scheduleByConfigKey = configHints
            .Where(item => !string.IsNullOrWhiteSpace(item.ScheduleKey)
                           && item.ResolutionStatus == HangfireScheduleResolutionStatus.Resolved
                           && !string.IsNullOrWhiteSpace(item.ScheduleExpression))
            .GroupBy(item => item.ScheduleKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().ScheduleExpression,
                StringComparer.OrdinalIgnoreCase);
        var scheduleConstantByName = scheduleConstants
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.OrdinalIgnoreCase);

        var repositoryDomainByType = repositoryMappings
            .GroupBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Confidence)
                    .Select(item => item.DomainCandidate)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
        var serviceDomainByType = files
            .Where(file => !string.IsNullOrWhiteSpace(file.ServiceName))
            .GroupBy(file => file.ServiceName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var candidate = NormalizeCandidateName(RemoveSuffix(group.Key, "Service"));
                    var resolved = ResolveDomainFromName(candidate, domainCandidates)
                                   ?? domainAliasMap.GetValueOrDefault(candidate, string.Empty)
                                   ?? "Unmapped";
                    return NormalizeConsolidatedDomain(resolved, domainAliasMap);
                },
                StringComparer.OrdinalIgnoreCase);
        var registrationByClass = registrationHints
            .Where(item => !string.IsNullOrWhiteSpace(item.JobClassName))
            .GroupBy(item => NormalizeTypeName(item.JobClassName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var configByHint = configHints
            .Where(item => !string.IsNullOrWhiteSpace(item.JobHint))
            .GroupBy(item => NormalizeCandidateName(item.JobHint), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenClassMethod = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (!LooksLikeHangfireRelatedFile(file))
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, file.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (new FileInfo(fullPath).Length > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var methodCandidates = ExtractJobMethodCandidates(content);
            var messageCandidates = ExtractMessageOrEventCandidates(content, file.ConstructorDependencies);
            var queueNameFromFile = ExtractQueueName(content);
            var producerSignal = LooksLikeProducerJob(content, file, methodCandidates);

            foreach (var className in file.ClassNames)
            {
                var normalizedClass = NormalizeTypeName(className);
                var matchingRegistration = registrationByClass.TryGetValue(normalizedClass, out var byClass)
                    ? byClass
                    : new List<JobRegistrationHint>();
                if (!IsHangfireJobClassCandidate(className, file, methodCandidates, matchingRegistration))
                {
                    continue;
                }

                var jobRoot = NormalizeJobRootName(normalizedClass);
                var matchingConfig = configByHint.TryGetValue(jobRoot, out var byHint)
                    ? byHint
                    : new List<ConfigScheduleHint>();
                var category = ResolveJobCategory(normalizedClass, file, content, methodCandidates, messageCandidates, matchingRegistration, producerSignal);
                var trigger = ResolveTriggerType(category, matchingRegistration);
                var methodName = ResolveJobMethodName(methodCandidates, matchingRegistration);
                var consumedMessage = ResolveConsumedMessage(messageCandidates, matchingRegistration);
                var producedMessage = ResolveProducedMessage(content, matchingRegistration, methodCandidates);
                var queueName = ResolveQueueName(queueNameFromFile, matchingRegistration);
                var scheduleResolution = ResolveScheduleResolution(
                    matchingRegistration,
                    matchingConfig,
                    scheduleByConfigKey,
                    scheduleConstantByName);
                category = FinalizeJobCategory(category, trigger, scheduleResolution);
                trigger = FinalizeTriggerType(category, trigger, scheduleResolution);
                scheduleResolution = NormalizeScheduleResolutionForCategory(category, scheduleResolution);
                var scheduleSource = ResolveScheduleSource(file.RelativePath, matchingRegistration, matchingConfig);
                var registrationSource = ResolveRegistrationSource(file.RelativePath, matchingRegistration);
                var ownership = InferHangfireJobDomain(
                    normalizedClass,
                    jobRoot,
                    file,
                    messageCandidates,
                    domainCandidates,
                    domainAliasMap,
                    serviceDomainByType,
                    repositoryDomainByType);

                var domain = ownership.Domain;
                var isInfrastructureOnly = domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                                           && IsInfrastructureJobClass(normalizedClass, file.RelativePath);
                if (isInfrastructureOnly)
                {
                    domain = "Infrastructure";
                }

                var legacyHosting = EvaluateLegacyHosting(scheduleSource, file.RelativePath, matchingRegistration);
                var mustRemainInMonolith = legacyHosting.IsLegacyHosted;
                var canMoveWithService = !isInfrastructureOnly
                                         && !mustRemainInMonolith
                                         && !domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                                         && !domain.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase);
                var key = $"{normalizedClass}|{methodName}|{domain}|{category}";
                if (!seen.Add(key))
                {
                    continue;
                }
                seenClassMethod.Add($"{normalizedClass}|{methodName}");

                jobs.Add(new HangfireJobContract
                {
                    JobName = normalizedClass,
                    ClassName = normalizedClass,
                    MethodName = methodName,
                    RelativePath = file.RelativePath,
                    RegistrationSource = registrationSource,
                    DomainOwner = domain,
                    Category = category,
                    TriggerType = trigger,
                    QueueName = queueName,
                    QueueOrTopic = queueName,
                    ConsumedMessageType = consumedMessage,
                    ProducedMessageType = producedMessage,
                    RelatedMessageOrEvent = consumedMessage,
                    ScheduleSource = scheduleSource,
                    ScheduleExpression = scheduleResolution.ResolvedScheduleExpression,
                    RawScheduleKey = scheduleResolution.RawScheduleKey,
                    ResolvedScheduleExpression = scheduleResolution.ResolvedScheduleExpression,
                    ScheduleSourceType = scheduleResolution.SourceType,
                    ScheduleResolutionStatus = scheduleResolution.Status,
                    ScheduleResolutionNote = scheduleResolution.Note,
                    IsInfrastructureOnly = isInfrastructureOnly,
                    MustRemainInMonolithInitially = mustRemainInMonolith,
                    IsLegacyHosted = legacyHosting.IsLegacyHosted,
                    LegacyHostingReason = legacyHosting.Reason,
                    NeedsRefactorBeforeMigration = legacyHosting.IsLegacyHosted,
                    CanMoveWithService = canMoveWithService,
                    OwnershipConfidence = ownership.Confidence,
                    TopCandidateDomains = ownership.TopCandidates,
                    UnmappedReason = ownership.UnmappedReason,
                    DependentDomains = ownership.DependentDomains,
                    Evidence = ownership.Evidence
                });
            }
        }

        foreach (var registration in registrationHints)
        {
            if (string.IsNullOrWhiteSpace(registration.JobClassName))
            {
                continue;
            }

            var className = NormalizeTypeName(registration.JobClassName);
            var methodName = string.IsNullOrWhiteSpace(registration.MethodName) ? "Execute" : registration.MethodName;
            if (IsExplicitlyExcludedJobClass(className))
            {
                continue;
            }

            if (seenClassMethod.Contains($"{className}|{methodName}"))
            {
                continue;
            }

            var jobRoot = NormalizeJobRootName(className);
            var matchingConfig = configByHint.TryGetValue(jobRoot, out var configMatches)
                ? configMatches
                : new List<ConfigScheduleHint>();
            var scheduleResolution = ResolveScheduleResolution(
                new[] { registration },
                matchingConfig,
                scheduleByConfigKey,
                scheduleConstantByName);
            var inferredDomain = ResolveDomainFromName(jobRoot, domainCandidates)
                                 ?? domainAliasMap.GetValueOrDefault(jobRoot, string.Empty)
                                 ?? "Unmapped";
            inferredDomain = NormalizeConsolidatedDomain(inferredDomain, domainAliasMap);
            var isInfrastructureOnly = inferredDomain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                                       && IsInfrastructureJobClass(className, registration.SourcePath);
            var domain = isInfrastructureOnly ? "Infrastructure" : inferredDomain;
            var legacyHosting = EvaluateLegacyHosting(registration.SourcePath, registration.SourcePath, new[] { registration });
            var mustRemainInMonolith = legacyHosting.IsLegacyHosted;
            var canMoveWithService = !isInfrastructureOnly
                                     && !mustRemainInMonolith
                                     && !domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase);

            var category = registration.CategoryHint;
            if (category == HangfireJobCategory.TriggeredJob
                && LooksLikeConsumerJobName(className))
            {
                category = HangfireJobCategory.ConsumerJob;
            }
            var trigger = ResolveTriggerType(category, new[] { registration });
            category = FinalizeJobCategory(category, trigger, scheduleResolution);
            trigger = FinalizeTriggerType(category, trigger, scheduleResolution);
            scheduleResolution = NormalizeScheduleResolutionForCategory(category, scheduleResolution);

            var key = $"{className}|{methodName}|{domain}|{category}";
            if (!seen.Add(key))
            {
                continue;
            }

            jobs.Add(new HangfireJobContract
            {
                JobName = className,
                ClassName = className,
                MethodName = methodName,
                RelativePath = registration.SourcePath,
                RegistrationSource = registration.RegistrationSource,
                DomainOwner = domain,
                Category = category,
                TriggerType = trigger,
                QueueName = registration.QueueName,
                QueueOrTopic = registration.QueueName,
                ConsumedMessageType = registration.RelatedMessageOrEvent,
                ProducedMessageType = registration.ProducedMessageOrEvent,
                RelatedMessageOrEvent = registration.RelatedMessageOrEvent,
                ScheduleSource = registration.SourcePath,
                ScheduleExpression = scheduleResolution.ResolvedScheduleExpression,
                RawScheduleKey = scheduleResolution.RawScheduleKey,
                ResolvedScheduleExpression = scheduleResolution.ResolvedScheduleExpression,
                ScheduleSourceType = scheduleResolution.SourceType,
                ScheduleResolutionStatus = scheduleResolution.Status,
                ScheduleResolutionNote = scheduleResolution.Note,
                IsInfrastructureOnly = isInfrastructureOnly,
                MustRemainInMonolithInitially = mustRemainInMonolith,
                IsLegacyHosted = legacyHosting.IsLegacyHosted,
                LegacyHostingReason = legacyHosting.Reason,
                NeedsRefactorBeforeMigration = legacyHosting.IsLegacyHosted,
                CanMoveWithService = canMoveWithService,
                OwnershipConfidence = isInfrastructureOnly ? 0.45 : 0.55,
                TopCandidateDomains = BuildFallbackTopDomainCandidates(jobRoot, domainCandidates),
                UnmappedReason = domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                    ? "No strong ownership signal was found from class name and registration source."
                    : string.Empty,
                DependentDomains = new List<string>(),
                Evidence = new List<string> { $"Detected from Hangfire registration source `{registration.SourcePath}`." }
            });
        }

        var producerJobs = jobs
            .Where(job => job.Category == HangfireJobCategory.ProducerJob)
            .ToList();
        foreach (var consumer in jobs.Where(job => job.Category == HangfireJobCategory.ConsumerJob && string.IsNullOrWhiteSpace(job.ProducerJob)))
        {
            var producer = producerJobs
                .Where(job => job.DomainOwner.Equals(consumer.DomainOwner, StringComparison.OrdinalIgnoreCase))
                .Select(job =>
                {
                    var score = 0;
                    if (!string.IsNullOrWhiteSpace(consumer.ConsumedMessageType)
                        && !string.IsNullOrWhiteSpace(job.ProducedMessageType)
                        && job.ProducedMessageType.Equals(consumer.ConsumedMessageType, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 3;
                    }

                    var consumerRoot = NormalizeJobRootName(consumer.JobName);
                    var producerRoot = NormalizeJobRootName(job.JobName);
                    if (!string.IsNullOrWhiteSpace(consumerRoot)
                        && !string.IsNullOrWhiteSpace(producerRoot)
                        && producerRoot.Equals(consumerRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 2;
                    }
                    else if (!string.IsNullOrWhiteSpace(consumerRoot)
                             && !string.IsNullOrWhiteSpace(producerRoot)
                             && (producerRoot.Contains(consumerRoot, StringComparison.OrdinalIgnoreCase)
                                 || consumerRoot.Contains(producerRoot, StringComparison.OrdinalIgnoreCase)))
                    {
                        score += 1;
                    }

                    return new
                    {
                        Producer = job,
                        Score = score
                    };
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Producer.JobName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (producer is not null)
            {
                consumer.ProducerJob = producer.Producer.JobName;
            }
        }

        return jobs
            .OrderBy(job => job.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(job => job.Category)
            .ThenBy(job => job.JobName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<TableOwnershipContract> BuildTableOwnership(IReadOnlyCollection<RepositoryTableMappingContract> mappings)
    {
        var ownerships = new List<TableOwnershipContract>();
        var grouped = mappings
            .GroupBy(mapping => mapping.TableName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var byDomain = group
                .Where(item => !string.IsNullOrWhiteSpace(item.DomainCandidate) && !item.DomainCandidate.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
                .Select(domainGroup => new
                {
                    Domain = domainGroup.Key,
                    Score = domainGroup.Sum(item => item.Confidence)
                })
                .OrderByDescending(item => item.Score)
                .ToList();

            if (byDomain.Count == 0)
            {
                ownerships.Add(new TableOwnershipContract
                {
                    TableName = group.Key,
                    OwnerDomain = string.Empty,
                    Confidence = 0,
                    IsShared = false,
                    CandidateDomains = new List<string>(),
                    ReadDomains = new List<string>(),
                    WriteDomains = new List<string>()
                });
                continue;
            }

            var top = byDomain[0];
            var total = byDomain.Sum(item => item.Score);
            var confidence = total == 0 ? 0 : top.Score / total;
            var second = byDomain.Count > 1 ? byDomain[1].Score : 0;
            var isShared = byDomain.Count > 1 && (second >= top.Score * 0.75 || byDomain.Count(domain => domain.Score >= 0.8) > 1);
            var readDomains = group
                .Where(item => IsReadAccess(item.AccessPattern))
                .Select(item => item.DomainCandidate)
                .Where(item => !string.IsNullOrWhiteSpace(item) && !item.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var writeDomains = group
                .Where(item => IsWriteAccess(item.AccessPattern))
                .Select(item => item.DomainCandidate)
                .Where(item => !string.IsNullOrWhiteSpace(item) && !item.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ownerships.Add(new TableOwnershipContract
            {
                TableName = group.Key,
                OwnerDomain = isShared ? string.Empty : top.Domain,
                Confidence = Math.Round(confidence, 2),
                IsShared = isShared,
                CandidateDomains = byDomain.Select(item => item.Domain).ToList(),
                ReadDomains = readDomains,
                WriteDomains = writeDomains
            });
        }

        return ownerships;
    }

    private static async Task<List<JobRegistrationHint>> ExtractHangfireRegistrationHintsAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var hints = new List<JobRegistrationHint>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (!LooksLikeHangfireRegistrationContent(content, sourceFile.RelativePath))
            {
                continue;
            }

            foreach (Match match in RecurringJobRegistrationRegex.Matches(content))
            {
                var jobType = NormalizeTypeName(match.Groups["jobType"].Value);
                var args = match.Groups["args"].Value;
                if (!args.Contains("=>", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(jobType))
                {
                    continue;
                }

                var extracted = ExtractJobClassAndMethod(jobType, args);
                if (string.IsNullOrWhiteSpace(extracted.JobClassName))
                {
                    continue;
                }

                var scheduleToken = ExtractScheduleTokenFromArgs(args);
                var scheduleExpression = ExtractScheduleExpressionFromArgs(scheduleToken);
                var rawScheduleKey = ExtractRawScheduleKey(scheduleToken);
                var scheduleStatus = !string.IsNullOrWhiteSpace(scheduleExpression)
                    ? HangfireScheduleResolutionStatus.Resolved
                    : !string.IsNullOrWhiteSpace(rawScheduleKey)
                        ? HangfireScheduleResolutionStatus.Unresolved
                        : HangfireScheduleResolutionStatus.Partial;
                var relatedMessage = ExtractMessageOrEventCandidates(args, Array.Empty<string>()).FirstOrDefault() ?? string.Empty;
                var producedMessage = ExtractProducedMessageCandidates(args).FirstOrDefault() ?? string.Empty;
                var trigger = HangfireJobTriggerType.Recurring;
                var category = ResolveCategoryFromHints(
                    extracted.JobClassName,
                    trigger,
                    args,
                    relatedMessage,
                    producedMessage);

                hints.Add(new JobRegistrationHint
                {
                    JobClassName = extracted.JobClassName,
                    MethodName = extracted.MethodName,
                    TriggerType = trigger,
                    CategoryHint = category,
                    RegistrationSource = sourceFile.RelativePath,
                    ScheduleExpression = scheduleExpression,
                    RawScheduleKey = rawScheduleKey,
                    ScheduleResolutionStatus = scheduleStatus,
                    ScheduleSourceType = InferScheduleSourceType(sourceFile.RelativePath, scheduleToken, rawScheduleKey),
                    SourcePath = sourceFile.RelativePath,
                    QueueName = ExtractQueueName(args),
                    RelatedMessageOrEvent = relatedMessage,
                    ProducedMessageOrEvent = producedMessage
                });
            }

            foreach (Match match in BackgroundJobRegistrationRegex.Matches(content))
            {
                var operation = match.Groups["operation"].Value;
                var jobType = NormalizeTypeName(match.Groups["jobType"].Value);
                var args = match.Groups["args"].Value;
                if (!args.Contains("=>", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(jobType))
                {
                    continue;
                }

                var extracted = ExtractJobClassAndMethod(jobType, args);
                if (string.IsNullOrWhiteSpace(extracted.JobClassName))
                {
                    continue;
                }

                var trigger = ResolveBackgroundTriggerType(operation, args);
                var scheduleToken = ExtractScheduleTokenFromArgs(args);
                var scheduleExpression = ExtractScheduleExpressionFromArgs(scheduleToken);
                var rawScheduleKey = ExtractRawScheduleKey(scheduleToken);
                var scheduleStatus = trigger is HangfireJobTriggerType.Recurring or HangfireJobTriggerType.Scheduled or HangfireJobTriggerType.Delayed
                    ? (!string.IsNullOrWhiteSpace(scheduleExpression)
                        ? HangfireScheduleResolutionStatus.Resolved
                        : !string.IsNullOrWhiteSpace(rawScheduleKey)
                            ? HangfireScheduleResolutionStatus.Unresolved
                            : HangfireScheduleResolutionStatus.Partial)
                    : HangfireScheduleResolutionStatus.NotApplicable;
                var relatedMessage = ExtractMessageOrEventCandidates(args, Array.Empty<string>()).FirstOrDefault() ?? string.Empty;
                var producedMessage = ExtractProducedMessageCandidates(args).FirstOrDefault() ?? string.Empty;
                var category = ResolveCategoryFromHints(
                    extracted.JobClassName,
                    trigger,
                    args,
                    relatedMessage,
                    producedMessage);

                hints.Add(new JobRegistrationHint
                {
                    JobClassName = extracted.JobClassName,
                    MethodName = extracted.MethodName,
                    TriggerType = trigger,
                    CategoryHint = category,
                    RegistrationSource = sourceFile.RelativePath,
                    ScheduleExpression = scheduleExpression,
                    RawScheduleKey = rawScheduleKey,
                    ScheduleResolutionStatus = scheduleStatus,
                    ScheduleSourceType = InferScheduleSourceType(sourceFile.RelativePath, scheduleToken, rawScheduleKey),
                    SourcePath = sourceFile.RelativePath,
                    QueueName = ExtractQueueName(args),
                    RelatedMessageOrEvent = relatedMessage,
                    ProducedMessageOrEvent = producedMessage
                });
            }
        }

        return hints
            .OrderBy(item => item.JobClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<ConfigScheduleHint>> ExtractConfigScheduleHintsAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var hints = new List<ConfigScheduleHint>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(sourceFile.RelativePath);
            if (!extension.Equals(".config", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (!LooksLikeScheduleConfigContent(content, sourceFile.RelativePath))
            {
                continue;
            }

            var sourceType = InferScheduleSourceType(sourceFile.RelativePath, string.Empty, string.Empty);

            foreach (Match xmlMatch in ConfigAddRegex.Matches(content))
            {
                var key = xmlMatch.Groups["key"].Value;
                var value = xmlMatch.Groups["value"].Value;
                if (!LooksLikeScheduleKeyOrValue(key, value))
                {
                    continue;
                }

                var resolved = NormalizeScheduleExpression(value);
                hints.Add(new ConfigScheduleHint
                {
                    JobHint = ExtractPotentialJobHint(key),
                    ScheduleKey = key,
                    ScheduleExpression = resolved,
                    ResolutionStatus = !string.IsNullOrWhiteSpace(resolved)
                        ? HangfireScheduleResolutionStatus.Resolved
                        : HangfireScheduleResolutionStatus.Unresolved,
                    SourceType = sourceType,
                    SourcePath = sourceFile.RelativePath
                });
            }

            foreach (Match jsonMatch in JsonStringPairRegex.Matches(content))
            {
                var key = jsonMatch.Groups["key"].Value;
                var value = jsonMatch.Groups["value"].Value;
                if (!LooksLikeScheduleKeyOrValue(key, value))
                {
                    continue;
                }

                var resolved = NormalizeScheduleExpression(value);
                hints.Add(new ConfigScheduleHint
                {
                    JobHint = ExtractPotentialJobHint(key),
                    ScheduleKey = key,
                    ScheduleExpression = resolved,
                    ResolutionStatus = !string.IsNullOrWhiteSpace(resolved)
                        ? HangfireScheduleResolutionStatus.Resolved
                        : HangfireScheduleResolutionStatus.Unresolved,
                    SourceType = sourceType,
                    SourcePath = sourceFile.RelativePath
                });
            }

            foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.Contains("cron", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("interval", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var expression = ExtractScheduleExpressionFromArgs(line);
                var rawKey = ExtractRawScheduleKey(line);

                hints.Add(new ConfigScheduleHint
                {
                    JobHint = ExtractPotentialJobHint(line),
                    ScheduleKey = rawKey,
                    ScheduleExpression = expression,
                    ResolutionStatus = !string.IsNullOrWhiteSpace(expression)
                        ? HangfireScheduleResolutionStatus.Resolved
                        : !string.IsNullOrWhiteSpace(rawKey)
                            ? HangfireScheduleResolutionStatus.Unresolved
                            : HangfireScheduleResolutionStatus.Partial,
                    SourceType = sourceType,
                    SourcePath = sourceFile.RelativePath
                });
            }
        }

        return hints
            .OrderBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.JobHint, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<ScheduleConstantHint>> ExtractScheduleConstantHintsAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken)
    {
        var hints = new List<ScheduleConstantHint>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (!content.Contains("Cron", StringComparison.OrdinalIgnoreCase)
                && !content.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                && !content.Contains("interval", StringComparison.OrdinalIgnoreCase)
                && !content.Contains("AppSettings", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match match in ConstStringRegex.Matches(content))
            {
                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                if (!LooksLikeScheduleKeyOrValue(name, value))
                {
                    continue;
                }

                hints.Add(new ScheduleConstantHint
                {
                    Name = NormalizeTypeName(name),
                    Value = value,
                    SourcePath = sourceFile.RelativePath
                });
            }

            foreach (Match match in StaticReadonlyStringRegex.Matches(content))
            {
                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                if (!LooksLikeScheduleKeyOrValue(name, value))
                {
                    continue;
                }

                hints.Add(new ScheduleConstantHint
                {
                    Name = NormalizeTypeName(name),
                    Value = value,
                    SourcePath = sourceFile.RelativePath
                });
            }
        }

        return hints
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string JobClassName, string MethodName) ExtractJobClassAndMethod(string jobType, string args)
    {
        var className = NormalizeTypeName(jobType);
        var methodName = string.Empty;

        var lambdaMatch = LambdaJobCallRegex.Match(args);
        if (lambdaMatch.Success)
        {
            methodName = lambdaMatch.Groups["method"].Value;
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            foreach (Match staticMatch in StaticJobCallRegex.Matches(args))
            {
                var candidate = NormalizeTypeName(staticMatch.Groups["class"].Value);
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (candidate.Equals("Cron", StringComparison.OrdinalIgnoreCase)
                    || candidate.Equals("TimeSpan", StringComparison.OrdinalIgnoreCase)
                    || candidate.Equals("DateTime", StringComparison.OrdinalIgnoreCase)
                    || candidate.Equals("DateTimeOffset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!LooksLikeJobToken(candidate))
                {
                    continue;
                }

                className = candidate;
                if (string.IsNullOrWhiteSpace(methodName))
                {
                    methodName = staticMatch.Groups["method"].Value;
                }

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(className))
        {
            className = NormalizeTypeName(ExtractPotentialJobHint(args));
        }

        if (string.IsNullOrWhiteSpace(methodName))
        {
            methodName = "Execute";
        }

        return (className, methodName);
    }

    private static HangfireOwnershipResult InferHangfireJobDomain(
        string className,
        string jobRoot,
        FileObservation file,
        IReadOnlyCollection<string> messageCandidates,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyDictionary<string, string> domainAliasMap,
        IReadOnlyDictionary<string, string> serviceDomainByType,
        IReadOnlyDictionary<string, string> repositoryDomainByType)
    {
        var scoreByDomain = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var dependentDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evidence = new List<string>();

        void AddScore(string? rawDomain, double score, string reason, bool markDependency = false)
        {
            if (string.IsNullOrWhiteSpace(rawDomain))
            {
                return;
            }

            var normalized = NormalizeConsolidatedDomain(rawDomain, domainAliasMap);
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                || IsTechnicalNonDomain(normalized))
            {
                return;
            }

            scoreByDomain[normalized] = scoreByDomain.GetValueOrDefault(normalized, 0) + score;
            evidence.Add(reason);
            if (markDependency)
            {
                dependentDomains.Add(normalized);
            }
        }

        AddScore(
            ResolveDomainFromName(jobRoot, domainCandidates)
            ?? domainAliasMap.GetValueOrDefault(jobRoot, string.Empty),
            8,
            "Domain inferred from job class naming.");

        var namespaceCandidate = ExtractNamespaceCandidate(file.NamespaceName);
        AddScore(
            ResolveDomainFromName(namespaceCandidate ?? string.Empty, domainCandidates)
            ?? domainAliasMap.GetValueOrDefault(namespaceCandidate ?? string.Empty, string.Empty),
            5,
            "Domain inferred from namespace clustering.");

        foreach (var token in ExtractRouteCandidates(file.RelativePath.Replace('\\', '/')))
        {
            AddScore(
                ResolveDomainFromName(token, domainCandidates)
                ?? domainAliasMap.GetValueOrDefault(token, string.Empty),
                3,
                "Domain inferred from folder/path signal.");
        }

        foreach (var dependency in file.ConstructorDependencies)
        {
            if (serviceDomainByType.TryGetValue(dependency, out var serviceDomain))
            {
                AddScore(serviceDomain, 4, $"Domain inferred from constructor service dependency `{dependency}`.", markDependency: true);
                continue;
            }

            if (repositoryDomainByType.TryGetValue(dependency, out var repositoryDomain))
            {
                AddScore(repositoryDomain, 3.5, $"Domain inferred from repository dependency `{dependency}`.", markDependency: true);
                continue;
            }

            if (IsServiceType(dependency))
            {
                AddScore(
                    ResolveDomainFromName(RemoveSuffix(dependency, "Service"), domainCandidates)
                    ?? domainAliasMap.GetValueOrDefault(NormalizeCandidateName(RemoveSuffix(dependency, "Service")), string.Empty),
                    2.5,
                    $"Domain inferred from service naming dependency `{dependency}`.",
                    markDependency: true);
            }
            else if (IsRepositoryType(dependency))
            {
                AddScore(
                    ResolveDomainFromName(RemoveSuffix(dependency, "Repository"), domainCandidates)
                    ?? domainAliasMap.GetValueOrDefault(NormalizeCandidateName(RemoveSuffix(dependency, "Repository")), string.Empty),
                    2.0,
                    $"Domain inferred from repository naming dependency `{dependency}`.",
                    markDependency: true);
            }
        }

        foreach (var message in messageCandidates)
        {
            AddScore(
                ResolveDomainFromName(message, domainCandidates)
                ?? domainAliasMap.GetValueOrDefault(NormalizeCandidateName(message), string.Empty),
                2.0,
                $"Domain inferred from message/event signal `{message}`.",
                markDependency: true);
        }

        if (scoreByDomain.Count == 0)
        {
            var fallbackCandidates = BuildFallbackTopDomainCandidates(jobRoot, domainCandidates);
            var reason = fallbackCandidates.Count == 0
                ? "No reliable domain ownership signal from namespace, dependencies, and message/topic usage."
                : $"Low-confidence ownership. Closest candidates: {string.Join(", ", fallbackCandidates)}.";
            return new HangfireOwnershipResult
            {
                Domain = "Unmapped",
                Confidence = 0.25,
                DependentDomains = dependentDomains.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                TopCandidates = fallbackCandidates,
                Evidence = evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                UnmappedReason = reason
            };
        }

        var ordered = scoreByDomain
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var winner = ordered[0];
        var total = ordered.Sum(item => item.Value);
        var confidence = total <= 0
            ? 0.40
            : Math.Clamp(winner.Value / total, 0.30, 0.97);
        var topCandidates = ordered
            .Take(3)
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HangfireOwnershipResult
        {
            Domain = winner.Key,
            Confidence = Math.Round(confidence, 2),
            DependentDomains = dependentDomains
                .Where(domain => !domain.Equals(winner.Key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            TopCandidates = topCandidates,
            Evidence = evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            UnmappedReason = string.Empty
        };
    }

    private static List<string> BuildFallbackTopDomainCandidates(
        string seed,
        IReadOnlyCollection<string> domainCandidates)
    {
        var normalizedSeed = NormalizeCandidateName(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
        {
            return new List<string>();
        }

        return domainCandidates
            .Select(domain =>
            {
                var normalizedDomain = NormalizeCandidateName(domain);
                var score = Math.Max(
                    StringSimilarityUtility.CalculateJaccardSimilarity(normalizedSeed, normalizedDomain),
                    StringSimilarityUtility.CalculateNormalizedSimilarity(normalizedSeed, normalizedDomain));
                return new
                {
                    Domain = domain,
                    Score = score
                };
            })
            .Where(item => item.Score >= 0.35)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(item => item.Domain)
            .ToList();
    }

    private static bool LooksLikeHangfireRelatedFile(FileObservation file)
    {
        if (HasHangfirePathSignal(file.RelativePath))
        {
            return true;
        }

        if (HasHangfireDependencySignal(file.ConstructorDependencies)
            || HasHangfireDependencySignal(file.ImplementedInterfaces))
        {
            return true;
        }

        return file.DiRegistrations.Any(value =>
            value.InterfaceType.Contains("Hangfire", StringComparison.OrdinalIgnoreCase)
            || value.InterfaceType.Contains("BackgroundJob", StringComparison.OrdinalIgnoreCase)
            || value.InterfaceType.Contains("RecurringJob", StringComparison.OrdinalIgnoreCase)
            || value.ImplementationType.Contains("Hangfire", StringComparison.OrdinalIgnoreCase)
            || value.ImplementationType.Contains("BackgroundJob", StringComparison.OrdinalIgnoreCase)
            || value.ImplementationType.Contains("RecurringJob", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHangfireJobClassCandidate(
        string className,
        FileObservation file,
        IReadOnlyCollection<string> methodCandidates,
        IReadOnlyCollection<JobRegistrationHint> matchingRegistrations)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        var normalizedClass = NormalizeTypeName(className);
        if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Service", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalizedClass.Equals("Startup", StringComparison.OrdinalIgnoreCase)
            || normalizedClass.Equals("Program", StringComparison.OrdinalIgnoreCase)
            || normalizedClass.Equals("MvcApplication", StringComparison.OrdinalIgnoreCase)
            || IsExplicitlyExcludedJobClass(normalizedClass))
        {
            return false;
        }

        var hasRegistration = matchingRegistrations.Count > 0;
        if (hasRegistration)
        {
            return true;
        }

        if (file.RelativePath.Replace('\\', '/').Contains("/jobs/base/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasJobLikeName = LooksLikeJobToken(normalizedClass);
        if (!hasJobLikeName)
        {
            return false;
        }

        var hasPathSignal = HasHangfirePathSignal(file.RelativePath);
        var hasDependencySignal = HasHangfireDependencySignal(file.ConstructorDependencies)
                                  || HasHangfireDependencySignal(file.ImplementedInterfaces);
        if (!hasPathSignal && !hasDependencySignal)
        {
            return false;
        }

        return methodCandidates.Count > 0 || hasPathSignal;
    }

    private static bool IsConsumerJobClass(
        string className,
        FileObservation file,
        IReadOnlyCollection<string> messageCandidates,
        IReadOnlyCollection<JobRegistrationHint> registrations)
    {
        if (LooksLikeConsumerJobName(className))
        {
            return true;
        }

        if (file.RelativePath.Contains("Consumer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((file.RelativePath.Contains("/Queue/", StringComparison.OrdinalIgnoreCase)
             || file.RelativePath.Contains("\\Queue\\", StringComparison.OrdinalIgnoreCase)
             || file.RelativePath.Contains("/Event/", StringComparison.OrdinalIgnoreCase)
             || file.RelativePath.Contains("\\Event\\", StringComparison.OrdinalIgnoreCase))
            && (LooksLikeConsumerJobName(className) || messageCandidates.Count > 0))
        {
            return true;
        }

        if (file.ImplementedInterfaces.Any(type =>
                type.Contains("Consumer", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Event", StringComparison.OrdinalIgnoreCase)
                || type.Contains("MessageHandler", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if ((file.RelativePath.Contains("Queue", StringComparison.OrdinalIgnoreCase)
             || file.RelativePath.Contains("Event", StringComparison.OrdinalIgnoreCase))
            && messageCandidates.Any(candidate =>
                ConsumerIndicatorTokens.Any(token => candidate.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return registrations.Any(registration => registration.TriggerType == HangfireJobTriggerType.QueueOrEvent);
    }

    private static List<string> ExtractJobMethodCandidates(string content)
    {
        return JobMethodRegex.Matches(content)
            .Select(match => match.Groups["name"].Value)
            .Where(name =>
                name.Contains("Execute", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Run", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Process", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Consume", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Handle", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Publish", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Sync", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Recompute", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Generate", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Refresh", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Cleanup", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractMessageOrEventCandidates(string content, IReadOnlyCollection<string> constructorDependencies)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in MessageOrEventTypeRegex.Matches(content))
        {
            var value = NormalizeTypeName(match.Groups["type"].Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(value);
            }
        }

        foreach (var dependency in constructorDependencies)
        {
            if (dependency.Contains("Event", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Message", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Consumer", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(NormalizeTypeName(dependency));
            }
        }

        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveJobMethodName(
        IReadOnlyCollection<string> methodCandidates,
        IReadOnlyCollection<JobRegistrationHint> registrationHints)
    {
        var method = registrationHints
            .Select(item => item.MethodName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(method))
        {
            return method;
        }

        return methodCandidates.FirstOrDefault() ?? "Execute";
    }

    private static string ResolveConsumedMessage(
        IReadOnlyCollection<string> messageCandidates,
        IReadOnlyCollection<JobRegistrationHint> registrationHints)
    {
        var fromRegistration = registrationHints
            .Select(item => item.RelatedMessageOrEvent)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(fromRegistration))
        {
            return fromRegistration;
        }

        return messageCandidates.FirstOrDefault() ?? string.Empty;
    }

    private static string ResolveProducedMessage(
        string content,
        IReadOnlyCollection<JobRegistrationHint> registrationHints,
        IReadOnlyCollection<string> methodCandidates)
    {
        var fromRegistration = registrationHints
            .Select(item => item.ProducedMessageOrEvent)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(fromRegistration))
        {
            return fromRegistration;
        }

        var producedCandidates = ExtractProducedMessageCandidates(content);
        if (producedCandidates.Count > 0)
        {
            return producedCandidates[0];
        }

        if (methodCandidates.Any(method =>
                method.Contains("Publish", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Produce", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Enqueue", StringComparison.OrdinalIgnoreCase)))
        {
            return "BackgroundJob";
        }

        return string.Empty;
    }

    private static List<string> ExtractProducedMessageCandidates(string content)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(content, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Event|Message|Notification|Command))\b"))
        {
            var name = NormalizeTypeName(match.Groups["name"].Value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name);
            }
        }

        return result
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveQueueName(string queueNameFromFile, IReadOnlyCollection<JobRegistrationHint> registrationHints)
    {
        var fromRegistration = registrationHints
            .Select(item => item.QueueName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(fromRegistration))
        {
            return fromRegistration;
        }

        return queueNameFromFile;
    }

    private static string ExtractQueueName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var queueMatch = NamedQueueRegex.Match(text);
        if (queueMatch.Success)
        {
            return queueMatch.Groups["queue"].Value;
        }

        var enqueuedMatch = EnqueuedStateQueueRegex.Match(text);
        if (enqueuedMatch.Success)
        {
            return enqueuedMatch.Groups["queue"].Value;
        }

        var queueAttributeMatch = QueueAttributeRegex.Match(text);
        if (queueAttributeMatch.Success)
        {
            return queueAttributeMatch.Groups["queue"].Value;
        }

        return string.Empty;
    }

    private static HangfireJobCategory ResolveJobCategory(
        string className,
        FileObservation file,
        string content,
        IReadOnlyCollection<string> methodCandidates,
        IReadOnlyCollection<string> messageCandidates,
        IReadOnlyCollection<JobRegistrationHint> registrations,
        bool producerSignal)
    {
        if (IsConsumerJobClass(className, file, messageCandidates, registrations))
        {
            return HangfireJobCategory.ConsumerJob;
        }

        var trigger = ResolveTriggerType(HangfireJobCategory.TriggeredJob, registrations);
        if (LooksLikeProducerJob(content, file, methodCandidates)
            || registrations.Any(item => item.CategoryHint == HangfireJobCategory.ProducerJob)
            || className.Contains("Producer", StringComparison.OrdinalIgnoreCase)
            || producerSignal)
        {
            return HangfireJobCategory.ProducerJob;
        }

        if (trigger is HangfireJobTriggerType.Recurring
            or HangfireJobTriggerType.Scheduled
            or HangfireJobTriggerType.Delayed)
        {
            return HangfireJobCategory.ScheduledJob;
        }

        return HangfireJobCategory.TriggeredJob;
    }

    private static bool LooksLikeProducerJob(
        string content,
        FileObservation file,
        IReadOnlyCollection<string> methodCandidates)
    {
        if (file.RelativePath.Contains("Producer", StringComparison.OrdinalIgnoreCase)
            || file.PrimaryClassName.Contains("Producer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (methodCandidates.Any(method =>
                method.Contains("Produce", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Publish", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Enqueue", StringComparison.OrdinalIgnoreCase)
                || method.Contains("Dispatch", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return content.Contains("BackgroundJob.Enqueue", StringComparison.OrdinalIgnoreCase)
               || content.Contains("IBackgroundJobClient", StringComparison.OrdinalIgnoreCase)
               || content.Contains(".Publish(", StringComparison.OrdinalIgnoreCase)
               || content.Contains(".Produce(", StringComparison.OrdinalIgnoreCase);
    }

    private static HangfireJobCategory ResolveCategoryFromHints(
        string jobClassName,
        HangfireJobTriggerType trigger,
        string args,
        string relatedMessage,
        string producedMessage)
    {
        if (LooksLikeConsumerJobName(jobClassName)
            || trigger == HangfireJobTriggerType.QueueOrEvent
            || (!string.IsNullOrWhiteSpace(relatedMessage)
                && ConsumerIndicatorTokens.Any(token => relatedMessage.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            return HangfireJobCategory.ConsumerJob;
        }

        if (jobClassName.Contains("Producer", StringComparison.OrdinalIgnoreCase)
            || args.Contains("Enqueue", StringComparison.OrdinalIgnoreCase)
            || args.Contains("Publish", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(producedMessage)
                && ProducerIndicatorTokens.Any(token => producedMessage.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            return HangfireJobCategory.ProducerJob;
        }

        if (trigger is HangfireJobTriggerType.Recurring
            or HangfireJobTriggerType.Scheduled
            or HangfireJobTriggerType.Delayed)
        {
            return HangfireJobCategory.ScheduledJob;
        }

        return HangfireJobCategory.TriggeredJob;
    }

    private static HangfireJobTriggerType ResolveTriggerType(
        HangfireJobCategory category,
        IReadOnlyCollection<JobRegistrationHint> registrationHints)
    {
        if (registrationHints.Count == 0)
        {
            return category == HangfireJobCategory.ConsumerJob
                ? HangfireJobTriggerType.QueueOrEvent
                : HangfireJobTriggerType.Unknown;
        }

        if (registrationHints.Any(item => item.TriggerType == HangfireJobTriggerType.Recurring))
        {
            return HangfireJobTriggerType.Recurring;
        }

        if (registrationHints.Any(item => item.TriggerType == HangfireJobTriggerType.Scheduled))
        {
            return HangfireJobTriggerType.Scheduled;
        }

        if (registrationHints.Any(item => item.TriggerType == HangfireJobTriggerType.Delayed))
        {
            return HangfireJobTriggerType.Delayed;
        }

        if (registrationHints.Any(item => item.TriggerType == HangfireJobTriggerType.FireAndForget))
        {
            return HangfireJobTriggerType.FireAndForget;
        }

        if (category == HangfireJobCategory.ConsumerJob
            || registrationHints.Any(item => item.TriggerType == HangfireJobTriggerType.QueueOrEvent))
        {
            return HangfireJobTriggerType.QueueOrEvent;
        }

        return HangfireJobTriggerType.Unknown;
    }

    private static string ResolveScheduleExpression(
        IReadOnlyCollection<JobRegistrationHint> registrationHints,
        IReadOnlyCollection<ConfigScheduleHint> configHints)
    {
        var fromRegistration = registrationHints
            .Select(item => item.ScheduleExpression)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(fromRegistration))
        {
            return fromRegistration;
        }

        return configHints
            .Select(item => item.ScheduleExpression)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }

    private static string ResolveScheduleSource(
        string jobFilePath,
        IReadOnlyCollection<JobRegistrationHint> registrationHints,
        IReadOnlyCollection<ConfigScheduleHint> configHints)
    {
        var sources = registrationHints
            .Select(item => item.SourcePath)
            .Concat(configHints.Select(item => item.SourcePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sources.Count == 0)
        {
            return jobFilePath;
        }

        return string.Join("; ", sources);
    }

    private static string ResolveRegistrationSource(
        string fallbackPath,
        IReadOnlyCollection<JobRegistrationHint> registrationHints)
    {
        var registrationSources = registrationHints
            .Select(item => item.RegistrationSource)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (registrationSources.Count == 0)
        {
            return fallbackPath;
        }

        return string.Join("; ", registrationSources);
    }

    private static ScheduleResolutionResult ResolveScheduleResolution(
        IReadOnlyCollection<JobRegistrationHint> registrationHints,
        IReadOnlyCollection<ConfigScheduleHint> configHints,
        IReadOnlyDictionary<string, string> scheduleByConfigKey,
        IReadOnlyDictionary<string, string> scheduleConstantByName)
    {
        var resolvedFromRegistration = registrationHints
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.ScheduleExpression));
        if (resolvedFromRegistration is not null)
        {
            return new ScheduleResolutionResult
            {
                RawScheduleKey = resolvedFromRegistration.RawScheduleKey,
                ResolvedScheduleExpression = NormalizeScheduleExpression(resolvedFromRegistration.ScheduleExpression),
                Status = HangfireScheduleResolutionStatus.Resolved,
                SourceType = resolvedFromRegistration.ScheduleSourceType,
                SourcePath = resolvedFromRegistration.SourcePath,
                Note = "Resolved from Hangfire registration code."
            };
        }

        var unresolvedFromRegistration = registrationHints
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.RawScheduleKey));
        if (unresolvedFromRegistration is not null)
        {
            var rawKey = unresolvedFromRegistration.RawScheduleKey;
            if (scheduleByConfigKey.TryGetValue(rawKey, out var resolvedFromConfig)
                && !string.IsNullOrWhiteSpace(resolvedFromConfig))
            {
                return new ScheduleResolutionResult
                {
                    RawScheduleKey = rawKey,
                    ResolvedScheduleExpression = NormalizeScheduleExpression(resolvedFromConfig),
                    Status = HangfireScheduleResolutionStatus.Resolved,
                    SourceType = HangfireScheduleSourceType.WebConfig,
                    SourcePath = unresolvedFromRegistration.SourcePath,
                    Note = "Resolved from configuration key lookup."
                };
            }

            if (scheduleConstantByName.TryGetValue(rawKey, out var resolvedFromConstant))
            {
                var normalized = NormalizeScheduleExpression(resolvedFromConstant);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return new ScheduleResolutionResult
                    {
                        RawScheduleKey = rawKey,
                        ResolvedScheduleExpression = normalized,
                        Status = HangfireScheduleResolutionStatus.Resolved,
                        SourceType = HangfireScheduleSourceType.Constant,
                        SourcePath = unresolvedFromRegistration.SourcePath,
                        Note = "Resolved from constant indirection."
                    };
                }
            }

            return new ScheduleResolutionResult
            {
                RawScheduleKey = rawKey,
                ResolvedScheduleExpression = string.Empty,
                Status = HangfireScheduleResolutionStatus.Unresolved,
                SourceType = unresolvedFromRegistration.ScheduleSourceType,
                SourcePath = unresolvedFromRegistration.SourcePath,
                Note = "Schedule key detected but value could not be resolved."
            };
        }

        var configResolved = configHints
            .FirstOrDefault(item => item.ResolutionStatus == HangfireScheduleResolutionStatus.Resolved
                                    && !string.IsNullOrWhiteSpace(item.ScheduleExpression));
        if (configResolved is not null)
        {
            return new ScheduleResolutionResult
            {
                RawScheduleKey = configResolved.ScheduleKey,
                ResolvedScheduleExpression = NormalizeScheduleExpression(configResolved.ScheduleExpression),
                Status = HangfireScheduleResolutionStatus.Resolved,
                SourceType = configResolved.SourceType,
                SourcePath = configResolved.SourcePath,
                Note = "Resolved from schedule configuration."
            };
        }

        var configUnresolved = configHints
            .FirstOrDefault(item => item.ResolutionStatus == HangfireScheduleResolutionStatus.Unresolved
                                    && !string.IsNullOrWhiteSpace(item.ScheduleKey));
        if (configUnresolved is not null)
        {
            return new ScheduleResolutionResult
            {
                RawScheduleKey = configUnresolved.ScheduleKey,
                ResolvedScheduleExpression = string.Empty,
                Status = HangfireScheduleResolutionStatus.Unresolved,
                SourceType = configUnresolved.SourceType,
                SourcePath = configUnresolved.SourcePath,
                Note = "Configuration key found but cron/interval value is unresolved."
            };
        }

        return new ScheduleResolutionResult
        {
            RawScheduleKey = string.Empty,
            ResolvedScheduleExpression = string.Empty,
            Status = HangfireScheduleResolutionStatus.NotApplicable,
            SourceType = HangfireScheduleSourceType.Unknown,
            SourcePath = string.Empty,
            Note = "No explicit schedule signal."
        };
    }

    private static HangfireJobCategory FinalizeJobCategory(
        HangfireJobCategory category,
        HangfireJobTriggerType trigger,
        ScheduleResolutionResult scheduleResolution)
    {
        if (category is HangfireJobCategory.ConsumerJob or HangfireJobCategory.ProducerJob)
        {
            return category;
        }

        if (trigger is HangfireJobTriggerType.Recurring
            or HangfireJobTriggerType.Scheduled
            or HangfireJobTriggerType.Delayed)
        {
            return HangfireJobCategory.ScheduledJob;
        }

        if (scheduleResolution.Status is HangfireScheduleResolutionStatus.Resolved
            or HangfireScheduleResolutionStatus.Unresolved
            or HangfireScheduleResolutionStatus.Partial)
        {
            return HangfireJobCategory.ScheduledJob;
        }

        return category;
    }

    private static HangfireJobTriggerType FinalizeTriggerType(
        HangfireJobCategory category,
        HangfireJobTriggerType trigger,
        ScheduleResolutionResult scheduleResolution)
    {
        if (trigger != HangfireJobTriggerType.Unknown)
        {
            return trigger;
        }

        if (category == HangfireJobCategory.ScheduledJob
            && scheduleResolution.Status != HangfireScheduleResolutionStatus.NotApplicable)
        {
            return HangfireJobTriggerType.Scheduled;
        }

        if (category == HangfireJobCategory.ConsumerJob)
        {
            return HangfireJobTriggerType.QueueOrEvent;
        }

        return trigger;
    }

    private static ScheduleResolutionResult NormalizeScheduleResolutionForCategory(
        HangfireJobCategory category,
        ScheduleResolutionResult scheduleResolution)
    {
        if (category == HangfireJobCategory.ScheduledJob)
        {
            return scheduleResolution;
        }

        return new ScheduleResolutionResult
        {
            RawScheduleKey = string.Empty,
            ResolvedScheduleExpression = string.Empty,
            Status = HangfireScheduleResolutionStatus.NotApplicable,
            SourceType = HangfireScheduleSourceType.Unknown,
            SourcePath = scheduleResolution.SourcePath,
            Note = "Schedule not applicable for non-scheduled job type."
        };
    }

    private static HangfireScheduleSourceType InferScheduleSourceType(string sourcePath, string scheduleToken, string rawScheduleKey)
    {
        if (sourcePath.EndsWith("web.config", StringComparison.OrdinalIgnoreCase))
        {
            return HangfireScheduleSourceType.WebConfig;
        }

        if (sourcePath.EndsWith("app.config", StringComparison.OrdinalIgnoreCase))
        {
            return HangfireScheduleSourceType.AppConfig;
        }

        if (sourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return HangfireScheduleSourceType.JsonConfig;
        }

        if (!string.IsNullOrWhiteSpace(rawScheduleKey)
            && (scheduleToken.Contains("AppSettings", StringComparison.OrdinalIgnoreCase)
                || scheduleToken.Contains("ConfigurationManager", StringComparison.OrdinalIgnoreCase)))
        {
            return HangfireScheduleSourceType.WebConfig;
        }

        if (!string.IsNullOrWhiteSpace(rawScheduleKey)
            && scheduleToken.Contains("Settings", StringComparison.OrdinalIgnoreCase))
        {
            return HangfireScheduleSourceType.AppConfig;
        }

        if (!string.IsNullOrWhiteSpace(rawScheduleKey))
        {
            return HangfireScheduleSourceType.Constant;
        }

        return HangfireScheduleSourceType.Code;
    }

    private static LegacyHostingEvaluation EvaluateLegacyHosting(
        string scheduleSource,
        string relativePath,
        IReadOnlyCollection<JobRegistrationHint> registrations)
    {
        if (IsLegacyScheduleSource(scheduleSource))
        {
            return new LegacyHostingEvaluation
            {
                IsLegacyHosted = true,
                Reason = "Schedule source depends on web/app config or legacy startup hosting."
            };
        }

        if (relativePath.Contains("Global.asax", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("App_Start", StringComparison.OrdinalIgnoreCase))
        {
            return new LegacyHostingEvaluation
            {
                IsLegacyHosted = true,
                Reason = "Job registration relies on legacy ASP.NET Framework hosting entry points."
            };
        }

        if (registrations.Any(item =>
                item.RegistrationSource.Contains("Startup", StringComparison.OrdinalIgnoreCase)
                && item.RegistrationSource.Contains("Hangfire", StringComparison.OrdinalIgnoreCase)))
        {
            return new LegacyHostingEvaluation
            {
                IsLegacyHosted = true,
                Reason = "Job registration is coupled to legacy Hangfire startup wiring."
            };
        }

        return new LegacyHostingEvaluation
        {
            IsLegacyHosted = false,
            Reason = string.Empty
        };
    }

    private static string ExtractCronExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cronApiMatch = Regex.Match(text, @"\bCron\.[A-Za-z0-9_]+\s*(?:\([^)]*\))?", RegexOptions.IgnoreCase);
        if (cronApiMatch.Success)
        {
            return cronApiMatch.Value;
        }

        var literalMatch = CronLiteralRegex.Match(text);
        if (literalMatch.Success)
        {
            return literalMatch.Groups["cron"].Value;
        }

        return string.Empty;
    }

    private static string ExtractScheduleExpressionFromArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return string.Empty;
        }

        var cron = ExtractCronExpression(args);
        if (!string.IsNullOrWhiteSpace(cron))
        {
            return NormalizeScheduleExpression(cron);
        }

        var timeSpanMatch = Regex.Match(args, @"\bTimeSpan\.[A-Za-z0-9_]+\s*\([^)]*\)", RegexOptions.IgnoreCase);
        if (timeSpanMatch.Success)
        {
            return timeSpanMatch.Value;
        }

        var dateTimeMatch = Regex.Match(args, @"\bDateTime(?:Offset)?\.[A-Za-z0-9_]+\s*\([^)]*\)", RegexOptions.IgnoreCase);
        if (dateTimeMatch.Success)
        {
            return dateTimeMatch.Value;
        }

        var quoted = Regex.Match(args, "\"(?<value>[^\"]+)\"");
        if (quoted.Success)
        {
            var value = quoted.Groups["value"].Value;
            return IsLikelyScheduleExpression(value)
                ? NormalizeScheduleExpression(value)
                : string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractScheduleTokenFromArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return string.Empty;
        }

        var parts = SplitInvocationArguments(args);
        foreach (var part in parts)
        {
            if (LooksLikeScheduleToken(part))
            {
                return part.Trim();
            }
        }

        return string.Empty;
    }

    private static List<string> SplitInvocationArguments(string args)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
        {
            return parts;
        }

        var depth = 0;
        var inString = false;
        var token = new StringBuilder();

        for (var i = 0; i < args.Length; i++)
        {
            var ch = args[i];
            if (ch == '"' && (i == 0 || args[i - 1] != '\\'))
            {
                inString = !inString;
            }

            if (!inString)
            {
                if (ch is '(' or '[' or '{')
                {
                    depth++;
                }
                else if (ch is ')' or ']' or '}')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (ch == ',' && depth == 0)
                {
                    parts.Add(token.ToString().Trim());
                    token.Clear();
                    continue;
                }
            }

            token.Append(ch);
        }

        if (token.Length > 0)
        {
            parts.Add(token.ToString().Trim());
        }

        return parts;
    }

    private static bool LooksLikeScheduleToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.Contains("Cron.", StringComparison.OrdinalIgnoreCase)
            || token.Contains("TimeSpan.", StringComparison.OrdinalIgnoreCase)
            || token.Contains("DateTime", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (token.Contains("AppSettings", StringComparison.OrdinalIgnoreCase)
            || token.Contains("ConfigurationManager", StringComparison.OrdinalIgnoreCase)
            || token.Contains("Settings.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var quotedValue = Regex.Match(token, "\"(?<value>[^\"]+)\"");
        if (quotedValue.Success)
        {
            var value = quotedValue.Groups["value"].Value;
            return IsLikelyScheduleExpression(value)
                   || value.Contains("cron", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                   || value.Contains("interval", StringComparison.OrdinalIgnoreCase);
        }

        return token.Contains("cron", StringComparison.OrdinalIgnoreCase)
               || token.Contains("schedule", StringComparison.OrdinalIgnoreCase)
               || token.Contains("interval", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractRawScheduleKey(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return string.Empty;
        }

        var appSettingsMatch = AppSettingLookupRegex.Match(args);
        if (appSettingsMatch.Success)
        {
            return appSettingsMatch.Groups["key"].Value;
        }

        var quoted = Regex.Match(args, "\"(?<value>[^\"]+)\"");
        if (quoted.Success)
        {
            var value = quoted.Groups["value"].Value;
            return IsLikelyScheduleExpression(value) ? string.Empty : value;
        }

        var identifierMatch = Regex.Match(args, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Cron|Schedule|Interval))\b");
        if (identifierMatch.Success)
        {
            return identifierMatch.Groups["name"].Value;
        }

        return string.Empty;
    }

    private static bool IsLikelyScheduleExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Contains("Cron.", StringComparison.OrdinalIgnoreCase)
            || value.Contains("TimeSpan.", StringComparison.OrdinalIgnoreCase)
            || value.Contains("DateTime", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (CronLiteralRegex.IsMatch(value))
        {
            return true;
        }

        return Regex.IsMatch(value, @"^\s*(?:\*|[0-9\/,\-\?]+)(?:\s+(?:\*|[0-9\/,\-\?]+)){4,6}\s*$");
    }

    private static string NormalizeScheduleExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Trim('"');
        return IsLikelyScheduleExpression(trimmed) ? trimmed : string.Empty;
    }

    private static bool LooksLikeScheduleKeyOrValue(string key, string value)
    {
        if (IsLikelyScheduleExpression(value))
        {
            return true;
        }

        return key.Contains("cron", StringComparison.OrdinalIgnoreCase)
               || key.Contains("schedule", StringComparison.OrdinalIgnoreCase)
               || key.Contains("interval", StringComparison.OrdinalIgnoreCase)
               || value.Contains("cron", StringComparison.OrdinalIgnoreCase)
               || value.Contains("schedule", StringComparison.OrdinalIgnoreCase)
               || value.Contains("interval", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPotentialJobHint(string text)
    {
        foreach (Match match in Regex.Matches(text, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Job|Consumer|Handler|Worker|Runner|Processor))\b"))
        {
            var name = NormalizeTypeName(match.Groups["name"].Value);
            if (LooksLikeJobToken(name))
            {
                return NormalizeJobRootName(name);
            }
        }

        foreach (Match match in Regex.Matches(text, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Cron|Schedule|Interval))\b"))
        {
            var candidate = match.Groups["name"].Value
                .Replace("Cron", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Schedule", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Interval", string.Empty, StringComparison.OrdinalIgnoreCase);
            candidate = NormalizeCandidateName(candidate);
            if (!string.IsNullOrWhiteSpace(candidate) && !IsInvalidDomainCandidateName(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static HangfireJobTriggerType ResolveBackgroundTriggerType(string operation, string args)
    {
        if (operation.Equals("Enqueue", StringComparison.OrdinalIgnoreCase))
        {
            return args.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                   || args.Contains("Event", StringComparison.OrdinalIgnoreCase)
                   || args.Contains("Message", StringComparison.OrdinalIgnoreCase)
                ? HangfireJobTriggerType.QueueOrEvent
                : HangfireJobTriggerType.FireAndForget;
        }

        if (operation.Equals("Schedule", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Contains("TimeSpan", StringComparison.OrdinalIgnoreCase))
            {
                return HangfireJobTriggerType.Delayed;
            }

            return HangfireJobTriggerType.Scheduled;
        }

        if (operation.Equals("ContinueJobWith", StringComparison.OrdinalIgnoreCase))
        {
            return HangfireJobTriggerType.Scheduled;
        }

        return HangfireJobTriggerType.Unknown;
    }

    private static bool LooksLikeHangfireRegistrationContent(string content, string path)
    {
        if (path.Contains("Hangfire", StringComparison.OrdinalIgnoreCase)
            || path.Contains("Startup", StringComparison.OrdinalIgnoreCase)
            || path.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return content.Contains("RecurringJob", StringComparison.OrdinalIgnoreCase)
               || content.Contains("BackgroundJob", StringComparison.OrdinalIgnoreCase)
               || content.Contains("IRecurringJobManager", StringComparison.OrdinalIgnoreCase)
               || content.Contains("IBackgroundJobClient", StringComparison.OrdinalIgnoreCase)
               || content.Contains("Hangfire", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeScheduleConfigContent(string content, string path)
    {
        if (path.Contains("web.config", StringComparison.OrdinalIgnoreCase)
            || path.Contains("appsettings", StringComparison.OrdinalIgnoreCase)
            || path.Contains("hangfire", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return content.Contains("cron", StringComparison.OrdinalIgnoreCase)
               || content.Contains("schedule", StringComparison.OrdinalIgnoreCase)
               || content.Contains("interval", StringComparison.OrdinalIgnoreCase)
               || content.Contains("hangfire", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHangfirePathSignal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        return normalizedPath.Contains("hangfire", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.Contains("/jobs/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.Contains("/background/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.Contains("/backgroundjobs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHangfireDependencySignal(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (value.Contains("Hangfire", StringComparison.OrdinalIgnoreCase)
                || value.Contains("BackgroundJob", StringComparison.OrdinalIgnoreCase)
                || value.Contains("RecurringJob", StringComparison.OrdinalIgnoreCase)
                || value.Contains("IBackgroundJobClient", StringComparison.OrdinalIgnoreCase)
                || value.Contains("IRecurringJobManager", StringComparison.OrdinalIgnoreCase)
                || value.Contains("IJobCancellationToken", StringComparison.OrdinalIgnoreCase)
                || value.Contains("JobStorage", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeConsumerJobName(string className)
    {
        return className.Contains("Consumer", StringComparison.OrdinalIgnoreCase)
               || className.EndsWith("Handler", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitlyExcludedJobClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return true;
        }

        var normalized = NormalizeTypeName(className);
        if (IgnoredJobClassNames.Contains(normalized))
        {
            return true;
        }

        if (normalized.StartsWith("I", StringComparison.Ordinal)
            && normalized.Length > 1
            && char.IsUpper(normalized[1]))
        {
            return true;
        }

        return NonJobClassSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeJobToken(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        var normalized = NormalizeTypeName(className);
        if (IsExplicitlyExcludedJobClass(normalized))
        {
            return false;
        }

        return JobSuffixes.Any(suffix => normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeJobRootName(string className)
    {
        var normalized = NormalizeCandidateName(className);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        foreach (var suffix in JobSuffixes)
        {
            normalized = RemoveSuffix(normalized, suffix);
        }

        return NormalizeCandidateName(normalized);
    }

    private static bool IsInfrastructureJobClass(string className, string path)
    {
        if (className.Contains("Bootstrap", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Server", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase)
               && path.Contains("Hangfire", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyScheduleSource(string scheduleSource)
    {
        if (string.IsNullOrWhiteSpace(scheduleSource))
        {
            return false;
        }

        return scheduleSource.Contains("web.config", StringComparison.OrdinalIgnoreCase)
               || scheduleSource.Contains(".config", StringComparison.OrdinalIgnoreCase)
               || scheduleSource.Contains("Global.asax", StringComparison.OrdinalIgnoreCase)
               || scheduleSource.Contains("App_Start", StringComparison.OrdinalIgnoreCase)
               || scheduleSource.Contains("Startup", StringComparison.OrdinalIgnoreCase);
    }

    private static List<ExecutionChainContract> BuildExecutionChains(
        IReadOnlyCollection<FileObservation> controllerObservations,
        IReadOnlyCollection<FileObservation> serviceObservations,
        IReadOnlyCollection<FileObservation> repositoryObservations,
        IReadOnlyCollection<FileObservation> allFiles,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryTableMappings,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyDictionary<string, string> domainAliasMap)
    {
        var chains = new List<ExecutionChainContract>();
        var servicesByName = serviceObservations
            .Where(service => !string.IsNullOrWhiteSpace(service.ServiceName))
            .GroupBy(service => service.ServiceName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var repositoriesByName = repositoryObservations
            .Where(repository => !string.IsNullOrWhiteSpace(repository.RepositoryName))
            .GroupBy(repository => repository.RepositoryName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var tableByRepository = repositoryTableMappings
            .GroupBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Confidence).First().TableName, StringComparer.OrdinalIgnoreCase);
        var repositoriesByDomain = repositoryTableMappings
            .Where(item => !string.IsNullOrWhiteSpace(item.DomainCandidate) && !item.DomainCandidate.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Confidence)
                    .Select(item => item.RepositoryName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        var servicesByDomain = serviceObservations
            .Where(item => !string.IsNullOrWhiteSpace(item.ServiceName))
            .GroupBy(item =>
            {
                var candidate = NormalizeCandidateName(RemoveSuffix(item.ServiceName!, "Service"));
                return NormalizeConsolidatedDomain(
                    ResolveDomainFromName(candidate, domainCandidates)
                    ?? domainAliasMap.GetValueOrDefault(candidate, "Unmapped"),
                    domainAliasMap);
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ServiceName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        var interfaceToImplementationMap = BuildInterfaceImplementationMap(allFiles);

        foreach (var controller in controllerObservations)
        {
            if (string.IsNullOrWhiteSpace(controller.ControllerName))
            {
                continue;
            }

            if (IsFrameworkController(controller.ControllerName))
            {
                continue;
            }

            var controllerCandidate = NormalizeCandidateName(RemoveSuffix(controller.ControllerName, "Controller"));
            var domain = ResolveDomainFromName(controllerCandidate, domainCandidates)
                         ?? ResolveDomainFromRoute(controller.Endpoints.Select(endpoint => endpoint.RoutePrefix), domainCandidates)
                         ?? domainAliasMap.GetValueOrDefault(controllerCandidate, string.Empty)
                         ?? "Unmapped";
            domain = NormalizeConsolidatedDomain(domain, domainAliasMap);

            var directService = controller.ConstructorDependencies
                .Select(dependency => ResolveDependencyImplementation(dependency, interfaceToImplementationMap))
                .FirstOrDefault(dependency => IsServiceType(dependency));

            var directRepository = controller.ConstructorDependencies
                .Select(dependency => ResolveDependencyImplementation(dependency, interfaceToImplementationMap))
                .FirstOrDefault(dependency => IsRepositoryType(dependency));

            var repositoryFromService = string.Empty;
            if (!string.IsNullOrWhiteSpace(directService) && servicesByName.TryGetValue(directService, out var serviceCandidates))
            {
                var matchedService = serviceCandidates
                    .OrderByDescending(candidate =>
                        ResolveDomainFromName(RemoveSuffix(candidate.ServiceName ?? string.Empty, "Service"), domainCandidates)?
                            .Equals(domain, StringComparison.OrdinalIgnoreCase) == true
                            ? 1
                            : 0)
                    .ThenByDescending(candidate => candidate.ConstructorDependencies.Count(dependency =>
                        dependency.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)))
                    .ThenByDescending(candidate => candidate.ConstructorDependencies.Count)
                    .FirstOrDefault();

                repositoryFromService = matchedService?.ConstructorDependencies
                    .Select(dependency => ResolveDependencyImplementation(dependency, interfaceToImplementationMap))
                    .FirstOrDefault(IsRepositoryType)
                    ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(directService)
                && servicesByDomain.TryGetValue(domain, out var domainServices)
                && domainServices.Count > 0)
            {
                directService = SelectMostSimilar(domainServices, controller.ControllerName, "Controller", "Service");
            }

            if (string.IsNullOrWhiteSpace(directRepository))
            {
                if (!string.IsNullOrWhiteSpace(directService)
                    && servicesByName.TryGetValue(directService, out var serviceCandidatesForFallback))
                {
                    directRepository = serviceCandidatesForFallback
                        .SelectMany(candidate => candidate.ConstructorDependencies)
                        .Select(dependency => ResolveDependencyImplementation(dependency, interfaceToImplementationMap))
                        .FirstOrDefault(IsRepositoryType)
                        ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(directRepository)
                    && repositoriesByDomain.TryGetValue(domain, out var domainRepositories)
                    && domainRepositories.Count > 0)
                {
                    directRepository = SelectMostSimilar(domainRepositories, controller.ControllerName, "Controller", "Repository");
                }
            }

            var repository = !string.IsNullOrWhiteSpace(directRepository) ? directRepository : repositoryFromService;
            tableByRepository.TryGetValue(repository, out var table);
            if (string.IsNullOrWhiteSpace(table)
                && !string.IsNullOrWhiteSpace(repository)
                && repositoriesByName.TryGetValue(repository, out var repositoryCandidates))
            {
                table = repositoryCandidates
                    .SelectMany(candidate => candidate.TableObservations)
                    .Where(observation => IsLikelyRealTable(observation.TableName))
                    .OrderByDescending(observation => observation.Confidence)
                    .Select(observation => observation.TableName)
                    .FirstOrDefault()
                    ?? string.Empty;
            }

            chains.Add(new ExecutionChainContract
            {
                DomainCandidate = domain,
                Controller = controller.ControllerName,
                Service = directService ?? string.Empty,
                Repository = repository ?? string.Empty,
                Table = table ?? string.Empty,
                Evidence = BuildExecutionChainEvidence(directService, repository, table)
            });
        }

        return chains
            .OrderBy(chain => chain.DomainCandidate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(chain => chain.Controller, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DomainDependencyContract> BuildDependencyMatrix(
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings)
    {
        var domainByType = BuildDomainByTypeMap(domainCandidates, endpointMappings, repositoryMappings);
        var domainByFile = BuildDomainByFileMap(files, domainCandidates, endpointMappings, repositoryMappings);
        var repositoryAccessPatterns = repositoryMappings
            .GroupBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Confidence)
                    .Select(item => item.AccessPattern)
                    .FirstOrDefault() ?? "read/write",
                StringComparer.OrdinalIgnoreCase);

        var matrixMap = new Dictionary<(string From, string To, string Kind), int>();

        foreach (var file in files)
        {
            if (!domainByFile.TryGetValue(file.RelativePath, out var fromDomain))
            {
                continue;
            }

            foreach (var dependency in file.ConstructorDependencies)
            {
                if (!domainByType.TryGetValue(dependency, out var toDomain))
                {
                    continue;
                }

                if (fromDomain.Equals(toDomain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var kind = ClassifyDependencyKind(dependency, repositoryAccessPatterns);
                var key = (fromDomain, toDomain, kind);
                matrixMap[key] = matrixMap.GetValueOrDefault(key, 0) + 1;
            }
        }

        var tableUsageGroups = repositoryMappings
            .Where(item => !string.IsNullOrWhiteSpace(item.DomainCandidate) && !item.DomainCandidate.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.DomainCandidate).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .ToList();

        foreach (var tableGroup in tableUsageGroups)
        {
            var domainUsage = tableGroup
                .GroupBy(item => item.DomainCandidate, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Domain = group.Key,
                    Access = string.Join(";", group.Select(item => item.AccessPattern))
                })
                .ToList();

            foreach (var from in domainUsage)
            {
                foreach (var to in domainUsage)
                {
                    if (from.Domain.Equals(to.Domain, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var sharedKind = ClassifySharedTableDependencyKind(from.Access, to.Access);
                    var key = (from.Domain, to.Domain, sharedKind);
                    matrixMap[key] = matrixMap.GetValueOrDefault(key, 0) + 1;
                }
            }
        }

        return matrixMap
            .Select(item => new DomainDependencyContract
            {
                FromDomain = item.Key.From,
                ToDomain = item.Key.To,
                DependencyKind = item.Key.Kind,
                Intensity = item.Value
            })
            .OrderByDescending(item => item.Intensity)
            .ThenBy(item => item.FromDomain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ToDomain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DomainDependencyContract> EnrichDependencyMatrixWithHangfireJobs(
        IReadOnlyCollection<DomainDependencyContract> matrix,
        IReadOnlyCollection<HangfireJobContract> hangfireJobs)
    {
        var matrixMap = new Dictionary<(string FromDomain, string ToDomain, string DependencyKind), int>();
        foreach (var item in matrix)
        {
            var key = (item.FromDomain, item.ToDomain, item.DependencyKind);
            matrixMap[key] = matrixMap.GetValueOrDefault(key, 0) + item.Intensity;
        }

        foreach (var job in hangfireJobs)
        {
            if (job.IsInfrastructureOnly || string.IsNullOrWhiteSpace(job.DomainOwner) || job.DomainOwner.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var dependentDomain in job.DependentDomains)
            {
                if (string.IsNullOrWhiteSpace(dependentDomain)
                    || dependentDomain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                    || dependentDomain.Equals(job.DomainOwner, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dependencyKind = job.Category == HangfireJobCategory.ConsumerJob
                    ? "event-based"
                    : job.Category == HangfireJobCategory.ProducerJob
                        ? "queue"
                        : "internal-call";
                var intensity = job.Category == HangfireJobCategory.ConsumerJob
                    ? 2
                    : job.Category == HangfireJobCategory.ProducerJob
                        ? 2
                        : 1;
                var key = (job.DomainOwner, dependentDomain, dependencyKind);
                matrixMap[key] = matrixMap.GetValueOrDefault(key, 0) + intensity;
            }
        }

        return matrixMap
            .Select(item => new DomainDependencyContract
            {
                FromDomain = item.Key.FromDomain,
                ToDomain = item.Key.ToDomain,
                DependencyKind = item.Key.DependencyKind,
                Intensity = item.Value
            })
            .OrderByDescending(item => item.Intensity)
            .ThenBy(item => item.FromDomain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ToDomain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ProducerConsumerRelationshipContract> BuildProducerConsumerRelationships(
        IReadOnlyCollection<HangfireJobContract> jobs)
    {
        var relationships = new List<ProducerConsumerRelationshipContract>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var producers = jobs
            .Where(job => job.Category == HangfireJobCategory.ProducerJob)
            .ToList();
        var consumers = jobs
            .Where(job => job.Category == HangfireJobCategory.ConsumerJob)
            .ToList();

        foreach (var producer in producers)
        {
            var producerRoot = NormalizeJobRootName(producer.JobName);
            var matches = consumers
                .Where(consumer => consumer.DomainOwner.Equals(producer.DomainOwner, StringComparison.OrdinalIgnoreCase))
                .Select(consumer =>
                {
                    var score = 0.0;
                    var consumerRoot = NormalizeJobRootName(consumer.JobName);

                    if (!string.IsNullOrWhiteSpace(producer.ProducedMessageType)
                        && !string.IsNullOrWhiteSpace(consumer.ConsumedMessageType)
                        && producer.ProducedMessageType.Equals(consumer.ConsumedMessageType, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.5;
                    }

                    if (!string.IsNullOrWhiteSpace(producerRoot)
                        && !string.IsNullOrWhiteSpace(consumerRoot)
                        && producerRoot.Equals(consumerRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.4;
                    }
                    else if (!string.IsNullOrWhiteSpace(producerRoot)
                             && !string.IsNullOrWhiteSpace(consumerRoot)
                             && (producerRoot.Contains(consumerRoot, StringComparison.OrdinalIgnoreCase)
                                 || consumerRoot.Contains(producerRoot, StringComparison.OrdinalIgnoreCase)))
                    {
                        score += 0.2;
                    }

                    if (!string.IsNullOrWhiteSpace(producer.QueueOrTopic)
                        && !string.IsNullOrWhiteSpace(consumer.QueueOrTopic)
                        && producer.QueueOrTopic.Equals(consumer.QueueOrTopic, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.2;
                    }

                    return new
                    {
                        Consumer = consumer,
                        Confidence = Math.Clamp(score, 0.0, 0.97)
                    };
                })
                .Where(item => item.Confidence >= 0.55)
                .OrderByDescending(item => item.Confidence)
                .ThenBy(item => item.Consumer.JobName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var match in matches)
            {
                var relationshipType = !string.IsNullOrWhiteSpace(producer.QueueOrTopic)
                    ? "queue"
                    : !string.IsNullOrWhiteSpace(producer.ProducedMessageType)
                        ? "event"
                        : "enqueue";
                var key = $"{producer.JobName}|{match.Consumer.JobName}|{relationshipType}";
                if (!seen.Add(key))
                {
                    continue;
                }

                relationships.Add(new ProducerConsumerRelationshipContract
                {
                    ProducerJob = producer.JobName,
                    ConsumerJob = match.Consumer.JobName,
                    RelationshipType = relationshipType,
                    DomainOwner = producer.DomainOwner,
                    Confidence = Math.Round(match.Confidence, 2)
                });
            }
        }

        foreach (var consumer in consumers.Where(item => !string.IsNullOrWhiteSpace(item.ProducerJob)))
        {
            var key = $"{consumer.ProducerJob}|{consumer.JobName}|inferred";
            if (!seen.Add(key))
            {
                continue;
            }

            relationships.Add(new ProducerConsumerRelationshipContract
            {
                ProducerJob = consumer.ProducerJob,
                ConsumerJob = consumer.JobName,
                RelationshipType = "inferred",
                DomainOwner = consumer.DomainOwner,
                Confidence = 0.60
            });
        }

        return relationships
            .OrderByDescending(item => item.Confidence)
            .ThenBy(item => item.DomainOwner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProducerJob, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ConsumerJob, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BackgroundJobValidationContract BuildBackgroundJobValidation(
        IReadOnlyCollection<HangfireJobContract> jobs,
        IReadOnlyCollection<ProducerConsumerRelationshipContract> relationships)
    {
        var warnings = new List<string>();

        var discovered = jobs.Count;
        var typed = jobs.Count(job => Enum.IsDefined(typeof(HangfireJobCategory), job.Category));
        if (typed != discovered)
        {
            warnings.Add($"Job type missing for {discovered - typed} discovered job(s).");
        }

        var scheduledJobs = jobs.Where(job => job.Category == HangfireJobCategory.ScheduledJob).ToList();
        var scheduledResolved = scheduledJobs.Count(job => job.ScheduleResolutionStatus == HangfireScheduleResolutionStatus.Resolved);
        var scheduledUnresolved = scheduledJobs.Count(job => job.ScheduleResolutionStatus == HangfireScheduleResolutionStatus.Unresolved
                                                             || job.ScheduleResolutionStatus == HangfireScheduleResolutionStatus.Partial);
        if (scheduledUnresolved > 0)
        {
            warnings.Add($"{scheduledUnresolved} scheduled job(s) have unresolved/partial schedule resolution.");
        }

        var mapped = jobs.Count(job => !string.IsNullOrWhiteSpace(job.DomainOwner) && !job.DomainOwner.Equals("Unmapped", StringComparison.OrdinalIgnoreCase));
        var unmapped = jobs.Count(job => job.DomainOwner.Equals("Unmapped", StringComparison.OrdinalIgnoreCase));
        if (unmapped > 0)
        {
            warnings.Add($"{unmapped} job(s) remain unmapped and require boundary validation.");
        }

        var consumerJobs = jobs.Where(job => job.Category == HangfireJobCategory.ConsumerJob).ToList();
        var consumerWithMessageOrTopic = consumerJobs.Count(job =>
            !string.IsNullOrWhiteSpace(job.ConsumedMessageType) || !string.IsNullOrWhiteSpace(job.QueueOrTopic));
        if (consumerJobs.Count > 0 && consumerWithMessageOrTopic < consumerJobs.Count)
        {
            warnings.Add($"{consumerJobs.Count - consumerWithMessageOrTopic} consumer job(s) have no inferred message/topic.");
        }

        return new BackgroundJobValidationContract
        {
            DiscoveredJobCount = discovered,
            TypedJobCount = typed,
            ScheduledJobsWithResolvedSchedule = scheduledResolved,
            ScheduledJobsWithUnresolvedSchedule = scheduledUnresolved,
            DomainMappedJobCount = mapped,
            UnmappedJobCount = unmapped,
            ConsumerJobsWithMessageOrTopic = consumerWithMessageOrTopic,
            ProducerConsumerRelationshipCount = relationships.Count,
            Warnings = warnings
        };
    }

    private static List<ExternalDependencyMapContract> BuildExternalDependencyMaps(
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings,
        IReadOnlyCollection<DomainDependencyContract> dependencyMatrix)
    {
        var results = new List<ExternalDependencyMapContract>();
        var domainByFile = BuildDomainByFileMap(files, domainCandidates, endpointMappings, repositoryMappings);

        foreach (var domain in domainCandidates)
        {
            var domainFiles = files.Where(file =>
                domainByFile.TryGetValue(file.RelativePath, out var mapped) && mapped.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();

            var httpClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var thirdParty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var externalApis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queueOrEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in domainFiles)
            {
                foreach (var dependency in file.ExternalDependencies)
                {
                    if (dependency.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) || dependency.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        httpClients.Add(dependency);
                    }

                    if (dependency.Contains("Api", StringComparison.OrdinalIgnoreCase))
                    {
                        externalApis.Add(dependency);
                    }

                    if (dependency.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                        || dependency.Contains("Event", StringComparison.OrdinalIgnoreCase)
                        || dependency.Contains("Kafka", StringComparison.OrdinalIgnoreCase)
                        || dependency.Contains("Rabbit", StringComparison.OrdinalIgnoreCase)
                        || dependency.Contains("Bus", StringComparison.OrdinalIgnoreCase))
                    {
                        queueOrEvents.Add(dependency);
                    }

                    if (!dependency.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                        && !dependency.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                        && !dependency.StartsWith("Arabam.", StringComparison.OrdinalIgnoreCase)
                        && !dependency.Equals(domain, StringComparison.OrdinalIgnoreCase)
                        && IsLikelyExternalTypeName(dependency))
                    {
                        thirdParty.Add(dependency);
                    }
                }
            }

            var internalCalls = dependencyMatrix
                .Where(item => item.FromDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Intensity)
                .Select(item => $"{item.ToDomain} ({item.DependencyKind}, {item.Intensity})")
                .ToList();

            results.Add(new ExternalDependencyMapContract
            {
                DomainCandidate = domain,
                HttpClients = httpClients.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                ThirdPartyIntegrations = thirdParty.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                ExternalApis = externalApis.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                QueuesOrEvents = queueOrEvents.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                InternalServiceCalls = internalCalls
            });
        }

        return results;
    }

    private static List<WorkflowAnalysisContract> BuildWorkflowAnalyses(
        IReadOnlyCollection<EndpointMappingContract> endpoints,
        IReadOnlyCollection<ExecutionChainContract> chains)
    {
        var workflowGroups = endpoints
            .GroupBy(endpoint => NormalizeWorkflowName(endpoint.Action), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= 2 || group.Select(item => item.DomainCandidate).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();

        var workflows = new List<WorkflowAnalysisContract>();
        foreach (var group in workflowGroups)
        {
            var relatedDomains = group.Select(item => item.DomainCandidate).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            var relatedControllers = group.Select(item => item.Controller).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            var multiDomainChains = chains.Count(chain => relatedControllers.Contains(chain.Controller, StringComparer.OrdinalIgnoreCase)
                                                         && !string.IsNullOrWhiteSpace(chain.DomainCandidate));

            workflows.Add(new WorkflowAnalysisContract
            {
                WorkflowName = group.Key,
                RelatedControllers = relatedControllers,
                RelatedDomains = relatedDomains,
                ParticipationCount = group.Count() + multiDomainChains,
                BoundaryNote = relatedDomains.Count <= 1
                    ? "Workflow appears isolated to a single candidate boundary."
                    : "Workflow crosses multiple candidates. Requires staged extraction and contract hardening."
            });
        }

        return workflows;
    }
    private static List<LegacyRiskDetailContract> BuildLegacyRiskDetails(
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<ExecutionChainContract> chains)
    {
        var domainByFile = BuildDomainByFileMap(files, domainCandidates, endpointMappings, Array.Empty<RepositoryTableMappingContract>());
        var riskFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            foreach (var riskType in file.LegacyRiskTypes)
            {
                if (!riskFiles.TryGetValue(riskType, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    riskFiles[riskType] = set;
                }

                set.Add(file.RelativePath);
            }
        }

        var risks = new List<LegacyRiskDetailContract>();
        foreach (var kvp in riskFiles.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            var riskType = kvp.Key;
            var impactedFiles = kvp.Value.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).Take(40).ToList();
            var affectedDomains = impactedFiles
                .Select(path => domainByFile.GetValueOrDefault(path, "Unmapped"))
                .Where(domain => !string.IsNullOrWhiteSpace(domain) && !domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var template = GetRiskTemplate(riskType);
            var crossDomainWorkflowCount = chains.Count(chain => affectedDomains.Contains(chain.DomainCandidate, StringComparer.OrdinalIgnoreCase));

            risks.Add(new LegacyRiskDetailContract
            {
                RiskType = riskType,
                WhyRisky = template.WhyRisky,
                ImpactedFiles = impactedFiles,
                MigrationImpact = template.MigrationImpact,
                RecommendedRemediation = template.RecommendedRemediation,
                AffectedDomains = affectedDomains,
                BlocksExtraction = template.HighImpact && affectedDomains.Count > 0 && crossDomainWorkflowCount > 0,
                RequiresAntiCorruptionLayerOrRefactor = template.HighImpact || riskType.Contains("Static", StringComparison.OrdinalIgnoreCase)
            });
        }

        return risks;
    }

    private static List<SharedKernelItemContract> BuildSharedKernelItems(
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates)
    {
        var domainByFile = BuildDomainByFileMap(files, domainCandidates, Array.Empty<EndpointMappingContract>(), Array.Empty<RepositoryTableMappingContract>());
        var candidateSet = domainCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sharedCandidates = files
            .Where(file => file.RelativePath.Contains("Core", StringComparison.OrdinalIgnoreCase)
                           || file.RelativePath.Contains("Helper", StringComparison.OrdinalIgnoreCase)
                           || file.RelativePath.Contains("WorkContext", StringComparison.OrdinalIgnoreCase)
                           || file.RelativePath.Contains("Dto", StringComparison.OrdinalIgnoreCase)
                           || file.RelativePath.Contains("Config", StringComparison.OrdinalIgnoreCase))
            .Select(file =>
            {
                var consumerDomains = files
                    .Where(other =>
                        !other.RelativePath.Equals(file.RelativePath, StringComparison.OrdinalIgnoreCase)
                        && (other.ConstructorDependencies.Contains(file.PrimaryClassName, StringComparer.OrdinalIgnoreCase)
                            || other.ImplementedInterfaces.Contains(file.PrimaryClassName, StringComparer.OrdinalIgnoreCase)))
                    .Select(other => domainByFile.GetValueOrDefault(other.RelativePath, "Unmapped"))
                    .Where(domain => candidateSet.Contains(domain))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var componentType = InferSharedKernelComponentType(file.RelativePath, file.PrimaryClassName);
                var recommendation = InferSharedKernelRecommendation(componentType, consumerDomains.Count);

                return new
                {
                    Contract = new SharedKernelItemContract
                    {
                        ComponentName = file.PrimaryClassName,
                        ComponentType = componentType,
                        Recommendation = recommendation,
                        Rationale = $"Detected in {file.RelativePath}. Referenced by {consumerDomains.Count} domain candidate(s)."
                    },
                    ConsumerCount = consumerDomains.Count
                };
            })
            .Where(item => item.ConsumerCount >= 2
                           || item.Contract.ComponentType.Equals("WorkContext implementation", StringComparison.OrdinalIgnoreCase)
                           || item.Contract.ComponentType.Equals("common configuration", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Contract)
            .GroupBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.Rationale.Length).First())
            .OrderBy(item => item.ComponentType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        return sharedCandidates;
    }

    private static List<ServiceDossierContract> BuildServiceDossiers(
        IReadOnlyCollection<string> domains,
        IReadOnlyCollection<EndpointMappingContract> endpoints,
        IReadOnlyCollection<ExecutionChainContract> chains,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings,
        IReadOnlyCollection<TableOwnershipContract> tableOwnerships,
        IReadOnlyCollection<DomainDependencyContract> dependencyMatrix,
        IReadOnlyCollection<ExternalDependencyMapContract> externalMaps,
        IReadOnlyCollection<WorkflowAnalysisContract> workflows,
        IReadOnlyCollection<HangfireJobContract> hangfireJobs,
        IReadOnlyCollection<LegacyRiskDetailContract> legacyRisks,
        IReadOnlyCollection<SharedKernelItemContract> sharedKernelItems,
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyDictionary<string, List<string>> domainHierarchy)
    {
        var dossiers = new List<ServiceDossierContract>();
        var domainByFile = BuildDomainByFileMap(files, domains, endpoints, repositoryMappings);
        var overlappingRoutes = endpoints
            .GroupBy(endpoint => endpoint.RoutePrefix, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.DomainCandidate).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .SelectMany(group => group.Select(item => item.DomainCandidate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in domains)
        {
            var domainEndpoints = endpoints.Where(item => item.DomainCandidate.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
            var domainChains = chains.Where(item => item.DomainCandidate.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
            var domainRepositories = repositoryMappings.Where(item => item.DomainCandidate.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
            var domainEntities = files
                .Where(file => domainByFile.TryGetValue(file.RelativePath, out var mappedDomain)
                               && mappedDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .SelectMany(file => file.ClassNames.Where(className => IsEntityClassCandidate(className)))
                .Select(NormalizeCandidateName)
                .Where(entity => !string.IsNullOrWhiteSpace(entity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entity => entity, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var domainTables = domainRepositories.Select(item => item.TableName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            var domainJobs = hangfireJobs
                .Where(job => job.DomainOwner.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var consumerJobs = domainJobs
                .Where(job => job.Category == HangfireJobCategory.ConsumerJob)
                .Select(job => job.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var scheduledJobs = domainJobs
                .Where(job => job.Category == HangfireJobCategory.ScheduledJob)
                .Select(job => job.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var triggeredJobs = domainJobs
                .Where(job => job.Category == HangfireJobCategory.TriggeredJob)
                .Select(job => job.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var producerJobs = domainJobs
                .Where(job => job.Category == HangfireJobCategory.ProducerJob)
                .Select(job => job.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var normalJobs = domainJobs
                .Where(job => job.Category != HangfireJobCategory.ConsumerJob)
                .Select(job => job.JobName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var schedulingDependencies = domainJobs
                .Select(job => job.ScheduleSource)
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var legacyHostedJobCount = domainJobs.Count(job => job.IsLegacyHosted);
            var domainExternalMap = externalMaps.FirstOrDefault(item => item.DomainCandidate.Equals(domain, StringComparison.OrdinalIgnoreCase));
            var domainLegacyRisks = legacyRisks
                .Where(risk => risk.AffectedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                .Select(risk => risk.RiskType)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var outboundIntensity = dependencyMatrix
                .Where(item => item.FromDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Intensity);

            var inboundIntensity = dependencyMatrix
                .Where(item => item.ToDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Intensity);

            var jobCouplingPenalty = Math.Min(30, (consumerJobs.Count * 4) + (producerJobs.Count * 3) + Math.Min(12, legacyHostedJobCount * 3));
            var couplingScore = Math.Clamp(100 - ((outboundIntensity + inboundIntensity) * 5) - jobCouplingPenalty, 5, 100);
            var chainWithTable = domainChains.Count(chain => !string.IsNullOrWhiteSpace(chain.Table));
            var unknownChainCount = domainChains.Count(chain =>
                string.IsNullOrWhiteSpace(chain.Service)
                || string.IsNullOrWhiteSpace(chain.Repository)
                || string.IsNullOrWhiteSpace(chain.Table));
            var cohesionBase = domainEndpoints.Count + domainRepositories.Count + chainWithTable + Math.Min(12, domainJobs.Count * 2);
            var cohesionScore = Math.Clamp(30 + (cohesionBase * 8), 20, 100);
            var unknownChainRatio = domainChains.Count == 0
                ? 1.0
                : (double)unknownChainCount / domainChains.Count;

            var sharedTableCount = tableOwnerships.Count(item => item.IsShared && item.CandidateDomains.Contains(domain, StringComparer.OrdinalIgnoreCase));
            var externalDependencyCount = (domainExternalMap?.HttpClients.Count ?? 0)
                                         + (domainExternalMap?.ThirdPartyIntegrations.Count ?? 0)
                                         + (domainExternalMap?.ExternalApis.Count ?? 0)
                                         + (domainExternalMap?.QueuesOrEvents.Count ?? 0);
            var legacyRiskCount = domainLegacyRisks.Count;
            var hostUiCoupling = files.Count(file =>
                file.RelativePath.Contains("Arabam.UI", StringComparison.OrdinalIgnoreCase)
                && (file.ControllerName?.Contains(domain, StringComparison.OrdinalIgnoreCase) ?? false));

            var relatedWorkflowCount = workflows.Count(workflow => workflow.RelatedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase));
            var crossDomainWorkflowCount = workflows.Count(workflow =>
                workflow.RelatedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase)
                && workflow.RelatedDomains.Count > 1);
            var endpointOverlapPenalty = overlappingRoutes.Contains(domain) ? 12 : 0;
            var endpointOwnershipConfidence = domainEndpoints.Count == 0
                ? 0.45
                : domainEndpoints.Average(endpoint => endpoint.OwnershipConfidence);
            var workflowIsolationScore = relatedWorkflowCount == 0
                ? 70
                : Math.Clamp(100 - (crossDomainWorkflowCount * 20), 25, 100);

            var readinessPenalty = 0.0;
            readinessPenalty += sharedTableCount * 18;
            readinessPenalty += unknownChainRatio * 25;
            readinessPenalty += externalDependencyCount * 5;
            readinessPenalty += legacyRiskCount * 15;
            readinessPenalty += (outboundIntensity + inboundIntensity) * 2.5;
            readinessPenalty += endpointOverlapPenalty;
            readinessPenalty += crossDomainWorkflowCount * 10;
            readinessPenalty += hostUiCoupling * 10;
            readinessPenalty += Math.Max(0, (0.8 - endpointOwnershipConfidence) * 20);
            readinessPenalty += domainJobs.Count * 2.5;
            readinessPenalty += consumerJobs.Count * 4;
            readinessPenalty += producerJobs.Count * 3;
            readinessPenalty += legacyHostedJobCount * 8;
            if (domainEndpoints.Count == 0)
            {
                readinessPenalty += 8;
            }

            if (domainRepositories.Count == 0)
            {
                readinessPenalty += 10;
            }

            var readinessScore = Math.Clamp((int)Math.Round(100 - readinessPenalty, MidpointRounding.AwayFromZero), 5, 95);

            var readOnlyEndpoints = domainEndpoints.Count(endpoint => endpoint.HttpMethod == EndpointHttpMethod.Get);
            var readOnlyPossible = domainEndpoints.Count > 0 && readOnlyEndpoints >= Math.Ceiling(domainEndpoints.Count * 0.6);
            var stagedRecommended = sharedTableCount > 0
                                    || couplingScore < 70
                                    || crossDomainWorkflowCount > 0
                                    || legacyRiskCount > 0
                                    || unknownChainRatio > 0.25
                                    || legacyHostedJobCount > 0
                                    || consumerJobs.Count > 0;

            var blockers = new List<string>();
            if (sharedTableCount > 0)
            {
                blockers.Add($"Shared table count is {sharedTableCount}; ownership must be clarified.");
            }

            if (legacyRiskCount > 0)
            {
                blockers.Add($"Legacy risk types detected: {string.Join(", ", domainLegacyRisks)}.");
            }

            if (couplingScore < 60)
            {
                blockers.Add("Cross-domain dependency intensity is high; contracts should be hardened.");
            }

            if (crossDomainWorkflowCount > 0)
            {
                blockers.Add("Critical workflows cross multiple domain candidates.");
            }

            if (unknownChainCount > 0)
            {
                blockers.Add($"Unknown execution chains detected: {unknownChainCount}.");
            }

            if (overlappingRoutes.Contains(domain))
            {
                blockers.Add("Endpoint route prefixes overlap with other domains.");
            }

            if (endpointOwnershipConfidence < 0.55)
            {
                blockers.Add("Endpoint ownership confidence is low; controller and route ownership mapping should be validated.");
            }

            if (consumerJobs.Count > 0)
            {
                blockers.Add($"Consumer job count is {consumerJobs.Count}; queue/event contracts should be versioned before extraction.");
            }

            if (producerJobs.Count > 0)
            {
                blockers.Add($"Producer job count is {producerJobs.Count}; producer->consumer contracts should be explicit before extraction.");
            }

            if (legacyHostedJobCount > 0)
            {
                blockers.Add($"Legacy-hosted Hangfire schedules detected ({legacyHostedJobCount}); scheduling must be re-hosted or adapted for .NET 10 worker.");
            }

            var readinessExplanation = BuildReadinessExplanation(
                readinessScore,
                cohesionScore,
                couplingScore,
                sharedTableCount,
                externalDependencyCount,
                legacyRiskCount,
                crossDomainWorkflowCount,
                unknownChainCount,
                domainJobs.Count,
                consumerJobs.Count,
                legacyHostedJobCount,
                overlappingRoutes.Contains(domain),
                endpointOwnershipConfidence);

            var strategy = BuildExtractionStrategy(readinessScore, readOnlyPossible, stagedRecommended);
            var readinessLevel = ResolveReadinessLevel(readinessScore);

            dossiers.Add(new ServiceDossierContract
            {
                CandidateName = domain,
                DetectionRationale = "Likely business boundary inferred from controller, repository, table, and dependency co-occurrence.",
                Subdomains = domainHierarchy.GetValueOrDefault(domain, new List<string>())
                    .Where(item => !item.Equals(domain, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                RelatedControllers = domainEndpoints.Select(endpoint => endpoint.Controller).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                RelatedEndpoints = domainEndpoints,
                RelatedServices = domainChains.Select(chain => chain.Service).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                RelatedRepositories = domainRepositories.Select(item => item.RepositoryName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
                RelatedEntities = domainEntities,
                RelatedTables = domainTables,
                ConsumerJobs = consumerJobs,
                ScheduledJobs = scheduledJobs,
                TriggeredJobs = triggeredJobs,
                ProducerJobs = producerJobs,
                NormalJobs = normalJobs,
                JobSchedulingDependencies = schedulingDependencies,
                BackgroundJobCount = domainJobs.Count,
                ConsumerJobCount = consumerJobs.Count,
                ScheduledJobCount = scheduledJobs.Count,
                TriggeredJobCount = triggeredJobs.Count,
                ProducerJobCount = producerJobs.Count,
                NormalJobCount = normalJobs.Count,
                LegacyHostedJobCount = legacyHostedJobCount,
                ExternalDependencies = BuildExternalDependencyList(domainExternalMap),
                SharedDependencies = sharedKernelItems
                    .Where(item => domainChains.Any(chain => chain.Service.Contains(item.ComponentName, StringComparison.OrdinalIgnoreCase)
                                                             || chain.Repository.Contains(item.ComponentName, StringComparison.OrdinalIgnoreCase)))
                    .Select(item => item.ComponentName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                LegacyRisks = domainLegacyRisks,
                CohesionScore = cohesionScore,
                CouplingScore = couplingScore,
                MigrationReadinessScore = readinessScore,
                MigrationReadinessLevel = readinessLevel,
                MigrationReadinessExplanation = readinessExplanation,
                RecommendedFirstExtractionStrategy = strategy,
                ReadOnlyFirstExtractionPossible = readOnlyPossible,
                StagedMigrationRecommended = stagedRecommended,
                UnknownExecutionChainCount = unknownChainCount,
                MajorBlockers = blockers
            });
        }

        return dossiers
            .OrderByDescending(item => item.MigrationReadinessScore)
            .ThenBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<MigrationOrderRecommendationContract> BuildMigrationOrderRecommendations(
        IReadOnlyCollection<ServiceDossierContract> dossiers)
    {
        var ranked = dossiers
            .OrderByDescending(dossier => dossier.MigrationReadinessScore)
            .ThenBy(dossier => dossier.MajorBlockers.Count)
            .ThenBy(dossier => dossier.CandidateName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recommendations = new List<MigrationOrderRecommendationContract>();
        for (var i = 0; i < ranked.Count; i++)
        {
            var dossier = ranked[i];
            var why = dossier.MigrationReadinessScore >= 70
                ? "High-confidence boundary with relatively low coupling and manageable legacy risks."
                : dossier.MigrationReadinessScore >= 45
                    ? "Candidate is viable after staged hardening of shared data, contracts, and dependencies."
                    : "Candidate should be postponed until unknown chains, shared data, and legacy blockers are reduced.";
            if (dossier.BackgroundJobCount > 0)
            {
                why = $"{why} Includes {dossier.BackgroundJobCount} Hangfire job(s) (consumer: {dossier.ConsumerJobCount}, normal: {dossier.NormalJobCount}).";
            }

            recommendations.Add(new MigrationOrderRecommendationContract
            {
                Rank = i + 1,
                CandidateName = dossier.CandidateName,
                WhyFirstOrLater = why,
                MajorBlockers = dossier.MajorBlockers,
                ReadOnlyFirstExtractionPossible = dossier.ReadOnlyFirstExtractionPossible,
                StagedMigrationRecommended = dossier.StagedMigrationRecommended
            });
        }

        return recommendations;
    }
    private static Dictionary<string, string> BuildDomainByFileMap(
        IReadOnlyCollection<FileObservation> files,
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var endpointDomainByController = endpointMappings
            .GroupBy(item => item.Controller, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().DomainCandidate, StringComparer.OrdinalIgnoreCase);
        var repositoryDomainByName = repositoryMappings
            .GroupBy(item => item.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().DomainCandidate, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file.ControllerName) && IsFrameworkController(file.ControllerName))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(file.ServiceName) && IsFrameworkService(file.ServiceName))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(file.RepositoryName) && IsFrameworkRepository(file.RepositoryName))
            {
                continue;
            }

            string? domain = null;

            if (!string.IsNullOrWhiteSpace(file.ControllerName) && endpointDomainByController.TryGetValue(file.ControllerName!, out var mappedControllerDomain))
            {
                domain = mappedControllerDomain;
            }
            else if (!string.IsNullOrWhiteSpace(file.RepositoryName) && repositoryDomainByName.TryGetValue(file.RepositoryName!, out var mappedRepositoryDomain))
            {
                domain = mappedRepositoryDomain;
            }
            else if (!string.IsNullOrWhiteSpace(file.ServiceName))
            {
                domain = ResolveDomainFromName(RemoveSuffix(file.ServiceName!, "Service"), domainCandidates);
            }

            if (string.IsNullOrWhiteSpace(domain))
            {
                domain = ResolveDomainFromName(file.PrimaryClassName, domainCandidates);
            }

            if (!string.IsNullOrWhiteSpace(domain))
            {
                result[file.RelativePath] = domain;
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildDomainByTypeMap(
        IReadOnlyCollection<string> domainCandidates,
        IReadOnlyCollection<EndpointMappingContract> endpointMappings,
        IReadOnlyCollection<RepositoryTableMappingContract> repositoryMappings)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in endpointMappings)
        {
            map[endpoint.Controller] = endpoint.DomainCandidate;
            map[RemoveSuffix(endpoint.Controller, "Controller")] = endpoint.DomainCandidate;
        }

        foreach (var repository in repositoryMappings)
        {
            map[repository.RepositoryName] = repository.DomainCandidate;
            map[RemoveSuffix(repository.RepositoryName, "Repository")] = repository.DomainCandidate;
        }

        foreach (var domain in domainCandidates)
        {
            map[domain] = domain;
            map[$"{domain}Service"] = domain;
            map[$"{domain}Repository"] = domain;
            map[$"{domain}Controller"] = domain;
        }

        return map;
    }

    private static string ClassifyDependencyKind(string dependencyType, IReadOnlyDictionary<string, string> repositoryAccessPatterns)
    {
        if (dependencyType.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
        {
            if (repositoryAccessPatterns.TryGetValue(dependencyType, out var accessPattern))
            {
                if (IsWriteAccess(accessPattern))
                {
                    return "write";
                }

                if (IsReadAccess(accessPattern))
                {
                    return "read";
                }
            }

            return "read/write";
        }

        if (dependencyType.Contains("Event", StringComparison.OrdinalIgnoreCase)
            || dependencyType.Contains("Queue", StringComparison.OrdinalIgnoreCase)
            || dependencyType.Contains("Bus", StringComparison.OrdinalIgnoreCase))
        {
            return "event-based";
        }

        if (dependencyType.Contains("HttpClient", StringComparison.OrdinalIgnoreCase)
            || dependencyType.EndsWith("Client", StringComparison.OrdinalIgnoreCase))
        {
            return "external";
        }

        return "internal-call";
    }

    private static string ClassifySharedTableDependencyKind(string fromAccess, string toAccess)
    {
        var fromWrites = IsWriteAccess(fromAccess);
        var toWrites = IsWriteAccess(toAccess);
        var fromReads = IsReadAccess(fromAccess);
        var toReads = IsReadAccess(toAccess);

        if (fromWrites && toWrites)
        {
            return "write";
        }

        if (fromWrites && toReads)
        {
            return "write->read";
        }

        if (fromReads && toWrites)
        {
            return "read->write";
        }

        if (fromReads && toReads)
        {
            return "read";
        }

        return "shared-data";
    }

    private static string BuildReadinessExplanation(
        int readiness,
        int cohesion,
        int coupling,
        int sharedTableCount,
        int externalDependencyCount,
        int legacyRiskCount,
        int crossDomainWorkflowCount,
        int unknownChainCount,
        int backgroundJobCount,
        int consumerJobCount,
        int legacyHostedJobCount,
        bool hasEndpointOverlap,
        double endpointOwnershipConfidence)
    {
        return $"Readiness={readiness}. Cohesion={cohesion}, Coupling={coupling}, SharedTables={sharedTableCount}, ExternalDeps={externalDependencyCount}, LegacyRisks={legacyRiskCount}, CrossDomainWorkflows={crossDomainWorkflowCount}, UnknownChains={unknownChainCount}, BackgroundJobs={backgroundJobCount}, ConsumerJobs={consumerJobCount}, LegacyHostedJobs={legacyHostedJobCount}, EndpointOverlap={(hasEndpointOverlap ? "yes" : "no")}, EndpointOwnershipConfidence={endpointOwnershipConfidence:F2}.";
    }

    private static string BuildExtractionStrategy(int readiness, bool readOnlyPossible, bool stagedRecommended)
    {
        if (readiness >= 70 && readOnlyPossible)
        {
            return "Start with read-only endpoint extraction and route traffic gradually using strangler pattern.";
        }

        if (readiness >= 55)
        {
            return stagedRecommended
                ? "Likely business boundary, but shared data and contracts must be hardened first. Use staged extraction."
                : "Extract as first wave candidate with dedicated data contract and anti-corruption layer.";
        }

        if (readiness >= 40)
        {
            return "Initial candidate detected from structural analysis. Prioritize dependency reduction and table ownership hardening before extraction.";
        }

        return "Do not extract directly yet. Address legacy and coupling blockers first, then re-evaluate.";
    }

    private static string ResolveReadinessLevel(int readiness)
    {
        if (readiness >= 70)
        {
            return "High";
        }

        if (readiness >= 45)
        {
            return "Medium";
        }

        return "Low";
    }

    private static List<string> BuildExternalDependencyList(ExternalDependencyMapContract? map)
    {
        if (map is null)
        {
            return new List<string>();
        }

        return map.HttpClients
            .Concat(map.ThirdPartyIntegrations)
            .Concat(map.ExternalApis)
            .Concat(map.QueuesOrEvents)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildExecutionChainEvidence(string? service, string? repository, string? table)
    {
        if (!string.IsNullOrWhiteSpace(service) && !string.IsNullOrWhiteSpace(repository) && !string.IsNullOrWhiteSpace(table))
        {
            return "Controller -> service -> repository -> table chain detected from constructor dependencies and table mapping.";
        }

        if (!string.IsNullOrWhiteSpace(service) && !string.IsNullOrWhiteSpace(repository))
        {
            return "Controller -> service -> repository chain detected. Table mapping requires further validation.";
        }

        return "Partial chain inferred from naming and dependency signals. Requires validation.";
    }

    private static double CalculateEndpointOwnershipConfidence(
        string? domainFromName,
        string? domainFromRoute,
        string? domainFromAlias,
        string resolvedDomain)
    {
        if (string.IsNullOrWhiteSpace(resolvedDomain) || resolvedDomain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
        {
            return 0.30;
        }

        var confidence = 0.35;
        if (!string.IsNullOrWhiteSpace(domainFromName))
        {
            confidence += 0.35;
        }

        if (!string.IsNullOrWhiteSpace(domainFromRoute))
        {
            confidence += 0.20;
        }

        if (!string.IsNullOrWhiteSpace(domainFromAlias))
        {
            confidence += 0.10;
        }

        return Math.Clamp(confidence, 0.35, 0.95);
    }

    private static List<EndpointObservation> EnrichEndpointExposure(
        IReadOnlyCollection<EndpointObservation> endpoints,
        string relativePath,
        string namespaceName,
        string content)
    {
        var adminContext = relativePath.Contains("Admin", StringComparison.OrdinalIgnoreCase)
                           || namespaceName.Contains(".Admin", StringComparison.OrdinalIgnoreCase)
                           || relativePath.Contains("BackOffice", StringComparison.OrdinalIgnoreCase)
                           || namespaceName.Contains(".BackOffice", StringComparison.OrdinalIgnoreCase);
        var internalContext = relativePath.Contains("Internal", StringComparison.OrdinalIgnoreCase)
                              || namespaceName.Contains(".Internal", StringComparison.OrdinalIgnoreCase)
                              || content.Contains("[InternalApi", StringComparison.OrdinalIgnoreCase);
        var authorizeContext = content.Contains("[Authorize", StringComparison.OrdinalIgnoreCase);
        var allowAnonymousContext = content.Contains("[AllowAnonymous", StringComparison.OrdinalIgnoreCase);
        var adminRoleContext = content.Contains("Roles", StringComparison.OrdinalIgnoreCase)
                               && content.Contains("Admin", StringComparison.OrdinalIgnoreCase);

        var enriched = new List<EndpointObservation>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            var exposure = endpoint.Exposure;

            if (internalContext
                || endpoint.Route.Contains("/internal", StringComparison.OrdinalIgnoreCase))
            {
                exposure = EndpointExposure.Internal;
            }
            else if (adminContext
                     || adminRoleContext
                     || endpoint.Route.Contains("/admin", StringComparison.OrdinalIgnoreCase))
            {
                exposure = EndpointExposure.Admin;
            }
            else if (allowAnonymousContext)
            {
                exposure = EndpointExposure.Public;
            }
            else if (authorizeContext
                     && (endpoint.Route.Contains("/api", StringComparison.OrdinalIgnoreCase)
                         || endpoint.RoutePrefix.Contains("/api", StringComparison.OrdinalIgnoreCase)))
            {
                exposure = EndpointExposure.Public;
            }

            enriched.Add(new EndpointObservation
            {
                Action = endpoint.Action,
                HttpMethod = endpoint.HttpMethod,
                RoutePrefix = endpoint.RoutePrefix,
                Route = endpoint.Route,
                Exposure = exposure
            });
        }

        return enriched;
    }

    private static IEnumerable<string> ExtractEntityCandidates(FileObservation file)
    {
        foreach (var className in file.ClassNames)
        {
            if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                || className.EndsWith("Service", StringComparison.OrdinalIgnoreCase)
                || className.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (className.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
                || className.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
                || className.EndsWith("Response", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalized = NormalizeCandidateName(className);
            if (string.IsNullOrWhiteSpace(normalized) || IsTechnicalNonDomain(normalized))
            {
                continue;
            }

            if (normalized.EndsWith("Entity", StringComparison.OrdinalIgnoreCase))
            {
                normalized = RemoveSuffix(normalized, "Entity");
            }

            if (normalized.EndsWith("Model", StringComparison.OrdinalIgnoreCase))
            {
                normalized = RemoveSuffix(normalized, "Model");
            }

            if (IsInvalidDomainCandidateName(normalized))
            {
                continue;
            }

            yield return normalized;
        }
    }

    private static bool IsEntityClassCandidate(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return false;
        }

        if (className.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Service", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Response", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Config", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = NormalizeCandidateName(className);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return !IsTechnicalNonDomain(normalized) && !IsInvalidDomainCandidateName(normalized);
    }

    private static IEnumerable<string> ExtractRouteCandidates(string routeValue)
    {
        if (string.IsNullOrWhiteSpace(routeValue))
        {
            yield break;
        }

        var segments = routeValue
            .Split(['/', '-', '_', '{', '}', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var segment in segments)
        {
            if (segment.Equals("api", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("admin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("internal", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("v1", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("v2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = NormalizeCandidateName(segment);
            if (IsInvalidDomainCandidateName(candidate) || IsTechnicalNonDomain(candidate))
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static bool IsFrameworkController(string className)
    {
        return IgnoredControllerNames.Contains(className)
               || className.StartsWith("Base", StringComparison.OrdinalIgnoreCase)
               || className.StartsWith("Generic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrameworkService(string className)
    {
        return IgnoredServiceNames.Contains(className)
               || className.StartsWith("Base", StringComparison.OrdinalIgnoreCase)
               || className.StartsWith("Generic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrameworkRepository(string className)
    {
        return IgnoredRepositoryNames.Contains(className)
               || className.StartsWith("Base", StringComparison.OrdinalIgnoreCase)
               || className.StartsWith("Generic", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsServiceType(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.EndsWith("Service", StringComparison.OrdinalIgnoreCase)
               && !IsFrameworkService(value);
    }

    private static bool IsRepositoryType(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)
               && !IsFrameworkRepository(value);
    }

    private static string NormalizeConsolidatedDomain(string? domain, IReadOnlyDictionary<string, string> domainAliasMap)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return "Unmapped";
        }

        var normalized = NormalizeCandidateName(domain);
        if (domainAliasMap.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        return domain;
    }

    private static string ResolveDependencyImplementation(string dependency, IReadOnlyDictionary<string, string> interfaceToImplementationMap)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            return string.Empty;
        }

        if (interfaceToImplementationMap.TryGetValue(dependency, out var implementation))
        {
            return implementation;
        }

        return dependency;
    }

    private static Dictionary<string, string> BuildInterfaceImplementationMap(IReadOnlyCollection<FileObservation> files)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            foreach (var implementedInterface in file.ImplementedInterfaces)
            {
                if (!map.ContainsKey(implementedInterface))
                {
                    map[implementedInterface] = file.PrimaryClassName;
                }
            }

            foreach (var registration in file.DiRegistrations)
            {
                if (!map.ContainsKey(registration.InterfaceType))
                {
                    map[registration.InterfaceType] = registration.ImplementationType;
                }
            }
        }

        return map;
    }

    private static string SelectMostSimilar(
        IReadOnlyCollection<string> candidates,
        string sourceName,
        string sourceSuffix,
        string targetSuffix)
    {
        var sourceRoot = NormalizeCandidateName(RemoveSuffix(sourceName, sourceSuffix));
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            return candidates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? string.Empty;
        }

        return candidates
            .Select(candidate =>
            {
                var candidateRoot = NormalizeCandidateName(RemoveSuffix(candidate, targetSuffix));
                var score = Math.Max(
                    StringSimilarityUtility.CalculateJaccardSimilarity(sourceRoot, candidateRoot),
                    StringSimilarityUtility.CalculateNormalizedSimilarity(sourceRoot, candidateRoot));

                if (candidateRoot.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase)
                    || sourceRoot.StartsWith(candidateRoot, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.2;
                }

                return new
                {
                    Candidate = candidate,
                    Score = score
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Candidate, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Candidate)
            .FirstOrDefault()
            ?? string.Empty;
    }

    private static List<string> ExtractImplementedInterfaces(string content)
    {
        var interfaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ClassInheritanceRegex.Matches(content))
        {
            var basesRaw = match.Groups["bases"].Value;
            var tokens = basesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var type = NormalizeTypeName(token);
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                if (!type.StartsWith("I", StringComparison.Ordinal) || type.Length < 2 || !char.IsUpper(type[1]))
                {
                    continue;
                }

                interfaces.Add(type);
            }
        }

        return interfaces.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<DiRegistrationObservation> ExtractDiRegistrations(string content)
    {
        var registrations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in DiRegistrationRegex.Matches(content))
        {
            var iface = NormalizeTypeName(match.Groups["interface"].Value);
            var implementation = NormalizeTypeName(match.Groups["implementation"].Value);
            if (string.IsNullOrWhiteSpace(iface) || string.IsNullOrWhiteSpace(implementation))
            {
                continue;
            }

            registrations[iface] = implementation;
        }

        return registrations
            .Select(item => new DiRegistrationObservation
            {
                InterfaceType = item.Key,
                ImplementationType = item.Value
            })
            .OrderBy(item => item.InterfaceType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ExtractNamespaceCandidate(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return null;
        }

        var parts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var candidate = NormalizeCandidateName(parts[i]);
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (IsTechnicalNonDomain(candidate) || IsInvalidDomainCandidateName(candidate))
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static HashSet<string> ExtractCteNames(string content)
    {
        var ctes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CteRegex.Matches(content))
        {
            var normalized = NormalizeTableName(match.Groups["cte"].Value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                ctes.Add(normalized);
            }
        }

        foreach (Match match in CteContinuationRegex.Matches(content))
        {
            var normalized = NormalizeTableName(match.Groups["cte"].Value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                ctes.Add(normalized);
            }
        }

        return ctes;
    }

    private static string DetectTableKind(string tableName)
    {
        if (tableName.StartsWith("Vw", StringComparison.OrdinalIgnoreCase)
            || tableName.Contains("View", StringComparison.OrdinalIgnoreCase))
        {
            return "view";
        }

        if (tableName.StartsWith("Tmp", StringComparison.OrdinalIgnoreCase)
            || tableName.StartsWith("Temp", StringComparison.OrdinalIgnoreCase))
        {
            return "temp";
        }

        return "table";
    }

    private static bool IsLikelyRealTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        var normalized = NormalizeTableName(tableName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (SqlKeywordTokens.Contains(normalized)
            || SqlAliasTokens.Contains(normalized)
            || InvalidTableTokens.Contains(normalized))
        {
            return false;
        }

        if (InvalidDomainTokens.Contains(normalized))
        {
            return false;
        }

        if (normalized.Length < 3)
        {
            return false;
        }

        if (Regex.IsMatch(normalized, @"^[A-Za-z]{1,2}$")
            || Regex.IsMatch(normalized, @"^[A-Za-z]\d{1,2}$"))
        {
            return false;
        }

        if (normalized.StartsWith("#", StringComparison.Ordinal)
            || normalized.StartsWith("@", StringComparison.Ordinal)
            || normalized.StartsWith("Tmp", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Temp", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("Cte", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.All(char.IsDigit))
        {
            return false;
        }

        return true;
    }

    private static bool IsReadAccess(string accessPattern)
    {
        if (string.IsNullOrWhiteSpace(accessPattern))
        {
            return false;
        }

        if (accessPattern.Contains("read/write", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ReadOnlyAccessTokens.Any(token => accessPattern.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWriteAccess(string accessPattern)
    {
        if (string.IsNullOrWhiteSpace(accessPattern))
        {
            return false;
        }

        if (accessPattern.Contains("read/write", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return WriteAccessTokens.Any(token => accessPattern.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInvalidDomainCandidateName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        var normalized = NormalizeCandidateName(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (InvalidDomainTokens.Contains(normalized))
        {
            return true;
        }

        if (normalized.Length < 3)
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^[A-Za-z]{1,2}$"))
        {
            return true;
        }

        var tokens = TokenizeIdentifier(normalized).ToList();
        if (tokens.Count > 0)
        {
            var invalidTokenCount = tokens.Count(token =>
                InvalidDomainTokens.Contains(token)
                || TechnicalNonDomainNames.Contains(ToPascalToken(token)));

            if (invalidTokenCount == tokens.Count
                || (tokens.Count <= 2 && invalidTokenCount >= 1))
            {
                return true;
            }
        }

        if (normalized.All(char.IsDigit))
        {
            return true;
        }

        return false;
    }

    private static bool IsLikelyBusinessCandidate(CandidateEvidence evidence)
    {
        if (IsInvalidDomainCandidateName(evidence.Name))
        {
            return false;
        }

        if (IsTechnicalNonDomain(evidence.Name))
        {
            return false;
        }

        var hasStructuralSignals = evidence.ControllerCount > 0 || evidence.RepositoryCount > 0 || evidence.ServiceCount > 0;
        if (!hasStructuralSignals)
        {
            return false;
        }

        if (evidence.ControllerCount == 0 && evidence.RepositoryCount == 0 && evidence.ServiceCount < 2)
        {
            return false;
        }

        return true;
    }

    private static DomainEnumerationValidationContract BuildDomainEnumerationValidation(
        IReadOnlyCollection<string> inferredDomains,
        IReadOnlyCollection<ServiceDossierContract> dossiers,
        IReadOnlyCollection<EndpointMappingContract> endpoints,
        IReadOnlyCollection<DomainDependencyContract> dependencies)
    {
        var inferred = inferredDomains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dossierDomains = dossiers
            .Select(dossier => dossier.CandidateName)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var endpointDomains = endpoints
            .Select(endpoint => endpoint.DomainCandidate)
            .Where(domain => !string.IsNullOrWhiteSpace(domain) && !domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dependencyDomains = dependencies
            .SelectMany(dependency => new[] { dependency.FromDomain, dependency.ToDomain })
            .Where(domain => !string.IsNullOrWhiteSpace(domain) && !domain.Equals("Unmapped", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingFromDossier = inferred
            .Except(dossierDomains, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingFromEndpoint = inferred
            .Except(endpointDomains, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingFromDependency = inferred
            .Except(dependencyDomains, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var renderedRootDomainCount = inferred
            .Intersect(dossierDomains, StringComparer.OrdinalIgnoreCase)
            .Count();

        var warnings = new List<string>();
        if (missingFromDossier.Count > 0)
        {
            warnings.Add($"Domains missing dossiers: {string.Join(", ", missingFromDossier)}.");
        }

        if (missingFromEndpoint.Count > 0)
        {
            warnings.Add($"Domains without endpoint clusters: {string.Join(", ", missingFromEndpoint)}.");
        }

        if (missingFromDependency.Count > 0)
        {
            warnings.Add($"Domains without dependency edges: {string.Join(", ", missingFromDependency)}.");
        }

        if (renderedRootDomainCount != inferred.Count)
        {
            warnings.Add($"Rendered root domain mismatch: inferred `{inferred.Count}` vs rendered `{renderedRootDomainCount}`.");
        }

        return new DomainEnumerationValidationContract
        {
            InferredRootDomainCount = inferred.Count,
            RenderedRootDomainCount = renderedRootDomainCount,
            DossierDomainCount = dossierDomains.Count,
            EndpointClusterDomainCount = endpointDomains.Count,
            DependencyDomainCount = dependencyDomains.Count,
            MissingDomains = missingFromDossier
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Warnings = warnings,
            IsValid = warnings.Count == 0
        };
    }

    private static DomainConsolidationResult ConsolidateDomainCandidates(IReadOnlyCollection<CandidateEvidence> rawCandidates)
    {
        var candidateLookup = rawCandidates
            .Where(candidate => !IsInvalidDomainCandidateName(candidate.Name))
            .GroupBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new CandidateEvidence(group.Key)
                {
                    StructuralScore = group.Sum(item => item.StructuralScore),
                    WeakSignalScore = group.Sum(item => item.WeakSignalScore),
                    ControllerCount = group.Sum(item => item.ControllerCount),
                    ServiceCount = group.Sum(item => item.ServiceCount),
                    RepositoryCount = group.Sum(item => item.RepositoryCount),
                    TableCount = group.Sum(item => item.TableCount)
                },
                StringComparer.OrdinalIgnoreCase);

        var ordered = candidateLookup.Values
            .Where(IsLikelyBusinessCandidate)
            .OrderByDescending(item => item.StructuralScore + item.ControllerCount * 2 + item.RepositoryCount * 2 + item.ServiceCount)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rootScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var aliasToRoot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hierarchy = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ordered)
        {
            var root = FindConsolidatedRoot(candidate.Name, rootScores.Keys);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = InferRootFromCandidate(candidate.Name);
            }

            if (string.IsNullOrWhiteSpace(root) || IsInvalidDomainCandidateName(root) || IsTechnicalNonDomain(root))
            {
                continue;
            }

            rootScores[root] = rootScores.GetValueOrDefault(root, 0) + candidate.StructuralScore + candidate.ControllerCount + candidate.RepositoryCount;
            aliasToRoot[candidate.Name] = root;

            if (!hierarchy.TryGetValue(root, out var subdomains))
            {
                subdomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                hierarchy[root] = subdomains;
            }

            subdomains.Add(candidate.Name);
        }

        var orderedRoots = rootScores
            .Where(item => item.Value >= 8)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var consolidatedDomains = new List<string>();
        if (orderedRoots.Count > 0)
        {
            var topScore = orderedRoots[0].Value;
            var dynamicThreshold = Math.Max(8, (int)Math.Ceiling(topScore * 0.20));
            consolidatedDomains = orderedRoots
                .Where(item => item.Value >= dynamicThreshold)
                .Select(item => item.Key)
                .ToList();

            var minimumCount = Math.Min(6, orderedRoots.Count);
            if (consolidatedDomains.Count < minimumCount)
            {
                consolidatedDomains = orderedRoots
                    .Take(minimumCount)
                    .Select(item => item.Key)
                    .ToList();
            }
        }

        if (consolidatedDomains.Count == 0)
        {
            var fallbackScores = rawCandidates
                .Where(candidate => !IsInvalidDomainCandidateName(candidate.Name))
                .Where(candidate => !IsTechnicalNonDomain(candidate.Name))
                .GroupBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Name = group.Key,
                    Score = group.Sum(item => item.StructuralScore + item.WeakSignalScore + item.ControllerCount + item.RepositoryCount + item.ServiceCount)
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fallbackScores.Count > 0)
            {
                var topScore = fallbackScores[0].Score;
                var dynamicThreshold = Math.Max(4, (int)Math.Ceiling(topScore * 0.35));
                consolidatedDomains = fallbackScores
                    .Where(item => item.Score >= dynamicThreshold)
                    .Select(item => item.Name)
                    .ToList();

                if (consolidatedDomains.Count == 0)
                {
                    consolidatedDomains = fallbackScores
                        .Take(Math.Min(6, fallbackScores.Count))
                        .Select(item => item.Name)
                        .ToList();
                }
            }
        }

        var selectedDomainSet = consolidatedDomains.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliasToRoot.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            var root = aliasToRoot[alias];
            var selectedRoot = selectedDomainSet.Contains(root)
                ? root
                : MapAliasToSelectedDomain(alias, consolidatedDomains);
            if (!string.IsNullOrWhiteSpace(selectedRoot))
            {
                selectedAliasMap[alias] = selectedRoot;
            }
        }

        foreach (var domain in consolidatedDomains)
        {
            selectedAliasMap[domain] = domain;
        }

        var filteredHierarchy = consolidatedDomains
            .ToDictionary(domain => domain, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var alias in selectedAliasMap)
        {
            if (!filteredHierarchy.TryGetValue(alias.Value, out var subdomains))
            {
                continue;
            }

            subdomains.Add(alias.Key);
        }

        var hierarchySnapshot = hierarchy
            .ToDictionary(item => item.Key, item => item.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var domain in consolidatedDomains)
        {
            if (!hierarchySnapshot.TryGetValue(domain, out var originalSubdomains))
            {
                continue;
            }

            foreach (var subdomain in originalSubdomains)
            {
                filteredHierarchy[domain].Add(subdomain);
            }
        }

        var normalizedHierarchy = filteredHierarchy
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var list = group.Value
                        .Where(subdomain => !string.IsNullOrWhiteSpace(subdomain))
                        .Where(subdomain => !IsInvalidDomainCandidateName(subdomain))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(subdomain => subdomain, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (!list.Contains(group.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Insert(0, group.Key);
                    }

                    return list;
                },
                StringComparer.OrdinalIgnoreCase);

        return new DomainConsolidationResult
        {
            Domains = consolidatedDomains,
            AliasToDomainMap = selectedAliasMap,
            DomainHierarchy = normalizedHierarchy
        };
    }

    private static string? FindConsolidatedRoot(string candidate, IEnumerable<string> existingRoots)
    {
        var normalizedCandidate = NormalizeCandidateName(candidate);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return null;
        }

        var roots = existingRoots.ToList();
        var prefixRoot = roots
            .Where(root => normalizedCandidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(root => root.Length)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(prefixRoot))
        {
            return prefixRoot;
        }

        var tokenized = TokenizeIdentifier(normalizedCandidate).ToList();
        if (tokenized.Count >= 2)
        {
            var firstTokenRoot = NormalizeCandidateName(tokenized[0]);
            if (roots.Any(root => root.Equals(firstTokenRoot, StringComparison.OrdinalIgnoreCase)))
            {
                return roots.First(root => root.Equals(firstTokenRoot, StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    private static string MapAliasToSelectedDomain(string alias, IReadOnlyCollection<string> selectedDomains)
    {
        if (selectedDomains.Count == 0)
        {
            return string.Empty;
        }

        var prefixRoot = FindConsolidatedRoot(alias, selectedDomains);
        if (!string.IsNullOrWhiteSpace(prefixRoot))
        {
            return prefixRoot;
        }

        var normalizedAlias = NormalizeCandidateName(alias);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return string.Empty;
        }

        var aliasTokens = TokenizeIdentifier(normalizedAlias).ToList();
        var aliasFirstToken = aliasTokens.Count == 0 ? string.Empty : aliasTokens[0];
        var scored = selectedDomains
            .Select(domain =>
            {
                var normalizedDomain = NormalizeCandidateName(domain);
                var domainTokens = TokenizeIdentifier(normalizedDomain).ToList();
                var domainFirstToken = domainTokens.Count == 0 ? string.Empty : domainTokens[0];
                var similarity = Math.Max(
                    StringSimilarityUtility.CalculateJaccardSimilarity(normalizedAlias, normalizedDomain),
                    StringSimilarityUtility.CalculateNormalizedSimilarity(normalizedAlias, normalizedDomain));
                if (!string.IsNullOrWhiteSpace(aliasFirstToken)
                    && aliasFirstToken.Equals(domainFirstToken, StringComparison.OrdinalIgnoreCase))
                {
                    similarity += 0.15;
                }

                if (normalizedAlias.StartsWith(normalizedDomain, StringComparison.OrdinalIgnoreCase)
                    || normalizedDomain.StartsWith(normalizedAlias, StringComparison.OrdinalIgnoreCase))
                {
                    similarity += 0.12;
                }

                return new
                {
                    Domain = domain,
                    Score = similarity
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return scored is null || scored.Score < 0.50
            ? string.Empty
            : scored.Domain;
    }

    private static string InferRootFromCandidate(string candidate)
    {
        var normalized = NormalizeCandidateName(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var tokens = TokenizeIdentifier(normalized).ToList();
        if (tokens.Count <= 1)
        {
            return normalized;
        }

        var firstToken = NormalizeCandidateName(tokens[0]);
        if (!string.IsNullOrWhiteSpace(firstToken) && !IsInvalidDomainCandidateName(firstToken) && !IsTechnicalNonDomain(firstToken))
        {
            return firstToken;
        }

        return normalized;
    }

    private static List<string> ExtractConstructorDependencies(string content, IReadOnlyCollection<string> classNames)
    {
        var dependencies = new List<string>();

        foreach (var className in classNames)
        {
            var regex = new Regex(string.Format(ConstructorRegexTemplate, Regex.Escape(className)), RegexOptions.Singleline);
            var match = regex.Match(content);
            if (!match.Success)
            {
                continue;
            }

            dependencies.AddRange(ParseConstructorParameters(match.Groups["params"].Value));
        }

        return dependencies;
    }

    private static IEnumerable<string> ParseConstructorParameters(string paramsValue)
    {
        if (string.IsNullOrWhiteSpace(paramsValue))
        {
            return Array.Empty<string>();
        }

        return paramsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parameter =>
            {
                var cleaned = parameter
                    .Replace("ref ", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("out ", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("in ", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return string.Empty;
                }

                var type = parts[0];
                if (type.Contains('<') && type.Contains('>'))
                {
                    var inner = type[(type.IndexOf('<') + 1)..type.LastIndexOf('>')];
                    return NormalizeTypeName(inner);
                }

                return NormalizeTypeName(type);
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<EndpointObservation> ExtractEndpoints(string content, string controllerName)
    {
        var endpoints = new List<EndpointObservation>();
        var routePrefix = ResolveControllerRoutePrefix(content, controllerName);

        var lines = content.Split('\n');
        var pendingMethod = EndpointHttpMethod.Unknown;
        var pendingRoute = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var httpMatch = HttpAttributeRegex.Match(line);
            if (httpMatch.Success)
            {
                pendingMethod = ParseHttpMethod(httpMatch.Groups["method"].Value);
                pendingRoute = httpMatch.Groups["route"].Value;
                continue;
            }

            var actionMatch = ActionRegex.Match(line);
            if (!actionMatch.Success)
            {
                continue;
            }

            var action = actionMatch.Groups["name"].Value;
            var method = pendingMethod == EndpointHttpMethod.Unknown ? InferHttpMethodFromAction(action) : pendingMethod;
            var route = string.IsNullOrWhiteSpace(pendingRoute)
                ? $"{routePrefix}/{action.ToLowerInvariant()}"
                : CombineRoute(routePrefix, pendingRoute);
            var exposure = ClassifyExposure(route);

            endpoints.Add(new EndpointObservation
            {
                Action = action,
                HttpMethod = method,
                RoutePrefix = routePrefix,
                Route = route,
                Exposure = exposure
            });

            pendingMethod = EndpointHttpMethod.Unknown;
            pendingRoute = string.Empty;
        }

        return endpoints;
    }

    private static string ResolveControllerRoutePrefix(string content, string controllerName)
    {
        var classRoute = ClassRouteRegex.Match(content);
        if (classRoute.Success)
        {
            var raw = classRoute.Groups["route"].Value.Trim();
            return raw.StartsWith("/", StringComparison.Ordinal) ? raw : "/" + raw;
        }

        return "/" + RemoveSuffix(controllerName, "Controller").ToLowerInvariant();
    }

    private static string CombineRoute(string prefix, string suffix)
    {
        var left = prefix.TrimEnd('/');
        var right = suffix.Trim();
        if (right.StartsWith("/", StringComparison.Ordinal))
        {
            return right;
        }

        return $"{left}/{right}";
    }

    private static EndpointExposure ClassifyExposure(string route)
    {
        if (route.Contains("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointExposure.Admin;
        }

        if (route.Contains("/internal", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointExposure.Internal;
        }

        return EndpointExposure.Public;
    }

    private static List<TableObservation> ExtractTableObservations(string content, string? repositoryName)
    {
        var results = new List<TableObservation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cteNames = ExtractCteNames(content);

        foreach (Match match in SqlTableRegex.Matches(content))
        {
            var raw = match.Groups["table"].Value;
            var normalizedRaw = raw.Trim();
            if (normalizedRaw.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var table = NormalizeTableName(raw);
            if (string.IsNullOrWhiteSpace(table) || !seen.Add(table))
            {
                continue;
            }

            if (!IsLikelyRealTable(table) || cteNames.Contains(table))
            {
                continue;
            }

            var tableKind = DetectTableKind(table);
            var confidence = tableKind switch
            {
                "view" => 0.68,
                "table" => 0.88,
                _ => 0.55
            };

            results.Add(new TableObservation
            {
                TableName = table,
                AccessPattern = DetectAccessPattern(content),
                Confidence = confidence,
                Evidence = $"Detected via SQL keyword `{match.Groups["verb"].Value}`. Type={tableKind}."
            });
        }

        foreach (Match match in DbSetRegex.Matches(content))
        {
            var entity = NormalizeTableName(match.Groups["entity"].Value);
            if (string.IsNullOrWhiteSpace(entity) || !seen.Add(entity))
            {
                continue;
            }

            if (!IsLikelyRealTable(entity))
            {
                continue;
            }

            results.Add(new TableObservation
            {
                TableName = entity,
                AccessPattern = "read/write",
                Confidence = 0.72,
                Evidence = "Detected via DbSet<Entity> ownership signal. Type=table (inferred)."
            });
        }

        if (results.Count == 0 && !string.IsNullOrWhiteSpace(repositoryName))
        {
            var inferred = NormalizeTableName(RemoveSuffix(repositoryName, "Repository"));
            if (IsLikelyRealTable(inferred))
            {
                results.Add(new TableObservation
                {
                    TableName = inferred,
                    AccessPattern = "unknown",
                    Confidence = 0.40,
                    Evidence = "Inferred from repository naming (low confidence)."
                });
            }
        }

        return results;
    }

    private static string DetectAccessPattern(string content)
    {
        var normalized = content.ToLowerInvariant();
        var hasWrite = normalized.Contains("insert", StringComparison.Ordinal)
                       || normalized.Contains("update", StringComparison.Ordinal)
                       || normalized.Contains("delete", StringComparison.Ordinal)
                       || normalized.Contains("merge", StringComparison.Ordinal);
        var hasRead = normalized.Contains("select", StringComparison.Ordinal)
                      || normalized.Contains(" from ", StringComparison.Ordinal)
                      || normalized.Contains(" join ", StringComparison.Ordinal);

        if (hasRead && hasWrite)
        {
            return "read/write";
        }

        if (hasWrite)
        {
            return "write";
        }

        return "read";
    }

    private static List<string> DetectLegacyRiskTypes(string content)
    {
        var risks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (content.Contains("System.Web", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("System.Web dependency");
        }

        if (content.Contains("HttpContext", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("HttpContext usage");
        }

        if (content.Contains("Razor", StringComparison.OrdinalIgnoreCase)
            || content.Contains("WebViewPage", StringComparison.OrdinalIgnoreCase)
            || content.Contains(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("Razor rendering dependency");
        }

        if (content.Contains("Castle.Windsor", StringComparison.OrdinalIgnoreCase)
            || content.Contains("StructureMap", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Ninject", StringComparison.OrdinalIgnoreCase)
            || content.Contains("UnityContainer", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("legacy IoC container usage");
        }

        if (content.Contains("System.Runtime.Remoting", StringComparison.OrdinalIgnoreCase)
            || content.Contains("System.EnterpriseServices", StringComparison.OrdinalIgnoreCase)
            || content.Contains("System.Messaging", StringComparison.OrdinalIgnoreCase)
            || content.Contains("AppDomain.CurrentDomain", StringComparison.OrdinalIgnoreCase)
            || content.Contains("RegistryKey", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add(".NET Framework-only APIs");
        }

        if (content.Contains("ConfigurationManager.AppSettings", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Web.config", StringComparison.OrdinalIgnoreCase)
            || content.Contains("GlobalConfiguration", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("tightly coupled shared config");
        }

        if (content.Contains("HttpContext.Current", StringComparison.OrdinalIgnoreCase)
            || content.Contains("CallContext", StringComparison.OrdinalIgnoreCase)
            || content.Contains("[ThreadStatic]", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Singleton", StringComparison.OrdinalIgnoreCase)
            || content.Contains("ServiceLocator", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("static/global context usage");
        }

        return risks
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractExternalDependencies(string content, IReadOnlyCollection<string> constructorDependencies)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in UsingRegex.Matches(content))
        {
            var ns = match.Groups["ns"].Value.Trim();
            if (!IsLikelyExternalNamespace(ns))
            {
                continue;
            }

            dependencies.Add(ns);
        }

        foreach (var dependency in constructorDependencies)
        {
            if (dependency.EndsWith("Client", StringComparison.OrdinalIgnoreCase)
                || dependency.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase)
                || dependency.EndsWith("Proxy", StringComparison.OrdinalIgnoreCase)
                || dependency.EndsWith("Adapter", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Queue", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Event", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Bus", StringComparison.OrdinalIgnoreCase)
                || dependency.Contains("Api", StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLikelyExternalNamespace(string ns)
    {
        if (string.IsNullOrWhiteSpace(ns))
        {
            return false;
        }

        var likelyExternalPrefixes = new[]
        {
            "RestSharp", "Refit", "MassTransit", "RabbitMQ", "Confluent", "Kafka", "Azure", "Amazon", "Google",
            "Elasticsearch", "StackExchange.Redis", "Grpc", "Flurl", "Polly", "Serilog", "NServiceBus"
        };

        if (likelyExternalPrefixes.Any(prefix => ns.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (ns.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("Newtonsoft.", StringComparison.OrdinalIgnoreCase))
        {
            return ns.Contains("Http", StringComparison.OrdinalIgnoreCase)
                   || ns.Contains("Messaging", StringComparison.OrdinalIgnoreCase);
        }

        if (ns.StartsWith("Migration.Intelligence.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ns.Contains("Client", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("External", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("Integration", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("Api", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("Queue", StringComparison.OrdinalIgnoreCase)
               || ns.Contains("Event", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyExternalTypeName(string typeName)
    {
        return typeName.Contains("Client", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Gateway", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Proxy", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Adapter", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("External", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Queue", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Event", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Bus", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Kafka", StringComparison.OrdinalIgnoreCase)
               || typeName.Contains("Rabbit", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractProjectName(string relativePath)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        foreach (var segment in segments)
        {
            if (segment.Equals("src", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("source", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("packages", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (segment.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return segment;
        }

        return "UnknownProject";
    }

    private static string NormalizeCandidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var typeName = NormalizeTypeName(value);
        typeName = RemoveSuffix(typeName, "Controller");
        typeName = RemoveSuffix(typeName, "Service");
        typeName = RemoveSuffix(typeName, "Repository");
        typeName = RemoveSuffix(typeName, "Manager");
        typeName = RemoveSuffix(typeName, "Provider");

        var tokens = TokenizeIdentifier(typeName)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();

        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var filtered = tokens
            .Where(token =>
                !HostTokens.Contains(token.ToLowerInvariant())
                && !InfrastructureTokens.Contains(token.ToLowerInvariant())
                && !IntegrationTokens.Contains(token.ToLowerInvariant()))
            .ToList();

        if (filtered.Count == 0)
        {
            filtered = tokens;
        }

        return string.Concat(filtered.Select(ToPascalToken));
    }

    private static CandidateEvidence GetOrCreateCandidate(
        IDictionary<string, CandidateEvidence> evidenceMap,
        string name)
    {
        if (evidenceMap.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var created = new CandidateEvidence(name);
        evidenceMap[name] = created;
        return created;
    }

    private static bool IsTechnicalNonDomain(string name)
    {
        var normalized = NormalizeCandidateName(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (TechnicalNonDomainNames.Contains(normalized))
        {
            return true;
        }

        var tokens = TokenizeIdentifier(normalized)
            .Select(token => token.ToLowerInvariant())
            .ToList();

        if (tokens.Count == 0)
        {
            return true;
        }

        var technicalTokenCount = tokens.Count(token =>
            HostTokens.Contains(token)
            || InfrastructureTokens.Contains(token)
            || IntegrationTokens.Contains(token)
            || TechnicalNonDomainNames.Contains(ToPascalToken(token)));

        return technicalTokenCount == tokens.Count
               || technicalTokenCount >= Math.Max(2, tokens.Count - 1);
    }

    private static ComponentCategory ClassifyComponent(string name)
    {
        var tokens = TokenizeIdentifier(name)
            .Select(token => token.ToLowerInvariant())
            .ToList();

        if (tokens.Any(HostTokens.Contains))
        {
            return ComponentCategory.HostApplication;
        }

        if (tokens.Any(IntegrationTokens.Contains))
        {
            return ComponentCategory.IntegrationComponent;
        }

        if (tokens.Any(InfrastructureTokens.Contains))
        {
            return ComponentCategory.InfrastructureComponent;
        }

        return ComponentCategory.BusinessDomainCandidate;
    }

    private static string BuildClassificationEvidence(string componentName, ComponentCategory category)
    {
        return category switch
        {
            ComponentCategory.HostApplication =>
                $"Classified as host application because `{componentName}` contains UI/API/runner host signals.",
            ComponentCategory.InfrastructureComponent =>
                $"Classified as infrastructure because `{componentName}` is dominated by shared/core/config signals.",
            ComponentCategory.IntegrationComponent =>
                $"Classified as integration because `{componentName}` contains client/gateway/event integration signals.",
            ComponentCategory.BusinessDomainCandidate =>
                $"Classified as business domain candidate only after structural signals (controller-service-repository-table) were detected.",
            _ => "Requires additional validation."
        };
    }

    private static string? ResolveDomainFromName(string rawName, IReadOnlyCollection<string> domainCandidates)
    {
        if (string.IsNullOrWhiteSpace(rawName) || domainCandidates.Count == 0)
        {
            return null;
        }

        var normalizedName = NormalizeCandidateName(rawName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var direct = domainCandidates.FirstOrDefault(candidate =>
            candidate.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        var scored = domainCandidates
            .Select(candidate =>
            {
                var normalizedCandidate = NormalizeCandidateName(candidate);
                var containsBoost =
                    normalizedCandidate.Contains(normalizedName, StringComparison.OrdinalIgnoreCase)
                    || normalizedName.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase)
                        ? 0.15
                        : 0.0;

                var similarity = Math.Max(
                    StringSimilarityUtility.CalculateJaccardSimilarity(normalizedName, normalizedCandidate),
                    StringSimilarityUtility.CalculateNormalizedSimilarity(normalizedName, normalizedCandidate));

                return new
                {
                    Candidate = candidate,
                    Score = similarity + containsBoost
                };
            })
            .OrderByDescending(item => item.Score)
            .FirstOrDefault();

        if (scored is null || scored.Score < 0.45)
        {
            return null;
        }

        return scored.Candidate;
    }

    private static string? ResolveDomainFromRoute(
        IEnumerable<string> routePrefixes,
        IReadOnlyCollection<string> domainCandidates)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var prefix in routePrefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                continue;
            }

            var routeTokens = prefix
                .Split(['/', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.ToLowerInvariant())
                .ToList();

            foreach (var domain in domainCandidates)
            {
                var domainToken = NormalizeCandidateName(domain).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(domainToken))
                {
                    continue;
                }

                if (routeTokens.Any(token =>
                        token.Equals(domainToken, StringComparison.OrdinalIgnoreCase)
                        || token.Contains(domainToken, StringComparison.OrdinalIgnoreCase)
                        || domainToken.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    scores[domain] = scores.GetValueOrDefault(domain, 0) + 1;
                }
            }
        }

        return scores.Count == 0
            ? null
            : scores.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase).First().Key;
    }

    private static string NormalizeTableName(string rawTable)
    {
        if (string.IsNullOrWhiteSpace(rawTable))
        {
            return string.Empty;
        }

        var cleaned = rawTable
            .Trim()
            .Trim('[', ']', '`', '"');

        if (cleaned.Contains('.'))
        {
            cleaned = cleaned.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? cleaned;
            cleaned = cleaned.Trim('[', ']', '`', '"');
        }

        var tokens = cleaned
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(ToPascalToken)
            .ToList();

        if (tokens.Count > 0)
        {
            return string.Concat(tokens);
        }

        return ToPascalToken(cleaned);
    }

    private static string NormalizeWorkflowName(string actionName)
    {
        var normalized = NormalizeCandidateName(RemoveSuffix(actionName, "Async"));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "UnknownWorkflow";
        }

        return normalized.EndsWith("Workflow", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + "Workflow";
    }

    private static (string WhyRisky, string MigrationImpact, string RecommendedRemediation, bool HighImpact) GetRiskTemplate(string riskType)
    {
        return riskType switch
        {
            "System.Web dependency" => (
                "System.Web pipeline abstractions are tightly coupled to classic ASP.NET hosting.",
                "Direct extraction to ASP.NET Core/.NET 10 is blocked until host pipeline concerns are isolated.",
                "Encapsulate System.Web usages behind adapter interfaces and migrate endpoint surface to ASP.NET Core abstractions first.",
                true),
            "HttpContext usage" => (
                "Direct HttpContext access leaks transport concerns into domain/application layers.",
                "Porting to microservices requires extensive rewiring of request-scoped context logic.",
                "Introduce request context interfaces and move HttpContext access to edge adapters/controllers.",
                true),
            "Razor rendering dependency" => (
                "Razor rendering dependencies indicate mixed UI and business logic.",
                "Service extraction is delayed because rendering concerns do not belong to domain services.",
                "Split rendering from business flow and expose API contracts before extraction.",
                false),
            "legacy IoC container usage" => (
                "Legacy DI container composition roots are often framework-specific and globally coupled.",
                "Service bootstrapping for .NET 10 microservices becomes risky and error-prone.",
                "Move registrations to Microsoft.Extensions.DependencyInjection-compatible modules and remove static container access.",
                true),
            ".NET Framework-only APIs" => (
                ".NET Framework-specific APIs have no direct equivalent or require different hosting/runtime models in .NET 10.",
                "Extraction may fail at runtime and block deployment to modern service hosts.",
                "Replace APIs with .NET cross-platform alternatives and add compatibility wrappers where needed.",
                true),
            "tightly coupled shared config" => (
                "Shared configuration access patterns create hidden coupling across domains.",
                "Extracted services cannot evolve configuration independently.",
                "Introduce bounded, versioned config contracts and isolate per-service settings.",
                false),
            "static/global context usage" => (
                "Global mutable state creates hidden dependencies and thread-safety risks.",
                "Service boundary isolation and independent scaling are compromised.",
                "Refactor to explicit dependency injection, remove global state, and model context per request/workflow.",
                true),
            _ => (
                "Legacy pattern detected and requires manual validation.",
                "May reduce migration readiness depending on usage scope.",
                "Refactor toward explicit contracts and isolate framework-specific code paths.",
                false)
        };
    }

    private static string InferSharedKernelComponentType(string relativePath, string className)
    {
        if (relativePath.Contains("WorkContext", StringComparison.OrdinalIgnoreCase)
            || className.Contains("WorkContext", StringComparison.OrdinalIgnoreCase))
        {
            return "WorkContext implementation";
        }

        if (relativePath.Contains("Dto", StringComparison.OrdinalIgnoreCase)
            || className.EndsWith("Dto", StringComparison.OrdinalIgnoreCase))
        {
            return "common DTO";
        }

        if (relativePath.Contains("Config", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Config", StringComparison.OrdinalIgnoreCase))
        {
            return "common configuration";
        }

        if (relativePath.Contains("Helper", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Helper", StringComparison.OrdinalIgnoreCase)
            || className.Contains("Util", StringComparison.OrdinalIgnoreCase))
        {
            return "core utility";
        }

        return "shared component";
    }

    private static string InferSharedKernelRecommendation(string componentType, int consumerDomainCount)
    {
        if (componentType.Equals("common DTO", StringComparison.OrdinalIgnoreCase))
        {
            return consumerDomainCount >= 2
                ? "refactor into contract package"
                : "duplicate deliberately";
        }

        if (componentType.Equals("common configuration", StringComparison.OrdinalIgnoreCase))
        {
            return consumerDomainCount >= 2
                ? "eliminate before extraction"
                : "keep shared temporarily";
        }

        if (componentType.Equals("WorkContext implementation", StringComparison.OrdinalIgnoreCase))
        {
            return "eliminate before extraction";
        }

        return consumerDomainCount >= 3
            ? "refactor into contract package"
            : "keep shared temporarily";
    }

    private static EndpointHttpMethod ParseHttpMethod(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "get" => EndpointHttpMethod.Get,
            "post" => EndpointHttpMethod.Post,
            "put" => EndpointHttpMethod.Put,
            "patch" => EndpointHttpMethod.Patch,
            "delete" => EndpointHttpMethod.Delete,
            "head" => EndpointHttpMethod.Head,
            "options" => EndpointHttpMethod.Options,
            _ => EndpointHttpMethod.Unknown
        };
    }

    private static EndpointHttpMethod InferHttpMethodFromAction(string actionName)
    {
        if (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("List", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Search", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Find", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHttpMethod.Get;
        }

        if (actionName.StartsWith("Create", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Add", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Post", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Submit", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHttpMethod.Post;
        }

        if (actionName.StartsWith("Update", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Edit", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Put", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHttpMethod.Put;
        }

        if (actionName.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHttpMethod.Patch;
        }

        if (actionName.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
            || actionName.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHttpMethod.Delete;
        }

        return EndpointHttpMethod.Get;
    }

    private static string RemoveSuffix(string value, string suffix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && value.Length > suffix.Length
            ? value[..^suffix.Length]
            : value;
    }

    private static string NormalizeTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
        {
            return string.Empty;
        }

        var type = rawType.Trim();

        var genericMarkerIndex = type.IndexOf('<');
        if (genericMarkerIndex >= 0)
        {
            type = type[..genericMarkerIndex];
        }

        type = type.TrimEnd('?', '!', '[', ']');

        if (type.Contains('.'))
        {
            type = type.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? type;
        }

        if (type.StartsWith("I", StringComparison.Ordinal)
            && type.Length > 1
            && char.IsUpper(type[1])
            && (type.EndsWith("Service", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("Repository", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("Client", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("Gateway", StringComparison.OrdinalIgnoreCase)
                || type.EndsWith("Provider", StringComparison.OrdinalIgnoreCase)))
        {
            type = type[1..];
        }

        return type.Trim();
    }

    private static IEnumerable<string> TokenizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var rawParts = Regex.Split(value, @"[^A-Za-z0-9]+")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        var tokens = new List<string>();
        foreach (var part in rawParts)
        {
            tokens.AddRange(SplitCamelCase(part));
        }

        return tokens;
    }

    private static IEnumerable<string> SplitCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            var current = value[i];
            var previous = value[i - 1];
            var next = i + 1 < value.Length ? value[i + 1] : '\0';

            var boundary =
                (char.IsUpper(current) && char.IsLower(previous))
                || (char.IsDigit(current) && !char.IsDigit(previous))
                || (!char.IsDigit(current) && char.IsDigit(previous))
                || (char.IsUpper(current) && char.IsUpper(previous) && next != '\0' && char.IsLower(next));

            if (!boundary)
            {
                continue;
            }

            yield return value[start..i];
            start = i;
        }

        yield return value[start..];
    }

    private static string ToPascalToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        if (token.Length == 1)
        {
            return token.ToUpperInvariant();
        }

        if (token.All(char.IsUpper))
        {
            return token;
        }

        if (token.Any(char.IsUpper) && token.Any(char.IsLower))
        {
            return char.ToUpperInvariant(token[0]) + token[1..];
        }

        return char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
    }

    private sealed class CandidateEvidence
    {
        public CandidateEvidence(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int StructuralScore { get; set; }
        public int WeakSignalScore { get; set; }
        public int ControllerCount { get; set; }
        public int ServiceCount { get; set; }
        public int RepositoryCount { get; set; }
        public int TableCount { get; set; }
        public HashSet<string> Reasons { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FileObservation
    {
        public required string RelativePath { get; init; }
        public required string NamespaceName { get; init; }
        public required List<string> ClassNames { get; init; }
        public required string PrimaryClassName { get; init; }
        public string? ControllerName { get; init; }
        public string? ServiceName { get; init; }
        public string? RepositoryName { get; init; }
        public required List<string> ConstructorDependencies { get; init; }
        public required List<EndpointObservation> Endpoints { get; init; }
        public required List<TableObservation> TableObservations { get; init; }
        public required List<string> LegacyRiskTypes { get; init; }
        public required List<string> ExternalDependencies { get; init; }
        public required string ProjectName { get; init; }
        public required List<string> ImplementedInterfaces { get; init; }
        public required List<DiRegistrationObservation> DiRegistrations { get; init; }
    }

    private sealed class EndpointObservation
    {
        public required string Action { get; init; }
        public required EndpointHttpMethod HttpMethod { get; init; }
        public required string RoutePrefix { get; init; }
        public required string Route { get; init; }
        public required EndpointExposure Exposure { get; init; }
    }

    private sealed class TableObservation
    {
        public required string TableName { get; init; }
        public required string AccessPattern { get; init; }
        public required double Confidence { get; init; }
        public required string Evidence { get; init; }
    }

    private sealed class DiRegistrationObservation
    {
        public required string InterfaceType { get; init; }
        public required string ImplementationType { get; init; }
    }

    private sealed class JobRegistrationHint
    {
        public required string JobClassName { get; init; }
        public required string MethodName { get; init; }
        public required HangfireJobTriggerType TriggerType { get; init; }
        public required HangfireJobCategory CategoryHint { get; init; }
        public required string RegistrationSource { get; init; }
        public required string ScheduleExpression { get; init; }
        public required string RawScheduleKey { get; init; }
        public required HangfireScheduleResolutionStatus ScheduleResolutionStatus { get; init; }
        public required HangfireScheduleSourceType ScheduleSourceType { get; init; }
        public required string SourcePath { get; init; }
        public required string QueueName { get; init; }
        public required string RelatedMessageOrEvent { get; init; }
        public required string ProducedMessageOrEvent { get; init; }
    }

    private sealed class ScheduleConstantHint
    {
        public required string Name { get; init; }
        public required string Value { get; init; }
        public required string SourcePath { get; init; }
    }

    private sealed class ConfigScheduleHint
    {
        public required string JobHint { get; init; }
        public required string ScheduleKey { get; init; }
        public required string ScheduleExpression { get; init; }
        public required HangfireScheduleResolutionStatus ResolutionStatus { get; init; }
        public required HangfireScheduleSourceType SourceType { get; init; }
        public required string SourcePath { get; init; }
    }

    private sealed class ScheduleResolutionResult
    {
        public required string RawScheduleKey { get; init; }
        public required string ResolvedScheduleExpression { get; init; }
        public required HangfireScheduleResolutionStatus Status { get; init; }
        public required HangfireScheduleSourceType SourceType { get; init; }
        public required string SourcePath { get; init; }
        public required string Note { get; init; }
    }

    private sealed class LegacyHostingEvaluation
    {
        public required bool IsLegacyHosted { get; init; }
        public required string Reason { get; init; }
    }

    private sealed class HangfireOwnershipResult
    {
        public required string Domain { get; init; }
        public required double Confidence { get; init; }
        public required List<string> DependentDomains { get; init; }
        public required List<string> TopCandidates { get; init; }
        public required List<string> Evidence { get; init; }
        public required string UnmappedReason { get; init; }
    }

    private sealed class DomainConsolidationResult
    {
        public List<string> Domains { get; init; } = new();
        public Dictionary<string, string> AliasToDomainMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> DomainHierarchy { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
