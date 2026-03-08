using System.Text;
using System.Text.Json;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Abstractions;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Services;

public sealed class ArtifactTemplateGenerator : IArtifactTemplateGenerator
{
    private readonly IBacklogGenerator _backlogGenerator;

    public ArtifactTemplateGenerator(IBacklogGenerator backlogGenerator)
    {
        _backlogGenerator = backlogGenerator;
    }

    public DomainGenerationPackage Generate(DomainMigrationDesign design, ValidationReport? validationReport = null)
    {
        ArgumentNullException.ThrowIfNull(design);

        var backlogItems = _backlogGenerator.Generate(design, validationReport).ToList();
        var artifacts = new List<GeneratedArtifact>
        {
            new()
            {
                FileName = "migration-design-summary.md",
                RelativePath = "docs/migration-design-summary.md",
                ContentType = "markdown",
                Content = BuildSummaryMarkdown(design, validationReport)
            },
            new()
            {
                FileName = "api-contract-catalog.md",
                RelativePath = "docs/api-contract-catalog.md",
                ContentType = "markdown",
                Content = BuildApiCatalogMarkdown(design)
            },
            new()
            {
                FileName = "database-transition-plan.md",
                RelativePath = "docs/database-transition-plan.md",
                ContentType = "markdown",
                Content = BuildDatabasePlanMarkdown(design)
            },
            new()
            {
                FileName = "strangler-runbook.md",
                RelativePath = "docs/strangler-runbook.md",
                ContentType = "markdown",
                Content = BuildRunbookMarkdown(design)
            },
            new()
            {
                FileName = "migration-backlog.json",
                RelativePath = "planning/migration-backlog.json",
                ContentType = "json",
                Content = JsonSerializer.Serialize(backlogItems, new JsonSerializerOptions { WriteIndented = true })
            }
        };

        return new DomainGenerationPackage
        {
            Domain = design.SelectedDomain,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Artifacts = artifacts,
            BacklogItems = backlogItems
        };
    }

    private static string BuildSummaryMarkdown(DomainMigrationDesign design, ValidationReport? validationReport)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Migration Design Summary - {design.SelectedDomain}");
        sb.AppendLine();
        sb.AppendLine($"- Bounded Context: `{design.ServiceBlueprint.BoundedContextName}`");
        sb.AppendLine($"- Readiness: `{design.ServiceBlueprint.MigrationReadinessLevel}` ({design.ServiceBlueprint.MigrationReadinessScore})");
        sb.AppendLine($"- Cohesion/Coupling: `{design.ServiceBlueprint.CohesionScore}` / `{design.ServiceBlueprint.CouplingScore}`");
        sb.AppendLine($"- Boundary Confidence: `{design.ServiceBoundary.BoundaryConfidence:P0}`");
        sb.AppendLine($"- Strategy: `{design.StranglerMigrationPlan.ExtractionStrategy}`");
        sb.AppendLine();

        if (validationReport is not null)
        {
            sb.AppendLine("## Validation");
            sb.AppendLine($"- Score: `{validationReport.QualityScore}`");
            sb.AppendLine($"- Errors: `{validationReport.Issues.Count(i => i.Severity == ValidationSeverity.Error)}`");
            sb.AppendLine($"- Warnings: `{validationReport.Issues.Count(i => i.Severity == ValidationSeverity.Warning)}`");
            sb.AppendLine();
        }

        sb.AppendLine("## Key Blockers");
        if (design.Blockers.Count == 0)
        {
            sb.AppendLine("- No blockers were reported.");
        }
        else
        {
            foreach (var blocker in design.Blockers)
            {
                sb.AppendLine($"- {blocker}");
            }
        }

        return sb.ToString();
    }

    private static string BuildApiCatalogMarkdown(DomainMigrationDesign design)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# API Contract Catalog - {design.SelectedDomain}");
        sb.AppendLine();
        sb.AppendLine("| Exposure | Method | Route | Controller | Action |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        var entries = design.ServiceContract.PublicApis
            .Select(item => ("Public", item))
            .Concat(design.ServiceContract.AdminApis.Select(item => ("Admin", item)))
            .Concat(design.ServiceContract.InternalApis.Select(item => ("Internal", item)))
            .OrderBy(entry => entry.Item2.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Item2.Controller, StringComparer.OrdinalIgnoreCase);

        foreach (var (exposure, endpoint) in entries)
        {
            sb.AppendLine($"| {exposure} | {endpoint.HttpMethod} | `{endpoint.Route}` | `{endpoint.Controller}` | `{endpoint.Action}` |");
        }

        if (design.ServiceContract.EventContracts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Event Contracts");
            sb.AppendLine("| Direction | Name | Queue/Topic | Related Domain | Confidence |");
            sb.AppendLine("| --- | --- | --- | --- | ---: |");
            foreach (var evt in design.ServiceContract.EventContracts.OrderBy(evt => evt.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"| {evt.Direction} | `{evt.Name}` | `{evt.QueueOrTopic}` | `{evt.RelatedDomain}` | `{evt.Confidence:F2}` |");
            }
        }

        return sb.ToString();
    }

    private static string BuildDatabasePlanMarkdown(DomainMigrationDesign design)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Database Transition Plan - {design.SelectedDomain}");
        sb.AppendLine();
        sb.AppendLine($"- Strategy: {design.DataOwnershipPlan.DatabaseSplitStrategy}");
        sb.AppendLine();
        sb.AppendLine("| Table | Role | Access | Shared | Confidence | Can Move |");
        sb.AppendLine("| --- | --- | --- | --- | ---: | --- |");

        var tables = design.DataOwnershipPlan.OwnedTables
            .Concat(design.DataOwnershipPlan.SharedTables)
            .Concat(design.DataOwnershipPlan.ReferencedTables)
            .OrderBy(table => table.TableName, StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            sb.AppendLine($"| `{table.TableName}` | {table.Role} | {table.AccessType} | `{table.IsShared}` | `{table.Confidence:F2}` | `{table.CanMoveIndependently}` |");
        }

        return sb.ToString();
    }

    private static string BuildRunbookMarkdown(DomainMigrationDesign design)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Strangler Runbook - {design.SelectedDomain}");
        sb.AppendLine();

        foreach (var phase in design.StranglerMigrationPlan.Phases.OrderBy(phase => phase.PhaseOrder))
        {
            sb.AppendLine($"## {phase.PhaseOrder}. {phase.Name}");
            sb.AppendLine($"- Objective: {phase.Objective}");
            sb.AppendLine($"- Rollback: {(phase.CanRollback ? "Yes" : "No")} - {phase.RollbackStrategy}");
            sb.AppendLine("- Work Items:");
            foreach (var item in phase.WorkItems)
            {
                sb.AppendLine($"  - {item}");
            }

            sb.AppendLine("- Exit Criteria:");
            foreach (var criterion in phase.ExitCriteria)
            {
                sb.AppendLine($"  - {criterion}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
