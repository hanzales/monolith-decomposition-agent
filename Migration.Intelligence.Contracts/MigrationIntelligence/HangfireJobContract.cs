namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class HangfireJobContract
{
    public required string JobName { get; init; }
    public required string ClassName { get; init; }
    public required string MethodName { get; init; }
    public string RelativePath { get; init; } = string.Empty;
    public string RegistrationSource { get; init; } = string.Empty;
    public required string DomainOwner { get; init; }
    public required HangfireJobCategory Category { get; init; }
    public required HangfireJobTriggerType TriggerType { get; init; }
    public string QueueName { get; init; } = string.Empty;
    public string QueueOrTopic { get; init; } = string.Empty;
    public string ConsumedMessageType { get; init; } = string.Empty;
    public string ProducedMessageType { get; init; } = string.Empty;
    public string ProducerJob { get; set; } = string.Empty;
    public string RelatedMessageOrEvent { get; init; } = string.Empty;
    public string ScheduleSource { get; init; } = string.Empty;
    public string ScheduleExpression { get; init; } = string.Empty;
    public string RawScheduleKey { get; init; } = string.Empty;
    public string ResolvedScheduleExpression { get; init; } = string.Empty;
    public HangfireScheduleSourceType ScheduleSourceType { get; init; }
    public HangfireScheduleResolutionStatus ScheduleResolutionStatus { get; init; }
    public string ScheduleResolutionNote { get; init; } = string.Empty;
    public bool IsInfrastructureOnly { get; init; }
    public bool MustRemainInMonolithInitially { get; init; }
    public bool IsLegacyHosted { get; init; }
    public string LegacyHostingReason { get; init; } = string.Empty;
    public bool NeedsRefactorBeforeMigration { get; init; }
    public bool CanMoveWithService { get; init; }
    public double OwnershipConfidence { get; init; }
    public List<string> TopCandidateDomains { get; init; } = new();
    public string UnmappedReason { get; init; } = string.Empty;
    public List<string> DependentDomains { get; init; } = new();
    public List<string> Evidence { get; init; } = new();
}
