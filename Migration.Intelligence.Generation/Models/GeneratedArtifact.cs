namespace Migration.Intelligence.Generation.Models;

public sealed class GeneratedArtifact
{
    public required string FileName { get; init; }
    public required string RelativePath { get; init; }
    public required string ContentType { get; init; }
    public required string Content { get; init; }
}
