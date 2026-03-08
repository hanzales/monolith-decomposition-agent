using Migration.Intelligence.Contracts.Discovery;

namespace Migration.Intelligence.Scanner.Mappers;

public sealed class ProjectMapper
{
    public ProjectContract Map(string sourceRoot, string projectPath, string targetFramework)
    {
        return new ProjectContract
        {
            Name = Path.GetFileNameWithoutExtension(projectPath),
            RelativePath = Path.GetRelativePath(sourceRoot, projectPath),
            TargetFramework = targetFramework
        };
    }
}
