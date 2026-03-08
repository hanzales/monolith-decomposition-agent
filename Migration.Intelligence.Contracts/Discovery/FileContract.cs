namespace Migration.Intelligence.Contracts.Discovery;

public class FileContract
{
    public required string RelativePath { get; init; }
    public required string Extension { get; init; }
    public long SizeBytes { get; init; }
}
