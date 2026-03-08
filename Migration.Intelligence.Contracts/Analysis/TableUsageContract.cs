namespace Migration.Intelligence.Contracts.Analysis;

public sealed class TableUsageContract
{
    public required string TableName { get; init; }
    public required string AccessType { get; init; }
    public required string RelativePath { get; init; }
}
