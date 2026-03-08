using Migration.Intelligence.Generation.Abstractions;
using Migration.Intelligence.Generation.Models;

namespace Migration.Intelligence.Generation.Services;

public sealed class FileSystemGenerationWriter : IGenerationWriter
{
    public async Task<GenerationWriteResult> WriteAsync(
        DomainGenerationPackage package,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new ArgumentException("Output root must not be empty.", nameof(outputRoot));
        }

        var domainDir = Path.Combine(outputRoot, "generated", ToSlug(package.Domain));
        Directory.CreateDirectory(domainDir);

        var writtenFiles = new List<string>();
        foreach (var artifact in package.Artifacts)
        {
            var relative = artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(domainDir, relative);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await File.WriteAllTextAsync(fullPath, artifact.Content, cancellationToken);
            writtenFiles.Add(fullPath);
        }

        return new GenerationWriteResult
        {
            Domain = package.Domain,
            OutputDirectory = domainDir,
            WrittenFiles = writtenFiles
        };
    }

    private static string ToSlug(string value)
    {
        var chars = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var candidate = new string(chars);
        while (candidate.Contains("--", StringComparison.Ordinal))
        {
            candidate = candidate.Replace("--", "-", StringComparison.Ordinal);
        }

        return candidate.Trim('-');
    }
}
