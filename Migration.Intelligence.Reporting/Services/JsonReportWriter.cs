using System.Text.Json;
using Migration.Intelligence.Contracts.Orchestration;

namespace Migration.Intelligence.Reporting.Services;

public sealed class JsonReportWriter
{
    public async Task<string> WriteAsync(
        MigrationExecutionContract execution,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "migration-report.json");

        var json = JsonSerializer.Serialize(execution, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }
}
