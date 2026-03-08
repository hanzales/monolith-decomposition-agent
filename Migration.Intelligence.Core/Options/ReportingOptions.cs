namespace Migration.Intelligence.Core.Options;

public class ReportingOptions
{
    public bool IncludeProjectInventory { get; init; } = true;
    public bool IncludeServiceDetails { get; init; } = true;
    public int MaxServicesInReport { get; init; } = 25;
}
