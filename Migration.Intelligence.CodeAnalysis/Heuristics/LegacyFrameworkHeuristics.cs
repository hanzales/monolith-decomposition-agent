using Migration.Intelligence.Contracts.Analysis;
using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.CodeAnalysis.Heuristics;

public sealed class LegacyFrameworkHeuristics
{
    public IReadOnlyCollection<LegacyRiskContract> Evaluate(string relativePath, string content)
    {
        var risks = new List<LegacyRiskContract>();

        if (content.Contains("System.Web", StringComparison.Ordinal))
        {
            risks.Add(CreateRisk("LEGACY_WEB", "Uses System.Web namespace.", relativePath, RiskLevel.High));
        }

        if (content.Contains("HttpContext.Current", StringComparison.Ordinal))
        {
            risks.Add(CreateRisk("LEGACY_CONTEXT", "Uses static HttpContext.Current access.", relativePath, RiskLevel.Medium));
        }

        if (content.Contains("packages.config", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith("packages.config", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add(CreateRisk("LEGACY_PACKAGES_CONFIG", "Uses legacy packages.config dependency management.", relativePath, RiskLevel.Medium));
        }

        return risks;
    }

    private static LegacyRiskContract CreateRisk(string ruleId, string description, string relativePath, RiskLevel level)
    {
        return new LegacyRiskContract
        {
            RuleId = ruleId,
            Description = description,
            RelativePath = relativePath,
            Level = level
        };
    }
}
