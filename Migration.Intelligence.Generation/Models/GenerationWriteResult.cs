namespace Migration.Intelligence.Generation.Models;

public sealed class GenerationWriteResult
{
    public required string Domain { get; init; }
    public required string OutputDirectory { get; init; }
    public List<string> WrittenFiles { get; init; } = new();
}
