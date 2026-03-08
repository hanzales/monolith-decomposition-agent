using Migration.Intelligence.Design.Models;
using Migration.Intelligence.Generation.Abstractions;
using Migration.Intelligence.Generation.Models;
using Migration.Intelligence.Validation.Models;

namespace Migration.Intelligence.Generation.Services;

public sealed class DomainGenerationOrchestrator : IDomainGenerationOrchestrator
{
    private readonly IArtifactTemplateGenerator _artifactTemplateGenerator;
    private readonly IGenerationWriter _generationWriter;

    public DomainGenerationOrchestrator(
        IArtifactTemplateGenerator artifactTemplateGenerator,
        IGenerationWriter generationWriter)
    {
        _artifactTemplateGenerator = artifactTemplateGenerator;
        _generationWriter = generationWriter;
    }

    public async Task<GenerationWriteResult> GenerateAsync(
        DomainMigrationDesign design,
        string outputRoot,
        ValidationReport? validationReport = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(design);
        var package = _artifactTemplateGenerator.Generate(design, validationReport);
        return await _generationWriter.WriteAsync(package, outputRoot, cancellationToken);
    }
}
