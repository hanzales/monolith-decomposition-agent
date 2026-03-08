using Migration.Intelligence.Contracts.Common;

namespace Migration.Intelligence.Contracts.Analysis;

public sealed class LegacyRiskContract
{
    public required string RuleId { get; init; }
    public required string Description { get; init; }
    public required string RelativePath { get; init; }
    public RiskLevel Level { get; init; } = RiskLevel.Low;
}
