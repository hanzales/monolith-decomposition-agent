namespace Migration.Intelligence.Generation.Models;

public sealed class DomainGenerationPackage
{
    public required string Domain { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public List<GeneratedArtifact> Artifacts { get; init; } = new();
    public List<BacklogItem> BacklogItems { get; init; } = new();
}
