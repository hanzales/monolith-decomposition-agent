using Migration.Intelligence.Contracts.Discovery;
using Migration.Intelligence.Core.Options;
using Migration.Intelligence.Core.Utilities;
using Migration.Intelligence.Scanner.Mappers;

namespace Migration.Intelligence.Scanner.Services;

public sealed class ProjectDiscoveryService
{
    private readonly CsProjParser _csProjParser;
    private readonly ProjectMapper _projectMapper;

    public ProjectDiscoveryService(CsProjParser csProjParser, ProjectMapper projectMapper)
    {
        _csProjParser = csProjParser;
        _projectMapper = projectMapper;
    }

    public List<ProjectContract> DiscoverProjects(string sourceRoot, ScannerOptions options)
    {
        return Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathUtility.IsExcludedPath(Path.GetRelativePath(sourceRoot, path), options.ExcludedDirectories))
            .Select(path =>
            {
                var targetFramework = _csProjParser.ReadTargetFramework(path);
                return _projectMapper.Map(sourceRoot, path, targetFramework);
            })
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
