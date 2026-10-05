namespace AeroCompliance.Domain.Entities;

public class OutboxMessage
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = null!;
    public string RoutingKey { get; set; } = null!;
    public int SchemaVersion { get; set; }
    public string Payload { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}

public class InboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string Consumer { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string? PayloadHash { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
