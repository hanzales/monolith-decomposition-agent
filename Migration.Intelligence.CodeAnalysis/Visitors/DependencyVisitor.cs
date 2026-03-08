using System.Text.RegularExpressions;

namespace Migration.Intelligence.CodeAnalysis.Visitors;

public sealed class DependencyVisitor
{
    private static readonly Regex UsingRegex =
        new("^\\s*using\\s+(?<dependency>[A-Za-z0-9_.]+)\\s*;", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex NewRegex =
        new("new\\s+(?<dependency>[A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled);

    public IReadOnlyCollection<string> ExtractDependencies(string content)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in UsingRegex.Matches(content))
        {
            dependencies.Add(match.Groups["dependency"].Value);
        }

        foreach (Match match in NewRegex.Matches(content))
        {
            dependencies.Add(match.Groups["dependency"].Value);
        }

        return dependencies.ToList();
    }
}
