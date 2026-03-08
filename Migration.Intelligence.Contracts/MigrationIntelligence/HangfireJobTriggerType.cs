namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public enum HangfireJobTriggerType
{
    Unknown = 0,
    Recurring = 1,
    Scheduled = 2,
    Delayed = 3,
    FireAndForget = 4,
    QueueOrEvent = 5
}
