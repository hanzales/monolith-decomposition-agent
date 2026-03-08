namespace Migration.Intelligence.Contracts.MigrationIntelligence;

public sealed class ProducerConsumerRelationshipContract
{
    public required string ProducerJob { get; init; }
    public required string ConsumerJob { get; init; }
    public required string RelationshipType { get; init; }
    public required string DomainOwner { get; init; }
    public double Confidence { get; init; }
}
