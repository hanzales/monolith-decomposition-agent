using Migration.Intelligence.Validation.Abstractions;

namespace Migration.Intelligence.Validation.Services;

/// <summary>
/// Composition entry point for validation services.
/// </summary>
public static class ValidationComposition
{
    public static IValidationOrchestrator CreateDefaultOrchestrator()
    {
        var intelligenceValidator = new MigrationIntelligenceValidator();
        var designValidator = new DomainMigrationDesignValidator();
        return new ValidationOrchestrator(intelligenceValidator, designValidator);
    }
}
