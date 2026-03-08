namespace Migration.Intelligence.Core.Options;

public class AnalysisOptions
{
    public required string SourcePath { get; init; }
    public required string TargetPath { get; init; }
    public List<string> ArchitectureMarkdownPaths { get; init; } = new();
    public bool DryRun { get; init; }
    public ScannerOptions Scanner { get; init; } = new();
    public CodeAnalysisOptions CodeAnalysis { get; init; } = new();
    public DomainInferenceOptions DomainInference { get; init; } = new();
    public ReportingOptions Reporting { get; init; } = new();
}
