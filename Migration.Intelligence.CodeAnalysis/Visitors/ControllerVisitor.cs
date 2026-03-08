using Migration.Intelligence.CodeAnalysis.Heuristics;

namespace Migration.Intelligence.CodeAnalysis.Visitors;

public sealed class ControllerVisitor
{
    private readonly ControllerHeuristics _heuristics;

    public ControllerVisitor(ControllerHeuristics heuristics)
    {
        _heuristics = heuristics;
    }

    public bool TryExtractControllerName(string relativePath, out string controllerName)
    {
        controllerName = string.Empty;
        if (!_heuristics.IsControllerFile(relativePath))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        if (!fileName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        controllerName = fileName;
        return true;
    }
}
