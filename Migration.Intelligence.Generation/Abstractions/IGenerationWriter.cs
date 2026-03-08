using Migration.Intelligence.Generation.Models;

namespace Migration.Intelligence.Generation.Abstractions;

/// <summary>
/// Writes generated artifacts to filesystem.
/// </summary>
public interface IGenerationWriter
{
    Task<GenerationWriteResult> WriteAsync(
        DomainGenerationPackage package,
        string outputRoot,
        CancellationToken cancellationToken = default);
}
