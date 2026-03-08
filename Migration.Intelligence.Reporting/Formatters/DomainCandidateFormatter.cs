using System.Text;
using Migration.Intelligence.Contracts.Orchestration;

namespace Migration.Intelligence.Reporting.Formatters;

public sealed class DomainCandidateFormatter
{
    public string FormatTable(IEnumerable<ServiceBlueprintContract> services)
    {
        var ordered = services
            .OrderByDescending(service => service.ConfidenceScore)
            .ThenBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("| Service | Context | Confidence | Signals |");
        builder.AppendLine("| --- | --- | ---: | ---: |");

        foreach (var service in ordered)
        {
            builder.AppendLine($"| `{service.ServiceName}` | `{service.BoundedContext}` | `{service.ConfidenceScore}` | `{service.SourceHints.Count}` |");
        }

        return builder.ToString().TrimEnd();
    }

    public string FormatDetails(ServiceBlueprintContract service)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"### {service.ServiceName}");
        builder.AppendLine($"- Bounded Context: `{service.BoundedContext}`");
        builder.AppendLine($"- Confidence Score: `{service.ConfidenceScore}`");
        builder.AppendLine($"- Description: {service.Description}");

        if (service.SourceHints.Count == 0)
        {
            builder.AppendLine("- Source Hints: none");
            return builder.ToString().TrimEnd();
        }

        builder.AppendLine("- Source Hints:");
        foreach (var hint in service.SourceHints)
        {
            builder.AppendLine($"  - `{hint}`");
        }

        return builder.ToString().TrimEnd();
    }
}
