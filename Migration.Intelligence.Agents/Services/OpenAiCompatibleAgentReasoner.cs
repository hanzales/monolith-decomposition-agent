using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Migration.Intelligence.Agents.Abstractions;
using Migration.Intelligence.Agents.Models;
using Migration.Intelligence.Design.Models;

namespace Migration.Intelligence.Agents.Services;

/// <summary>
/// OpenAI-compatible LLM reasoner for recommendation refinement.
/// </summary>
public sealed class OpenAiCompatibleAgentReasoner : IAgentReasoner
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleAgentReasoner(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<AgentReasoningResult> ReasonAsync(
        AgentReasoningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = request.PlanningOptions.Llm;

        var apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AgentReasoningResult
            {
                Mode = AgentMode.Llm,
                IsSuccessful = false,
                FailureReason = "Missing API key. Provide --llm-api-key or set OPENAI_API_KEY."
            };
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(15, options.TimeoutSeconds)));

            var payload = BuildPayload(request, options);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, timeoutCts.Token);
            var raw = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new AgentReasoningResult
                {
                    Mode = AgentMode.Llm,
                    IsSuccessful = false,
                    FailureReason = $"LLM request failed ({(int)response.StatusCode}): {Trim(raw, 500)}"
                };
            }

            var content = ExtractContent(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new AgentReasoningResult
                {
                    Mode = AgentMode.Llm,
                    IsSuccessful = false,
                    FailureReason = "LLM response did not include message content."
                };
            }

            return ParseReasoningResult(content);
        }
        catch (Exception ex)
        {
            return new AgentReasoningResult
            {
                Mode = AgentMode.Llm,
                IsSuccessful = false,
                FailureReason = $"LLM reasoning failed: {ex.Message}"
            };
        }
    }

    private static string BuildPayload(AgentReasoningRequest request, LlmAgentOptions options)
    {
        var systemPrompt =
            "You are a migration planning agent. Return strict JSON only with fields: summary, advice[]. " +
            "Each advice item: domain, priorityAdjustment (integer -15..15), suggestedStrategy " +
            "(DirectExtraction|ReadOnlyFirst|EventCarveOut|StranglerFigPhased|DeferredDueToCoupling), " +
            "additionalReasons[] (max 3), additionalActions[] with title/category/description/priority(1..3). " +
            "Use deterministic and conservative guidance. No markdown.";

        var userPrompt = BuildUserPrompt(request);
        var payload = new
        {
            model = options.Model,
            temperature = options.Temperature,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string BuildUserPrompt(AgentReasoningRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Refine and prioritize migration recommendations.");
        sb.AppendLine($"Domain count: {request.BaseRecommendations.Count}");
        sb.AppendLine();
        sb.AppendLine("Current recommendations:");
        foreach (var recommendation in request.BaseRecommendations.OrderBy(item => item.Rank))
        {
            sb.AppendLine($"- Domain={recommendation.Domain}; Rank={recommendation.Rank}; Score={recommendation.PriorityScore}; Strategy={recommendation.Strategy}; Blockers={recommendation.Blockers.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("Domain design signals:");
        foreach (var design in request.Designs.OrderBy(item => item.SelectedDomain, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {design.SelectedDomain}: boundaryConfidence={design.ServiceBoundary.BoundaryConfidence:F2}, " +
                          $"sharedTables={design.DataOwnershipPlan.SharedTables.Count}, " +
                          $"integrations={design.IntegrationBoundaryPlan.OutboundIntegrations.Count + design.IntegrationBoundaryPlan.InboundIntegrations.Count}, " +
                          $"readiness={design.ServiceBlueprint.MigrationReadinessScore}");
        }

        if (request.ValidationReport is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Validation overall score: {request.ValidationReport.OverallScore}");
            foreach (var domainReport in request.ValidationReport.DomainReports)
            {
                sb.AppendLine($"- {domainReport.Scope}: score={domainReport.QualityScore}, issues={domainReport.Issues.Count}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Return JSON with summary and advice list. Do not include domains not listed above.");
        return sb.ToString();
    }

    private static string ExtractContent(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var message = choices[0].GetProperty("message");
        return message.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty;
    }

    private static AgentReasoningResult ParseReasoningResult(string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var summary = root.TryGetProperty("summary", out var summaryProp)
            ? summaryProp.GetString() ?? string.Empty
            : string.Empty;

        var advice = new List<DomainReasoningAdvice>();
        if (root.TryGetProperty("advice", out var adviceArray) && adviceArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in adviceArray.EnumerateArray())
            {
                var domain = item.TryGetProperty("domain", out var domainProp)
                    ? domainProp.GetString() ?? string.Empty
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                var priorityAdjustment = item.TryGetProperty("priorityAdjustment", out var adjustmentProp) &&
                                         adjustmentProp.TryGetInt32(out var parsedAdjustment)
                    ? Math.Max(-15, Math.Min(15, parsedAdjustment))
                    : 0;

                var strategy = item.TryGetProperty("suggestedStrategy", out var strategyProp)
                    ? ParseStrategy(strategyProp.GetString())
                    : null;

                var reasons = new List<string>();
                if (item.TryGetProperty("additionalReasons", out var reasonsProp)
                    && reasonsProp.ValueKind == JsonValueKind.Array)
                {
                    reasons.AddRange(reasonsProp.EnumerateArray()
                        .Select(reason => reason.GetString() ?? string.Empty)
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Take(3));
                }

                var actions = new List<AgentActionItem>();
                if (item.TryGetProperty("additionalActions", out var actionsProp)
                    && actionsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var action in actionsProp.EnumerateArray())
                    {
                        var title = action.TryGetProperty("title", out var titleProp)
                            ? titleProp.GetString() ?? string.Empty
                            : string.Empty;
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            continue;
                        }

                        var category = action.TryGetProperty("category", out var categoryProp)
                            ? categoryProp.GetString() ?? string.Empty
                            : string.Empty;
                        var description = action.TryGetProperty("description", out var descProp)
                            ? descProp.GetString() ?? string.Empty
                            : string.Empty;
                        var priority = action.TryGetProperty("priority", out var priorityProp)
                                       && priorityProp.TryGetInt32(out var parsedPriority)
                            ? Math.Max(1, Math.Min(3, parsedPriority))
                            : 2;

                        actions.Add(new AgentActionItem
                        {
                            Title = title,
                            Category = category,
                            Description = description,
                            Priority = priority
                        });
                    }
                }

                advice.Add(new DomainReasoningAdvice
                {
                    Domain = domain,
                    PriorityAdjustment = priorityAdjustment,
                    SuggestedStrategy = strategy,
                    AdditionalReasons = reasons,
                    AdditionalActions = actions
                });
            }
        }

        return new AgentReasoningResult
        {
            Mode = AgentMode.Llm,
            IsSuccessful = true,
            Summary = summary,
            DomainAdvice = advice
        };
    }

    private static ExtractionStrategy? ParseStrategy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ExtractionStrategy>(value, ignoreCase: true, out var strategy)
            ? strategy
            : null;
    }

    private static string Trim(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }
}
