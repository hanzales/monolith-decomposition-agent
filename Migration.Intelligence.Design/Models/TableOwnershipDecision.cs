namespace Migration.Intelligence.Design.Models;

public sealed class TableOwnershipDecision
{
    public required string TableName { get; init; }
    public required TableRole Role { get; init; }
    public string OwnerDomain { get; init; } = string.Empty;
    public required TableAccessType AccessType { get; init; }
    public bool IsShared { get; init; }
    public double Confidence { get; init; }
    public bool CanMoveIndependently { get; init; }
    public List<string> ReferencedByDomains { get; init; } = new();
    public string Notes { get; init; } = string.Empty;
}
