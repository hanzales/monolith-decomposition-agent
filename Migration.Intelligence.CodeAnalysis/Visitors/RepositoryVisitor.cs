using Migration.Intelligence.CodeAnalysis.Heuristics;

namespace Migration.Intelligence.CodeAnalysis.Visitors;

public sealed class RepositoryVisitor
{
    private readonly RepositoryHeuristics _heuristics;

    public RepositoryVisitor(RepositoryHeuristics heuristics)
    {
        _heuristics = heuristics;
    }

    public bool IsRepositoryLike(string relativePath)
    {
        return _heuristics.IsRepositoryFile(relativePath);
    }
}
