namespace Migration.Intelligence.Core.Options;

public class CodeAnalysisOptions
{
    public int MaxFileReadBytes { get; init; } = 1_000_000;
    public bool IncludeGeneratedFiles { get; init; }
    public bool EnableLegacyRiskDetection { get; init; } = true;
    public bool EnableSqlUsageDetection { get; init; } = true;
}
