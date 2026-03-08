namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class ServiceDossierContract
{
    public required string CandidateName { get; init; }
    public required string DetectionRationale { get; init; }
    public List<string> Subdomains { get; init; } = new();
    public List<string> RelatedControllers { get; init; } = new();
    public List<EndpointMappingContract> RelatedEndpoints { get; init; } = new();
    public List<string> RelatedServices { get; init; } = new();
    public List<string> RelatedRepositories { get; init; } = new();
    public List<string> RelatedEntities { get; init; } = new();
    public List<string> RelatedTables { get; init; } = new();
    public List<string> ConsumerJobs { get; init; } = new();
    public List<string> ScheduledJobs { get; init; } = new();
    public List<string> TriggeredJobs { get; init; } = new();
    public List<string> ProducerJobs { get; init; } = new();
    public List<string> NormalJobs { get; init; } = new();
    public List<string> JobSchedulingDependencies { get; init; } = new();
    public int BackgroundJobCount { get; init; }
    public int ConsumerJobCount { get; init; }
    public int ScheduledJobCount { get; init; }
    public int TriggeredJobCount { get; init; }
    public int ProducerJobCount { get; init; }
    public int NormalJobCount { get; init; }
    public int LegacyHostedJobCount { get; init; }
    public List<string> ExternalDependencies { get; init; } = new();
    public List<string> SharedDependencies { get; init; } = new();
    public List<string> LegacyRisks { get; init; } = new();
    public int CohesionScore { get; init; }
    public int CouplingScore { get; init; }
    public int MigrationReadinessScore { get; init; }
    public required string MigrationReadinessLevel { get; init; }
    public required string MigrationReadinessExplanation { get; init; }
    public required string RecommendedFirstExtractionStrategy { get; init; }
    public bool ReadOnlyFirstExtractionPossible { get; init; }
    public bool StagedMigrationRecommended { get; init; }
    public int UnknownExecutionChainCount { get; init; }
    public List<string> MajorBlockers { get; init; } = new();
}
