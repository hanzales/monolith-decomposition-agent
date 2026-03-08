using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Results;

namespace Migration.Intelligence.Core.Pipeline;

public class AnalysisPipelineBuilder
{
    private IRepoScanner? _repoScanner;
    private ICodeAnalyzer? _codeAnalyzer;
    private IDomainInferenceEngine? _domainInferenceEngine;
    private IMigrationIntelligenceAnalyzer? _migrationIntelligenceAnalyzer;
    private ITargetProjectWriter? _targetProjectWriter;
    private IReportGenerator? _reportGenerator;

    public AnalysisPipelineBuilder UseRepoScanner(IRepoScanner repoScanner)
    {
        _repoScanner = repoScanner;
        return this;
    }

    public AnalysisPipelineBuilder UseCodeAnalyzer(ICodeAnalyzer codeAnalyzer)
    {
        _codeAnalyzer = codeAnalyzer;
        return this;
    }

    public AnalysisPipelineBuilder UseDomainInference(IDomainInferenceEngine domainInferenceEngine)
    {
        _domainInferenceEngine = domainInferenceEngine;
        return this;
    }

    public AnalysisPipelineBuilder UseMigrationIntelligenceAnalyzer(IMigrationIntelligenceAnalyzer migrationIntelligenceAnalyzer)
    {
        _migrationIntelligenceAnalyzer = migrationIntelligenceAnalyzer;
        return this;
    }

    public AnalysisPipelineBuilder UseTargetProjectWriter(ITargetProjectWriter targetProjectWriter)
    {
        _targetProjectWriter = targetProjectWriter;
        return this;
    }

    public AnalysisPipelineBuilder UseReportGenerator(IReportGenerator reportGenerator)
    {
        _reportGenerator = reportGenerator;
        return this;
    }

    public ResultT<AnalysisPipeline> Build()
    {
        if (_repoScanner is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Repo scanner is not configured.");
        }

        if (_codeAnalyzer is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Code analyzer is not configured.");
        }

        if (_domainInferenceEngine is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Domain inference engine is not configured.");
        }

        if (_targetProjectWriter is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Target project writer is not configured.");
        }

        if (_migrationIntelligenceAnalyzer is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Migration intelligence analyzer is not configured.");
        }

        if (_reportGenerator is null)
        {
            return ResultT<AnalysisPipeline>.Failure("Report generator is not configured.");
        }

        var pipeline = new AnalysisPipeline(
            _repoScanner,
            _codeAnalyzer,
            _domainInferenceEngine,
            _migrationIntelligenceAnalyzer,
            _targetProjectWriter,
            _reportGenerator);

        return ResultT<AnalysisPipeline>.Success(pipeline);
    }
}
