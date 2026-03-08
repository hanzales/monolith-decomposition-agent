namespace Migration.Intelligence.CodeAnalysis.Models;

public sealed class TypeNode
{
    public required string TypeName { get; init; }
    public string Namespace { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public HashSet<string> Dependencies { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
