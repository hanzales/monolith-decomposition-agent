using Migration.Intelligence.Generation.Abstractions;

namespace Migration.Intelligence.Generation.Services;

/// <summary>
/// Composition entry point for generation services.
/// </summary>
public static class GenerationComposition
{
    public static IDomainGenerationOrchestrator CreateDefaultOrchestrator()
    {
        var backlogGenerator = new BacklogGenerator();
        var templateGenerator = new ArtifactTemplateGenerator(backlogGenerator);
        var writer = new FileSystemGenerationWriter();
        return new DomainGenerationOrchestrator(templateGenerator, writer);
    }
}
