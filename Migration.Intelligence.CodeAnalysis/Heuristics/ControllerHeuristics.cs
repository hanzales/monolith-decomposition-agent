namespace Migration.Intelligence.CodeAnalysis.Heuristics;

public sealed class ControllerHeuristics
{
    public bool IsControllerFile(string relativePath)
    {
        return relativePath.Contains("Controller", StringComparison.OrdinalIgnoreCase)
               && relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public string ToRoutePrefix(string controllerName)
    {
        var normalized = controllerName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
            ? controllerName[..^"Controller".Length]
            : controllerName;

        return "/" + normalized.ToLowerInvariant();
    }
}
