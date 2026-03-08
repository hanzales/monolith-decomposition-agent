using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Agents.Services;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.CodeAnalysis.Services;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.Core.Pipeline;
using Migration.Intelligence.Design.Builders;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.DomainInference.Services;
using Migration.Intelligence.Generation.Services;
using Migration.Intelligence.Reporting.Services;
using Migration.Intelligence.Scanner.Services;
using Migration.Intelligence.Validation.Models;
using Migration.Intelligence.Validation.Services;

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    PrintUsage();
    return;
}

if (!TryParseArgs(
        args,
        out var options,
        out var designDomains,
        out var generateAllDesigns,
        out var validateDesignOutputs,
        out var generateDomainArtifacts,
        out var buildAgentPlan,
        out var agentPlanningOptions,
        out var error))
{
    Console.Error.WriteLine(error);
    PrintUsage();
    return;
}

var pipeline = new AnalysisPipeline(
    new RepoScanner(),
    new SolutionCodeAnalyzer(),
    new DomainInferenceEngine(),
    new MigrationIntelligenceAnalyzer(),
    new TargetProjectWriter(),
    new CompositeReportGenerator(new MarkdownReportWriter(), new JsonReportWriter()));

try
{
    var report = await pipeline.ExecuteAsync(options);
    Console.WriteLine("Migration analysis completed.");
    Console.WriteLine($"Markdown report: {report.MarkdownReportPath}");
    Console.WriteLine($"Json report: {report.JsonReportPath}");

    var shouldGenerateDesigns = generateAllDesigns
                                || designDomains.Count > 0
                                || validateDesignOutputs
                                || generateDomainArtifacts
                                || buildAgentPlan;
    var effectiveDesignAll = generateAllDesigns || designDomains.Count == 0;
    DesignOutputBundle? designOutputBundle = null;

    if (shouldGenerateDesigns)
    {
        designOutputBundle = await GenerateMigrationDesignOutputsAsync(
            report.Execution.Intelligence,
            options.TargetPath,
            designDomains,
            effectiveDesignAll);

        Console.WriteLine($"Migration design output count: {designOutputBundle.OutputPaths.Count}");
        foreach (var designPath in designOutputBundle.OutputPaths)
        {
            Console.WriteLine($"Design artifact: {designPath}");
        }
    }

    PortfolioValidationReport? validationReport = null;
    if ((validateDesignOutputs || generateDomainArtifacts || buildAgentPlan) && designOutputBundle is not null)
    {
        var validationOrchestrator = ValidationComposition.CreateDefaultOrchestrator();
        validationReport = validationOrchestrator.Validate(report.Execution.Intelligence, designOutputBundle.Designs);
        var validationPaths = await WriteValidationOutputsAsync(validationReport, options.TargetPath);

        Console.WriteLine($"Validation artifact count: {validationPaths.Count}");
        foreach (var validationPath in validationPaths)
        {
            Console.WriteLine($"Validation artifact: {validationPath}");
        }
    }

    if (buildAgentPlan && designOutputBundle is not null)
    {
        var agent = AgentComposition.CreateDefaultPlanningAgent();
        var agentReport = await agent.CreatePlanAsync(
            report.Execution.Intelligence,
            designOutputBundle.Designs,
            agentPlanningOptions,
            validationReport);
        var agentPaths = await WriteAgentOutputsAsync(agentReport, options.TargetPath);

        Console.WriteLine($"Agent artifact count: {agentPaths.Count}");
        foreach (var agentPath in agentPaths)
        {
            Console.WriteLine($"Agent artifact: {agentPath}");
        }
    }

    if (generateDomainArtifacts && designOutputBundle is not null)
    {
        var generationOrchestrator = GenerationComposition.CreateDefaultOrchestrator();
        var validationByScope = validationReport?.DomainReports.ToDictionary(
            reportItem => reportItem.Scope,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, ValidationReport>(StringComparer.OrdinalIgnoreCase);

        var generatedPaths = new List<string>();
        foreach (var design in designOutputBundle.Designs)
        {
            validationByScope.TryGetValue($"DomainDesign:{design.SelectedDomain}", out var domainValidation);
            var generationResult = await generationOrchestrator.GenerateAsync(
                design,
                options.TargetPath,
                domainValidation);
            generatedPaths.AddRange(generationResult.WrittenFiles);
        }

        Console.WriteLine($"Generated domain artifact count: {generatedPaths.Count}");
        foreach (var path in generatedPaths)
        {
            Console.WriteLine($"Generated artifact: {path}");
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Execution failed: {ex.Message}");
    Environment.ExitCode = 1;
}

return;

static bool TryParseArgs(
    string[] args,
    out AnalysisOptions options,
    out List<string> designDomains,
    out bool generateAllDesigns,
    out bool validateDesignOutputs,
    out bool generateDomainArtifacts,
    out bool buildAgentPlan,
    out AgentPlanningOptions agentPlanningOptions,
    out string error)
{
    options = null!;
    designDomains = new List<string>();
    generateAllDesigns = false;
    validateDesignOutputs = false;
    generateDomainArtifacts = false;
    buildAgentPlan = false;
    agentPlanningOptions = new AgentPlanningOptions();
    var agentMode = AgentMode.Deterministic;
    string? llmEndpoint = null;
    string? llmModel = null;
    string? llmApiKey = null;
    var llmTimeoutSeconds = 60;
    var llmTemperature = 0.1;
    error = string.Empty;

    string? source = null;
    string? target = null;
    var architectureDocs = new List<string>();
    var dryRun = false;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--source":
                if (!TryReadValue(args, ref i, out source))
                {
                    error = "`--source` value is missing.";
                    return false;
                }
                break;
            case "--target":
                if (!TryReadValue(args, ref i, out target))
                {
                    error = "`--target` value is missing.";
                    return false;
                }
                break;
            case "--architecture":
                if (!TryReadValue(args, ref i, out var archValue))
                {
                    error = "`--architecture` value is missing.";
                    return false;
                }

                architectureDocs.AddRange(
                    archValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            case "--dry-run":
                dryRun = true;
                break;
            case "--design-domain":
                if (!TryReadValue(args, ref i, out var designDomainValue))
                {
                    error = "`--design-domain` value is missing.";
                    return false;
                }

                var parsedDomains = designDomainValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (parsedDomains.Count == 0)
                {
                    error = "`--design-domain` requires at least one domain value.";
                    return false;
                }

                designDomains.AddRange(parsedDomains);
                break;
            case "--design-all":
                generateAllDesigns = true;
                break;
            case "--validate-design":
                validateDesignOutputs = true;
                break;
            case "--generate-domain-artifacts":
                generateDomainArtifacts = true;
                break;
            case "--agent-plan":
                buildAgentPlan = true;
                break;
            case "--agent-mode":
                if (!TryReadValue(args, ref i, out var modeValue))
                {
                    error = "`--agent-mode` value is missing.";
                    return false;
                }

                if (!TryParseAgentMode(modeValue, out agentMode))
                {
                    error = "`--agent-mode` must be either `deterministic` or `llm`.";
                    return false;
                }
                break;
            case "--llm-endpoint":
                if (!TryReadValue(args, ref i, out llmEndpoint))
                {
                    error = "`--llm-endpoint` value is missing.";
                    return false;
                }
                break;
            case "--llm-model":
                if (!TryReadValue(args, ref i, out llmModel))
                {
                    error = "`--llm-model` value is missing.";
                    return false;
                }
                break;
            case "--llm-api-key":
                if (!TryReadValue(args, ref i, out llmApiKey))
                {
                    error = "`--llm-api-key` value is missing.";
                    return false;
                }
                break;
            case "--llm-timeout-sec":
                if (!TryReadValue(args, ref i, out var timeoutValue)
                    || !int.TryParse(timeoutValue, out llmTimeoutSeconds)
                    || llmTimeoutSeconds < 15)
                {
                    error = "`--llm-timeout-sec` must be an integer >= 15.";
                    return false;
                }
                break;
            case "--llm-temperature":
                if (!TryReadValue(args, ref i, out var temperatureValue)
                    || !double.TryParse(temperatureValue, out llmTemperature)
                    || llmTemperature < 0
                    || llmTemperature > 1)
                {
                    error = "`--llm-temperature` must be a number between 0 and 1.";
                    return false;
                }
                break;
            default:
                error = $"Unknown argument: {arg}";
                return false;
        }
    }

    if (string.IsNullOrWhiteSpace(source))
    {
        error = "`--source` is required.";
        return false;
    }

    if (!Directory.Exists(source))
    {
        error = $"Source path does not exist: {source}";
        return false;
    }

    if (string.IsNullOrWhiteSpace(target))
    {
        error = "`--target` is required.";
        return false;
    }

    options = new AnalysisOptions
    {
        SourcePath = Path.GetFullPath(source),
        TargetPath = Path.GetFullPath(target),
        ArchitectureMarkdownPaths = architectureDocs.Select(Path.GetFullPath).ToList(),
        DryRun = dryRun
    };

    agentPlanningOptions = new AgentPlanningOptions
    {
        Mode = agentMode,
        Llm = new LlmAgentOptions
        {
            Endpoint = llmEndpoint ?? "https://api.openai.com/v1/chat/completions",
            Model = llmModel ?? "gpt-4.1-mini",
            ApiKey = llmApiKey ?? string.Empty,
            TimeoutSeconds = llmTimeoutSeconds,
            Temperature = llmTemperature
        }
    };

    return true;
}

static bool TryReadValue(string[] args, ref int index, out string value)
{
    value = string.Empty;
    if (index + 1 >= args.Length)
    {
        return false;
    }

    index++;
    value = args[index];
    return true;
}

static bool TryParseAgentMode(string value, out AgentMode mode)
{
    mode = AgentMode.Deterministic;
    if (string.Equals(value, "deterministic", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(value, "llm", StringComparison.OrdinalIgnoreCase))
    {
        mode = AgentMode.Llm;
        return true;
    }

    return false;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project Migration.Intelligence.Cli -- --source <path> --target <path> [--architecture <md1,md2>] [--dry-run] [--design-domain <name1,name2>] [--design-all] [--validate-design] [--generate-domain-artifacts] [--agent-plan] [--agent-mode <deterministic|llm>] [--llm-endpoint <url>] [--llm-model <name>] [--llm-api-key <key>] [--llm-timeout-sec <seconds>] [--llm-temperature <0..1>]");
}

static async Task<DesignOutputBundle> GenerateMigrationDesignOutputsAsync(
    MigrationIntelligenceContract intelligence,
    string outputRoot,
    IReadOnlyCollection<string> requestedDomains,
    bool generateAllDesigns)
{
    var designBuilder = DesignComposition.CreateDefaultBuilder();
    var designOutputDirectory = Path.Combine(outputRoot, "design");
    Directory.CreateDirectory(designOutputDirectory);

    var targets = requestedDomains
        .Where(domain => !string.IsNullOrWhiteSpace(domain))
        .Select(domain => domain.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    IReadOnlyList<DomainMigrationDesign> designs = generateAllDesigns
        ? designBuilder.BuildAll(intelligence)
        : targets.Select(domain => designBuilder.Build(intelligence, domain)).ToList();

    var outputPaths = new List<string>();
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    foreach (var design in designs)
    {
        var fileSlug = ToFileSlug(design.SelectedDomain);
        var jsonPath = Path.Combine(designOutputDirectory, $"{fileSlug}-migration-design.json");
        var markdownPath = Path.Combine(designOutputDirectory, $"{fileSlug}-migration-design.md");

        var jsonContent = JsonSerializer.Serialize(design, jsonOptions);
        await File.WriteAllTextAsync(jsonPath, jsonContent);
        await File.WriteAllTextAsync(markdownPath, RenderDesignMarkdown(design), Encoding.UTF8);

        outputPaths.Add(jsonPath);
        outputPaths.Add(markdownPath);
    }

    return new DesignOutputBundle
    {
        Designs = designs.ToList(),
        OutputPaths = outputPaths
    };
}

static string ToFileSlug(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "domain";
    }

    var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-");
    normalized = normalized.Trim('-');
    return string.IsNullOrWhiteSpace(normalized) ? "domain" : normalized;
}

static string RenderDesignMarkdown(DomainMigrationDesign design)
{
    var sb = new StringBuilder();
    sb.AppendLine($"# Migration Design: {design.SelectedDomain}");
    sb.AppendLine();
    sb.AppendLine("## Service Blueprint");
    sb.AppendLine($"- Bounded Context: `{design.ServiceBlueprint.BoundedContextName}`");
    sb.AppendLine($"- Cohesion Score: `{design.ServiceBlueprint.CohesionScore}`");
    sb.AppendLine($"- Coupling Score: `{design.ServiceBlueprint.CouplingScore}`");
    sb.AppendLine($"- Migration Readiness: `{design.ServiceBlueprint.MigrationReadinessLevel}` ({design.ServiceBlueprint.MigrationReadinessScore})");
    sb.AppendLine($"- Description: {design.ServiceBlueprint.Description}");
    sb.AppendLine();
    sb.AppendLine("## Boundary");
    sb.AppendLine($"- Confidence: `{design.ServiceBoundary.BoundaryConfidence:P0}`");
    sb.AppendLine($"- Rationale: {design.ServiceBoundary.BoundaryRationale}");
    sb.AppendLine($"- Controllers: {string.Join(", ", design.ServiceBoundary.Controllers)}");
    sb.AppendLine($"- Services: {string.Join(", ", design.ServiceBoundary.Services)}");
    sb.AppendLine($"- Repositories: {string.Join(", ", design.ServiceBoundary.Repositories)}");
    sb.AppendLine($"- Tables: {string.Join(", ", design.ServiceBoundary.Tables)}");
    sb.AppendLine();
    sb.AppendLine("## Contracts");
    sb.AppendLine($"- Public APIs: `{design.ServiceContract.PublicApis.Count}`");
    sb.AppendLine($"- Admin APIs: `{design.ServiceContract.AdminApis.Count}`");
    sb.AppendLine($"- Internal APIs: `{design.ServiceContract.InternalApis.Count}`");
    sb.AppendLine($"- Event Contracts: `{design.ServiceContract.EventContracts.Count}`");
    sb.AppendLine($"- Contract Completeness: `{design.ServiceContract.ContractCompleteness:P0}`");
    sb.AppendLine();
    sb.AppendLine("## Data Ownership");
    sb.AppendLine($"- Owned Tables: `{design.DataOwnershipPlan.OwnedTables.Count}`");
    sb.AppendLine($"- Shared Tables: `{design.DataOwnershipPlan.SharedTables.Count}`");
    sb.AppendLine($"- Referenced Tables: `{design.DataOwnershipPlan.ReferencedTables.Count}`");
    sb.AppendLine($"- Strategy: {design.DataOwnershipPlan.DatabaseSplitStrategy}");
    sb.AppendLine();
    sb.AppendLine("## Integrations");
    sb.AppendLine($"- Outbound Integrations: `{design.IntegrationBoundaryPlan.OutboundIntegrations.Count}`");
    sb.AppendLine($"- Inbound Integrations: `{design.IntegrationBoundaryPlan.InboundIntegrations.Count}`");
    sb.AppendLine($"- Internal Dependencies: `{design.IntegrationBoundaryPlan.InternalServiceDependencies.Count}`");
    sb.AppendLine($"- Needs ACL: `{design.IntegrationBoundaryPlan.NeedsAntiCorruptionLayer}`");
    sb.AppendLine();
    sb.AppendLine("## Strangler Plan");
    sb.AppendLine($"- Extraction Strategy: `{design.StranglerMigrationPlan.ExtractionStrategy}`");
    sb.AppendLine($"- Read-Only First: `{design.StranglerMigrationPlan.ReadOnlyFirstCandidate}`");
    sb.AppendLine($"- Staged Migration: `{design.StranglerMigrationPlan.StagedMigrationRecommended}`");
    sb.AppendLine($"- Phase Count: `{design.StranglerMigrationPlan.Phases.Count}`");
    sb.AppendLine();

    if (design.Blockers.Count > 0)
    {
        sb.AppendLine("## Blockers");
        foreach (var blocker in design.Blockers)
        {
            sb.AppendLine($"- {blocker}");
        }

        sb.AppendLine();
    }

    if (design.ReadinessNotes.Count > 0)
    {
        sb.AppendLine("## Readiness Notes");
        foreach (var note in design.ReadinessNotes)
        {
            sb.AppendLine($"- {note}");
        }

        sb.AppendLine();
    }

    sb.AppendLine("## Phases");
    foreach (var phase in design.StranglerMigrationPlan.Phases.OrderBy(phase => phase.PhaseOrder))
    {
        sb.AppendLine($"### {phase.PhaseOrder}. {phase.Name}");
        sb.AppendLine($"- Objective: {phase.Objective}");
        sb.AppendLine($"- Can Rollback: `{phase.CanRollback}`");
        sb.AppendLine($"- Rollback Strategy: {phase.RollbackStrategy}");
        if (phase.WorkItems.Count > 0)
        {
            sb.AppendLine("- Work Items:");
            foreach (var workItem in phase.WorkItems)
            {
                sb.AppendLine($"  - {workItem}");
            }
        }

        if (phase.ExitCriteria.Count > 0)
        {
            sb.AppendLine("- Exit Criteria:");
            foreach (var criterion in phase.ExitCriteria)
            {
                sb.AppendLine($"  - {criterion}");
            }
        }

        sb.AppendLine();
    }

    return sb.ToString();
}

static async Task<List<string>> WriteValidationOutputsAsync(
    PortfolioValidationReport report,
    string outputRoot)
{
    var validationDir = Path.Combine(outputRoot, "validation");
    Directory.CreateDirectory(validationDir);

    var jsonPath = Path.Combine(validationDir, "portfolio-validation.json");
    var markdownPath = Path.Combine(validationDir, "portfolio-validation.md");

    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(jsonPath, json);
    await File.WriteAllTextAsync(markdownPath, RenderValidationMarkdown(report), Encoding.UTF8);

    return new List<string> { jsonPath, markdownPath };
}

static string RenderValidationMarkdown(PortfolioValidationReport report)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Validation Report");
    sb.AppendLine();
    sb.AppendLine($"- Overall Score: `{report.OverallScore}`");
    sb.AppendLine($"- Has Errors: `{report.HasErrors}`");
    sb.AppendLine($"- Intelligence Score: `{report.IntelligenceReport.QualityScore}`");
    sb.AppendLine($"- Domain Report Count: `{report.DomainReports.Count}`");
    sb.AppendLine();

    sb.AppendLine("## Intelligence Issues");
    if (report.IntelligenceReport.Issues.Count == 0)
    {
        sb.AppendLine("- No issues detected.");
    }
    else
    {
        foreach (var issue in report.IntelligenceReport.Issues)
        {
            sb.AppendLine($"- [{issue.Severity}] {issue.Code}: {issue.Message}");
        }
    }

    sb.AppendLine();
    sb.AppendLine("## Domain Scores");
    sb.AppendLine("| Domain Scope | Score | Errors | Warnings |");
    sb.AppendLine("| --- | ---: | ---: | ---: |");
    foreach (var domainReport in report.DomainReports.OrderBy(item => item.Scope, StringComparer.OrdinalIgnoreCase))
    {
        var errors = domainReport.Issues.Count(issue => issue.Severity == Migration.Intelligence.Validation.Models.ValidationSeverity.Error);
        var warnings = domainReport.Issues.Count(issue => issue.Severity == Migration.Intelligence.Validation.Models.ValidationSeverity.Warning);
        sb.AppendLine($"| `{domainReport.Scope}` | `{domainReport.QualityScore}` | `{errors}` | `{warnings}` |");
    }

    return sb.ToString();
}

static async Task<List<string>> WriteAgentOutputsAsync(
    MigrationAgentReport report,
    string outputRoot)
{
    var agentsDir = Path.Combine(outputRoot, "agents");
    Directory.CreateDirectory(agentsDir);

    var jsonPath = Path.Combine(agentsDir, "migration-agent-plan.json");
    var markdownPath = Path.Combine(agentsDir, "migration-agent-plan.md");

    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(jsonPath, json);
    await File.WriteAllTextAsync(markdownPath, RenderAgentMarkdown(report), Encoding.UTF8);

    return new List<string> { jsonPath, markdownPath };
}

static string RenderAgentMarkdown(MigrationAgentReport report)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Migration Agent Plan");
    sb.AppendLine();
    sb.AppendLine($"- Mode: `{report.Mode}`");
    sb.AppendLine($"- AI Reasoning Applied: `{report.AiReasoningApplied}`");
    sb.AppendLine($"- Overall Confidence: `{report.OverallConfidenceScore}`");
    sb.AppendLine($"- Recommendation Count: `{report.Recommendations.Count}`");
    if (!string.IsNullOrWhiteSpace(report.AiSummary))
    {
        sb.AppendLine($"- AI Summary: {report.AiSummary}");
    }
    sb.AppendLine();

    sb.AppendLine("## Recommendations");
    sb.AppendLine("| Rank | Domain | Score | Strategy | Readiness |");
    sb.AppendLine("| ---: | --- | ---: | --- | --- |");
    foreach (var recommendation in report.Recommendations.OrderBy(item => item.Rank))
    {
        sb.AppendLine($"| {recommendation.Rank} | `{recommendation.Domain}` | `{recommendation.PriorityScore}` | `{recommendation.Strategy}` | `{recommendation.ReadinessLevel}` |");
    }

    foreach (var recommendation in report.Recommendations.OrderBy(item => item.Rank))
    {
        sb.AppendLine();
        sb.AppendLine($"### {recommendation.Rank}. {recommendation.Domain}");
        if (recommendation.Reasons.Count > 0)
        {
            sb.AppendLine("- Reasons:");
            foreach (var reason in recommendation.Reasons)
            {
                sb.AppendLine($"  - {reason}");
            }
        }

        if (recommendation.ActionItems.Count > 0)
        {
            sb.AppendLine("- Action Items:");
            foreach (var actionItem in recommendation.ActionItems)
            {
                sb.AppendLine($"  - (P{actionItem.Priority}) {actionItem.Title}: {actionItem.Description}");
            }
        }
    }

    return sb.ToString();
}

sealed class DesignOutputBundle
{
    public required List<DomainMigrationDesign> Designs { get; init; }
    public required List<string> OutputPaths { get; init; }
}
