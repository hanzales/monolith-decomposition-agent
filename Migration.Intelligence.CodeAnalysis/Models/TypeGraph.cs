namespace Migration.Intelligence.CodeAnalysis.Models;

public sealed class TypeGraph
{
    private readonly Dictionary<string, TypeNode> _nodes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<TypeNode> Nodes => _nodes.Values;

    public TypeNode GetOrCreate(string typeName, string? relativePath = null)
    {
        if (_nodes.TryGetValue(typeName, out var existing))
        {
            return existing;
        }

        var node = new TypeNode
        {
            TypeName = typeName,
            RelativePath = relativePath ?? string.Empty
        };

        _nodes[typeName] = node;
        return node;
    }

    public void AddDependency(string sourceType, string targetType)
    {
        var source = GetOrCreate(sourceType);
        source.Dependencies.Add(targetType);
        GetOrCreate(targetType);
    }
}
