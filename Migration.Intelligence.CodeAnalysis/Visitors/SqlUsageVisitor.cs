using System.Text.RegularExpressions;

namespace Migration.Intelligence.CodeAnalysis.Visitors;

public sealed class SqlUsageVisitor
{
    private static readonly Regex SqlTableRegex =
        new("\\b(?:from|join|into|update)\\s+(?<table>[A-Za-z0-9_\\[\\].]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyCollection<string> ExtractTableNames(string content)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SqlTableRegex.Matches(content))
        {
            tables.Add(match.Groups["table"].Value.Trim('[', ']'));
        }

        return tables.ToList();
    }

    public bool LooksLikeSql(string content)
    {
        return content.Contains("select ", StringComparison.OrdinalIgnoreCase)
               || content.Contains(" from ", StringComparison.OrdinalIgnoreCase)
               || content.Contains(" join ", StringComparison.OrdinalIgnoreCase)
               || content.Contains(" update ", StringComparison.OrdinalIgnoreCase)
               || content.Contains(" insert ", StringComparison.OrdinalIgnoreCase)
               || content.Contains(" delete ", StringComparison.OrdinalIgnoreCase);
    }
}
