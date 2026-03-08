using Migration.Intelligence.Contracts.Discovery;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.Core.Utilities;

namespace Migration.Intelligence.Scanner.Services;

public sealed class FileInventoryService
{
    public List<FileContract> DiscoverFiles(
        string sourceRoot,
        IEnumerable<string> extensions,
        ScannerOptions options)
    {
        var extensionSet = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => extensionSet.Contains(Path.GetExtension(path)))
            .Where(path => !PathUtility.IsExcludedPath(Path.GetRelativePath(sourceRoot, path), options.ExcludedDirectories))
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new FileContract
                {
                    RelativePath = Path.GetRelativePath(sourceRoot, path),
                    Extension = info.Extension,
                    SizeBytes = info.Length
                };
            })
            .ToList();

        return files;
    }
}
