using Migration.Intelligence.Contracts.Orchestration;

namespace Migration.Intelligence.Contracts.Reporting;

public class AnalysisReportContract
{
    public required MigrationExecutionContract Execution { get; init; }
    public required string MarkdownReportPath { get; init; }
    public required string JsonReportPath { get; init; }
}
