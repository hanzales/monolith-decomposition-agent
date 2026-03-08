namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class BackgroundJobValidationContract
{
    public int DiscoveredJobCount { get; init; }
    public int TypedJobCount { get; init; }
    public int ScheduledJobsWithResolvedSchedule { get; init; }
    public int ScheduledJobsWithUnresolvedSchedule { get; init; }
    public int DomainMappedJobCount { get; init; }
    public int UnmappedJobCount { get; init; }
    public int ConsumerJobsWithMessageOrTopic { get; init; }
    public int ProducerConsumerRelationshipCount { get; init; }
    public List<string> Warnings { get; init; } = new();
}
