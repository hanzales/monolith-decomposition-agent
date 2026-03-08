namespace Migration.Intelligence.Core.Utilities;

public static class PathUtility
{
    public static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    public static bool IsUnderDirectory(string rootPath, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = Path.GetFullPath(fullPath);

        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExcludedPath(string relativePath, IEnumerable<string> excludedDirectories)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            excludedDirectories.Any(excluded => excluded.Equals(segment, StringComparison.OrdinalIgnoreCase)));
    }

    public static string[] GetSegments(string relativePath)
    {
        return NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
    }
}
