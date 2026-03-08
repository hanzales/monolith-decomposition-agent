namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class RoslynWorkspaceFactory
{
    public bool IsRoslynWorkspaceAvailable => false;

    public IReadOnlyCollection<string> CreateWorkspaceSnapshot(string sourceRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string GetFallbackReason()
    {
        return "Roslyn workspace integration is not enabled in this build. Falling back to file-system analysis.";
    }
}
