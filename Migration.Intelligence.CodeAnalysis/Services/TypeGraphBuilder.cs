using Migration.Intelligence.CodeAnalysis.Models;
using Migration.Intelligence.Contracts.Analysis;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class TypeGraphBuilder
{
    public TypeGraph Build(IEnumerable<DependencyContract> dependencies)
    {
        var graph = new TypeGraph();

        foreach (var dependency in dependencies)
        {
            graph.AddDependency(dependency.SourceType, dependency.TargetType);
        }

        return graph;
    }
}
