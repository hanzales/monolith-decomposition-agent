using Migration.Intelligence.Contracts.Analysis;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.CodeAnalysis.Visitors;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class DependencyAnalyzer
{
    private readonly DependencyVisitor _dependencyVisitor;

    public DependencyAnalyzer(DependencyVisitor dependencyVisitor)
    {
        _dependencyVisitor = dependencyVisitor;
    }

    public async Task<List<DependencyContract>> AnalyzeDependenciesAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var dependencies = new List<DependencyContract>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            var sourceType = Path.GetFileNameWithoutExtension(sourceFile.RelativePath);

            foreach (var dependencyName in _dependencyVisitor.ExtractDependencies(content))
            {
                dependencies.Add(new DependencyContract
                {
                    SourceType = sourceType,
                    TargetType = dependencyName,
                    IsExternalDependency = IsExternalDependency(dependencyName)
                });
            }
        }

        return dependencies;
    }

    private static bool IsExternalDependency(string dependency)
    {
        return dependency.StartsWith("System.", StringComparison.Ordinal)
               || dependency.StartsWith("Microsoft.", StringComparison.Ordinal)
               || dependency.StartsWith("Newtonsoft.", StringComparison.Ordinal)
               || dependency.StartsWith("Serilog", StringComparison.Ordinal)
               || dependency.StartsWith("NUnit", StringComparison.Ordinal)
               || dependency.StartsWith("xUnit", StringComparison.OrdinalIgnoreCase);
    }
}
