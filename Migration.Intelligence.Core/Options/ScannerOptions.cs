namespace Migration.Intelligence.Core.Options;

public class ScannerOptions
{
    public List<string> ExcludedDirectories { get; init; } =
    [
        "bin",
        "obj",
        "node_modules",
        ".git",
        ".idea"
    ];

    public List<string> SourceFileExtensions { get; init; } = [".cs", ".config", ".json", ".xml"];
    public List<string> MarkdownFileExtensions { get; init; } = [".md"];
}
