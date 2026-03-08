using System.Text;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Reporting.Services;

public sealed class TargetProjectWriter : ITargetProjectWriter
{
    public async Task WriteAsync(
        List<ServiceBlueprintContract> serviceBlueprints,
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.TargetPath);
        var servicesRoot = Path.Combine(options.TargetPath, "services");
        Directory.CreateDirectory(servicesRoot);

        var expectedFolders = serviceBlueprints
            .Select(service => ToKebabCase(service.ServiceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var existingDirectory in Directory.EnumerateDirectories(servicesRoot))
        {
            var folderName = Path.GetFileName(existingDirectory);
            if (expectedFolders.Contains(folderName))
            {
                continue;
            }

            Directory.Delete(existingDirectory, recursive: true);
        }

        foreach (var service in serviceBlueprints)
        {
            var serviceFolder = Path.Combine(servicesRoot, ToKebabCase(service.ServiceName));
            var srcFolder = Path.Combine(serviceFolder, "src");

            Directory.CreateDirectory(srcFolder);
            await File.WriteAllTextAsync(
                Path.Combine(serviceFolder, "README.md"),
                BuildServiceReadme(service),
                cancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(options.TargetPath, "SERVICE_MAP.md"),
            BuildServiceMap(serviceBlueprints),
            cancellationToken);
    }

    private static string BuildServiceReadme(ServiceBlueprintContract service)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {service.ServiceName}");
        builder.AppendLine();
        builder.AppendLine($"- Bounded Context: `{service.BoundedContext}`");
        builder.AppendLine($"- Confidence Score: `{service.ConfidenceScore}`");
        builder.AppendLine($"- Description: {service.Description}");
        builder.AppendLine();
        builder.AppendLine("## Source Hints");

        if (service.SourceHints.Count == 0)
        {
            builder.AppendLine("- No source hints available.");
        }
        else
        {
            foreach (var hint in service.SourceHints)
            {
                builder.AppendLine($"- {hint}");
            }
        }

        return builder.ToString();
    }

    private static string BuildServiceMap(IEnumerable<ServiceBlueprintContract> serviceBlueprints)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Service Map");
        builder.AppendLine();

        var ordered = serviceBlueprints
            .OrderByDescending(x => x.ConfidenceScore)
            .ThenBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count == 0)
        {
            builder.AppendLine("- No service candidate inferred.");
            return builder.ToString();
        }

        builder.AppendLine("| Service | Context | Folder | Confidence | Signals |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: |");

        foreach (var service in ordered)
        {
            builder.AppendLine($"| `{service.ServiceName}` | `{service.BoundedContext}` | `{ToKebabCase(service.ServiceName)}` | `{service.ConfidenceScore}` | `{service.SourceHints.Count}` |");
        }

        return builder.ToString();
    }

    private static string ToKebabCase(string value)
    {
        var chars = new List<char>(value.Length * 2);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }
}
