namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public enum HangfireScheduleSourceType
{
    Unknown = 0,
    Code = 1,
    WebConfig = 2,
    AppConfig = 3,
    JsonConfig = 4,
    Constant = 5
}
