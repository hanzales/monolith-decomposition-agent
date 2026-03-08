using Migration.Intelligence.CodeAnalysis.Heuristics;
using Migration.Intelligence.Contracts.Analysis;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class LegacyRiskAnalyzer
{
    private readonly LegacyFrameworkHeuristics _heuristics;

    public LegacyRiskAnalyzer(LegacyFrameworkHeuristics heuristics)
    {
        _heuristics = heuristics;
    }

    public async Task<List<LegacyRiskContract>> AnalyzeAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.CodeAnalysis.EnableLegacyRiskDetection)
        {
            return new List<LegacyRiskContract>();
        }

        var risks = new List<LegacyRiskContract>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            risks.AddRange(_heuristics.Evaluate(sourceFile.RelativePath, content));
        }

        // Detect known legacy artifacts from markdown/metadata files too.
        foreach (var markdownFile in inventory.MarkdownFiles)
        {
            if (markdownFile.RelativePath.Contains("webforms", StringComparison.OrdinalIgnoreCase)
                || markdownFile.RelativePath.Contains("aspnet", StringComparison.OrdinalIgnoreCase))
            {
                risks.Add(new LegacyRiskContract
                {
                    RuleId = "LEGACY_ARTIFACT",
                    Description = "Legacy technology hint detected in markdown assets.",
                    RelativePath = markdownFile.RelativePath,
                    Level = Contracts.Common.RiskLevel.Low
                });
            }
        }

        return risks;
    }
}
