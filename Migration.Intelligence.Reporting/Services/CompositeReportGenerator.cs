using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Contracts.Reporting;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Reporting.Services;

public sealed class CompositeReportGenerator : IReportGenerator
{
    private readonly MarkdownReportWriter _markdownWriter;
    private readonly JsonReportWriter _jsonWriter;

    public CompositeReportGenerator(
        MarkdownReportWriter markdownWriter,
        JsonReportWriter jsonWriter)
    {
        _markdownWriter = markdownWriter;
        _jsonWriter = jsonWriter;
    }

    public async Task<AnalysisReportContract> GenerateAsync(
        MigrationExecutionContract execution,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var outputDir = options.TargetPath;
        Directory.CreateDirectory(outputDir);

        var markdownPath = await _markdownWriter.WriteAsync(execution, outputDir, cancellationToken);
        var jsonPath = await _jsonWriter.WriteAsync(execution, outputDir, cancellationToken);

        return new AnalysisReportContract
        {
            Execution = execution,
            MarkdownReportPath = markdownPath,
            JsonReportPath = jsonPath
        };
    }
}
