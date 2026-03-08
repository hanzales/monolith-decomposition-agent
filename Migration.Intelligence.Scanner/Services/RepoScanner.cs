using Migration.Intelligence.Contracts.Discovery;
using Migration.Intelligence.Contracts.Orchestration;
using Migration.Intelligence.Core.Abstractions;
using Migration.Intelligence.Core.Options;

namespace Migration.Intelligence.Scanner.Services;

public sealed class RepoScanner : IRepoScanner
{
    private readonly ProjectDiscoveryService _projectDiscoveryService;
    private readonly SolutionDiscoveryService _solutionDiscoveryService;
    private readonly FileInventoryService _fileInventoryService;

    public RepoScanner()
    {
        var parser = new CsProjParser();
        _projectDiscoveryService = new ProjectDiscoveryService(parser, new Mappers.ProjectMapper());
        _solutionDiscoveryService = new SolutionDiscoveryService();
        _fileInventoryService = new FileInventoryService();
    }

    public Task<RepositoryInventoryContract> ScanAsync(
        AnalysisOptions options,
        CancellationToken cancellationToken = default)
    {
        var sourceRoot = Path.GetFullPath(options.SourcePath);
        var scannerOptions = options.Scanner;

        var projects = _projectDiscoveryService.DiscoverProjects(sourceRoot, scannerOptions);
        var solutionContracts = _solutionDiscoveryService.DiscoverSolutions(sourceRoot, projects, scannerOptions);
        var sourceFiles = _fileInventoryService.DiscoverFiles(sourceRoot, scannerOptions.SourceFileExtensions, scannerOptions);
        var markdownFiles = _fileInventoryService.DiscoverFiles(sourceRoot, scannerOptions.MarkdownFileExtensions, scannerOptions);

        var inventory = new RepositoryInventoryContract
        {
            SourceRootPath = sourceRoot,
            Solutions = solutionContracts,
            Projects = projects,
            SourceFiles = sourceFiles,
            MarkdownFiles = markdownFiles
        };

        return Task.FromResult(inventory);
    }
}
