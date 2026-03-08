namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public enum HangfireJobCategory
{
    ConsumerJob = 0,
    ScheduledJob = 1,
    TriggeredJob = 2,
    ProducerJob = 3
}
