namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class TableOwnershipContract
{
    public required string TableName { get; init; }
    public string OwnerDomain { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public bool IsShared { get; init; }
    public List<string> CandidateDomains { get; init; } = new();
    public List<string> ReadDomains { get; init; } = new();
    public List<string> WriteDomains { get; init; } = new();
}
