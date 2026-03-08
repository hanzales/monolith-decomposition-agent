using Migration.Intelligence.Contracts.Analysis;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.CodeAnalysis.Visitors;

namespace Migration.Intelligence.CodeAnalysis.Services;

public sealed class DataAccessAnalyzer
{
    private readonly SqlUsageVisitor _sqlUsageVisitor;

    public DataAccessAnalyzer(SqlUsageVisitor sqlUsageVisitor)
    {
        _sqlUsageVisitor = sqlUsageVisitor;
    }

    public async Task<List<TableUsageContract>> AnalyzeTableUsageAsync(
        RepositoryInventoryContract inventory,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.CodeAnalysis.EnableSqlUsageDetection)
        {
            return new List<TableUsageContract>();
        }

        var tableUsages = new List<TableUsageContract>();

        foreach (var sourceFile in inventory.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!sourceFile.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!options.CodeAnalysis.IncludeGeneratedFiles
                && (sourceFile.RelativePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
                    || sourceFile.RelativePath.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (sourceFile.SizeBytes > options.CodeAnalysis.MaxFileReadBytes)
            {
                continue;
            }

            var fullPath = Path.Combine(inventory.SourceRootPath, sourceFile.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            if (!_sqlUsageVisitor.LooksLikeSql(content))
            {
                continue;
            }

            var accessType = DetectAccessType(content);
            foreach (var tableName in _sqlUsageVisitor.ExtractTableNames(content))
            {
                tableUsages.Add(new TableUsageContract
                {
                    TableName = tableName,
                    AccessType = accessType,
                    RelativePath = sourceFile.RelativePath
                });
            }
        }

        return tableUsages;
    }

    private static string DetectAccessType(string content)
    {
        if (content.Contains("insert ", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" update ", StringComparison.OrdinalIgnoreCase)
            || content.Contains(" delete ", StringComparison.OrdinalIgnoreCase))
        {
            return "write";
        }

        return "read";
    }
}
