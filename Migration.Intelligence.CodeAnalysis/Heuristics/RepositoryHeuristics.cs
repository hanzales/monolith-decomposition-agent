namespace Migration.Intelligence.CodeAnalysis.Heuristics;

public sealed class RepositoryHeuristics
{
    public bool IsRepositoryFile(string relativePath)
    {
        return relativePath.Contains("Repository", StringComparison.OrdinalIgnoreCase)
               && relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }
}
