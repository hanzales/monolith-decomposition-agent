using System.Xml.Linq;

namespace Migration.Intelligence.Scanner.Services;

public sealed class CsProjParser
{
    public string ReadTargetFramework(string csprojPath)
    {
        try
        {
            var document = XDocument.Load(csprojPath);
            var targetFramework = document
                .Descendants()
                .FirstOrDefault(node => node.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                ?.Value;

            return targetFramework?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
