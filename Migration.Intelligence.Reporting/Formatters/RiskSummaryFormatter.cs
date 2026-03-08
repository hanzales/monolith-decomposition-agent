using Migration.Intelligence.Contracts.Orchestration;

namespace Migration.Intelligence.Reporting.Formatters;

public sealed class RiskSummaryFormatter
{
    public List<string> Format(CodeInsightsContract insights)
    {
        var lines = new List<string>
        {
            $"- Legacy Risk Findings: `{insights.LegacyRiskCount}`",
            $"- External Dependency Signals: `{insights.ExternalCallCount}`",
            $"- Endpoint Count: `{insights.EndpointCount}`",
            $"- Data/Table Usage Findings: `{insights.TableUsageCount}`"
        };

        if (insights.TopRiskFiles.Count > 0)
        {
            lines.Add("- Top Risk Files:");
            lines.AddRange(insights.TopRiskFiles.Select(file => $"  - `{file}`"));
        }

        return lines;
    }
}
