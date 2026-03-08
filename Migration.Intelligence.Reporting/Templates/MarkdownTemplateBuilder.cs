using System.Text;

namespace Migration.Intelligence.Reporting.Templates;

public sealed class MarkdownTemplateBuilder
{
    public string BuildDocument(string title, IEnumerable<string> sections)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                continue;
            }

            builder.AppendLine(section.TrimEnd());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
