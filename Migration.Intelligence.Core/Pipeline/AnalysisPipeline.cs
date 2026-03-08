using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Contracts.Reporting;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Core.Pipeline;

public class AnalysisPipeline
{
    private readonly IRepoScanner _repoScanner;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly IDomainInferenceEngine _domainInferenceEngine;
    private readonly IMigrationIntelligenceAnalyzer _migrationIntelligenceAnalyzer;
    private readonly ITargetProjectWriter _targetProjectWriter;
    private readonly IReportGenerator _reportGenerator;

    public AnalysisPipeline(
        IRepoScanner repoScanner,
        ICodeAnalyzer codeAnalyzer,
        IDomainInferenceEngine domainInferenceEngine,
        IMigrationIntelligenceAnalyzer migrationIntelligenceAnalyzer,
        ITargetProjectWriter targetProjectWriter,
        IReportGenerator reportGenerator)
    {
        _repoScanner = repoScanner;
        _codeAnalyzer = codeAnalyzer;
        _domainInferenceEngine = domainInferenceEngine;
        _migrationIntelligenceAnalyzer = migrationIntelligenceAnalyzer;
        _targetProjectWriter = targetProjectWriter;
        _reportGenerator = reportGenerator;
    }

    public async Task<AnalysisReportContract> ExecuteAsync(
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var context = new AnalysisContext { Options = options };

        context.Inventory = await _repoScanner.ScanAsync(options, cancellationToken);
        context.Insights = await _codeAnalyzer.AnalyzeAsync(context.Inventory, options, cancellationToken);
        context.ServiceBlueprints = await _domainInferenceEngine.InferServicesAsync(
            context.Inventory,
            context.Insights,
            options,
            cancellationToken);
        context.Intelligence = await _migrationIntelligenceAnalyzer.AnalyzeAsync(
            context.Inventory,
            context.Insights,
            context.ServiceBlueprints,
            options,
            cancellationToken);

        if (!options.DryRun)
        {
            await _targetProjectWriter.WriteAsync(context.ServiceBlueprints, options, cancellationToken);
        }

        var execution = new MigrationExecutionContract
        {
            SourcePath = options.SourcePath,
            TargetPath = options.TargetPath,
            ArchitectureDocs = options.ArchitectureMarkdownPaths,
            Inventory = context.Inventory,
            Insights = context.Insights,
            Intelligence = context.Intelligence,
            ServiceBlueprints = context.ServiceBlueprints,
            DryRun = options.DryRun,
            ExecutedAtUtc = DateTimeOffset.UtcNow
        };

        return await _reportGenerator.GenerateAsync(execution, options, cancellationToken);
    }
}
