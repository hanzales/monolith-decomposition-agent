using Migration.Intelligence.Agents.Abstractions;
using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Agents.Services;

public sealed class DomainPrioritizationAgent : IDomainPrioritizationAgent
{
    public IReadOnlyList<AgentRecommendation> RankDomains(
        MigrationIntelligenceContract intelligence,
        IReadOnlyCollection<DomainMigrationDesign> designs,
        PortfolioValidationReport? validationReport = null)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(designs);

        var validationByScope = validationReport?.DomainReports
            .ToDictionary(report => report.Scope, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ValidationReport>(StringComparer.OrdinalIgnoreCase);

        var recommendations = new List<AgentRecommendation>();
        foreach (var design in designs)
        {
            var scope = $"DomainDesign:{design.SelectedDomain}";
            validationByScope.TryGetValue(scope, out var domainValidation);

            var score = CalculatePriorityScore(design, domainValidation);
            var reasons = BuildReasons(design, domainValidation, score);

            recommendations.Add(new AgentRecommendation
            {
                Domain = design.SelectedDomain,
                PriorityScore = score,
                Strategy = design.StranglerMigrationPlan.ExtractionStrategy,
                ReadinessLevel = design.ServiceBlueprint.MigrationReadinessLevel,
                Reasons = reasons,
                Blockers = design.Blockers.ToList()
            });
        }

        var ordered = recommendations
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Rank = index + 1;
        }

        return ordered;
    }

    private static int CalculatePriorityScore(DomainMigrationDesign design, ValidationReport? validationReport)
    {
        var readiness = design.ServiceBlueprint.MigrationReadinessScore;
        var confidenceBoost = (int)Math.Round(design.ServiceBoundary.BoundaryConfidence * 25);
        var couplingPenalty = (int)Math.Round(design.ServiceBlueprint.CouplingScore * 0.45);
        var blockerPenalty = design.Blockers.Count * 6;
        var sharedDataPenalty = design.DataOwnershipPlan.SharedTables.Count * 5;
        var unknownChainPenalty = design.ServiceBoundary.ExecutionChains.Count(chain => !chain.IsComplete) * 4;
        var validationPenalty = validationReport is null
            ? 0
            : validationReport.Issues.Sum(issue => issue.Severity switch
            {
                ValidationSeverity.Error => 8,
                ValidationSeverity.Warning => 3,
                _ => 1
            });

        var strategyModifier = design.StranglerMigrationPlan.ExtractionStrategy switch
        {
            ExtractionStrategy.DirectExtraction => 8,
            ExtractionStrategy.ReadOnlyFirst => 5,
            ExtractionStrategy.EventCarveOut => 4,
            ExtractionStrategy.StranglerFigPhased => 1,
            ExtractionStrategy.DeferredDueToCoupling => -10,
            _ => 0
        };

        var raw = readiness
                  + confidenceBoost
                  + strategyModifier
                  - couplingPenalty
                  - blockerPenalty
                  - sharedDataPenalty
                  - unknownChainPenalty
                  - validationPenalty;

        return Math.Max(0, Math.Min(100, raw));
    }

    private static List<string> BuildReasons(
        DomainMigrationDesign design,
        ValidationReport? validationReport,
        int priorityScore)
    {
        var reasons = new List<string>
        {
            $"Priority score computed as {priorityScore}.",
            $"Boundary confidence: {design.ServiceBoundary.BoundaryConfidence:P0}.",
            $"Readiness score: {design.ServiceBlueprint.MigrationReadinessScore} ({design.ServiceBlueprint.MigrationReadinessLevel}).",
            $"Shared tables: {design.DataOwnershipPlan.SharedTables.Count}.",
            $"Blockers: {design.Blockers.Count}.",
            $"Extraction strategy: {design.StranglerMigrationPlan.ExtractionStrategy}."
        };

        if (validationReport is not null)
        {
            reasons.Add($"Validation score: {validationReport.QualityScore}.");
        }

        return reasons;
    }
}
