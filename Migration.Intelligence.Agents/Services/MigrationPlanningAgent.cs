using Migration.Intelligence.Agents.Abstractions;
using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Contracts.MigrationIntelligence;
using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Agents.Services;

public sealed class MigrationPlanningAgent : IMigrationPlanningAgent
{
    private readonly IDomainPrioritizationAgent _prioritizationAgent;
    private readonly IBlockerResolutionAdvisor _blockerResolutionAdvisor;
    private readonly IAgentReasoner _deterministicReasoner;
    private readonly IAgentReasoner _llmReasoner;

    public MigrationPlanningAgent(
        IDomainPrioritizationAgent prioritizationAgent,
        IBlockerResolutionAdvisor blockerResolutionAdvisor,
        IAgentReasoner deterministicReasoner,
        IAgentReasoner llmReasoner)
    {
        _prioritizationAgent = prioritizationAgent;
        _blockerResolutionAdvisor = blockerResolutionAdvisor;
        _deterministicReasoner = deterministicReasoner;
        _llmReasoner = llmReasoner;
    }

    public async Task<MigrationAgentReport> CreatePlanAsync(
        MigrationIntelligenceContract intelligence,
        IReadOnlyCollection<DomainMigrationDesign> designs,
        AgentPlanningOptions? planningOptions = null,
        PortfolioValidationReport? validationReport = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(designs);

        planningOptions ??= new AgentPlanningOptions();

        var ranked = _prioritizationAgent
            .RankDomains(intelligence, designs, validationReport)
            .Select(item =>
            {
                item.ActionItems = _blockerResolutionAdvisor.BuildActions(item).ToList();
                return item;
            })
            .ToList();

        var reasoningResult = await ApplyReasoningAsync(
            planningOptions,
            intelligence,
            designs,
            ranked,
            validationReport,
            cancellationToken);

        ApplyAdvice(ranked, reasoningResult);

        var ordered = ranked
            .OrderByDescending(item => item.PriorityScore)
            .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Rank = index + 1;
        }

        var globalNotes = BuildGlobalNotes(intelligence, validationReport, ordered, reasoningResult);
        var confidence = CalculateConfidence(validationReport, ordered, reasoningResult);

        return new MigrationAgentReport
        {
            Recommendations = ordered,
            GlobalNotes = globalNotes,
            OverallConfidenceScore = confidence,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Mode = planningOptions.Mode,
            AiReasoningApplied = reasoningResult.IsSuccessful && reasoningResult.Mode == AgentMode.Llm,
            AiSummary = reasoningResult.Summary
        };
    }

    private async Task<AgentReasoningResult> ApplyReasoningAsync(
        AgentPlanningOptions planningOptions,
        MigrationIntelligenceContract intelligence,
        IReadOnlyCollection<DomainMigrationDesign> designs,
        IReadOnlyCollection<AgentRecommendation> ranked,
        PortfolioValidationReport? validationReport,
        CancellationToken cancellationToken)
    {
        var request = new AgentReasoningRequest
        {
            PlanningOptions = planningOptions,
            Intelligence = intelligence,
            Designs = designs,
            BaseRecommendations = ranked,
            ValidationReport = validationReport
        };

        if (planningOptions.Mode == AgentMode.Llm)
        {
            var llmResult = await _llmReasoner.ReasonAsync(request, cancellationToken);
            if (llmResult.IsSuccessful)
            {
                return llmResult;
            }

            var fallback = await _deterministicReasoner.ReasonAsync(request, cancellationToken);
            fallback.Summary = $"LLM fallback to deterministic: {llmResult.FailureReason}";
            return fallback;
        }

        return await _deterministicReasoner.ReasonAsync(request, cancellationToken);
    }

    private static void ApplyAdvice(
        IList<AgentRecommendation> recommendations,
        AgentReasoningResult reasoningResult)
    {
        if (!reasoningResult.IsSuccessful || reasoningResult.DomainAdvice.Count == 0)
        {
            return;
        }

        foreach (var advice in reasoningResult.DomainAdvice)
        {
            var recommendation = recommendations.FirstOrDefault(item =>
                item.Domain.Equals(advice.Domain, StringComparison.OrdinalIgnoreCase));
            if (recommendation is null)
            {
                continue;
            }

            recommendation.PriorityScore = Math.Max(0, Math.Min(100, recommendation.PriorityScore + advice.PriorityAdjustment));
            if (advice.SuggestedStrategy.HasValue)
            {
                recommendation.Strategy = advice.SuggestedStrategy.Value;
            }

            foreach (var reason in advice.AdditionalReasons.Where(reason => !string.IsNullOrWhiteSpace(reason)))
            {
                recommendation.Reasons.Add($"AI: {reason}");
            }

            if (advice.AdditionalActions.Count > 0)
            {
                recommendation.ActionItems = recommendation.ActionItems
                    .Concat(advice.AdditionalActions)
                    .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.OrderBy(action => action.Priority).First())
                    .OrderBy(action => action.Priority)
                    .ThenBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }

    private static List<string> BuildGlobalNotes(
        MigrationIntelligenceContract intelligence,
        PortfolioValidationReport? validationReport,
        IReadOnlyCollection<AgentRecommendation> recommendations,
        AgentReasoningResult reasoningResult)
    {
        var notes = new List<string>
        {
            $"Analyzed business domains: {intelligence.BusinessDomainCandidates.Count}.",
            $"Design recommendations produced: {recommendations.Count}."
        };

        if (validationReport is not null)
        {
            notes.Add($"Validation overall score: {validationReport.OverallScore}.");
            if (validationReport.HasErrors)
            {
                notes.Add("Validation detected blocking errors; top recommendations should be treated as conditional.");
            }
        }

        var deferredCount = recommendations.Count(item => item.Strategy == ExtractionStrategy.DeferredDueToCoupling);
        if (deferredCount > 0)
        {
            notes.Add($"{deferredCount} domain(s) deferred due to coupling/legacy risk.");
        }

        if (reasoningResult.IsSuccessful && !string.IsNullOrWhiteSpace(reasoningResult.Summary))
        {
            notes.Add($"Reasoning summary: {reasoningResult.Summary}");
        }
        else if (!reasoningResult.IsSuccessful && !string.IsNullOrWhiteSpace(reasoningResult.FailureReason))
        {
            notes.Add($"Reasoning note: {reasoningResult.FailureReason}");
        }

        return notes;
    }

    private static int CalculateConfidence(
        PortfolioValidationReport? validationReport,
        IReadOnlyCollection<AgentRecommendation> recommendations,
        AgentReasoningResult reasoningResult)
    {
        if (recommendations.Count == 0)
        {
            return 0;
        }

        var avgPriority = recommendations.Average(item => item.PriorityScore);
        var validationScore = validationReport?.OverallScore ?? 65;
        var errorPenalty = validationReport?.HasErrors == true ? 12 : 0;
        var reasoningModifier = reasoningResult.Mode == AgentMode.Llm && reasoningResult.IsSuccessful ? 5 : 0;

        var score = (avgPriority * 0.6) + (validationScore * 0.4) - errorPenalty + reasoningModifier;
        return Math.Max(0, Math.Min(100, (int)Math.Round(score, MidpointRounding.AwayFromZero)));
    }
}
