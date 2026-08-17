namespace AeroCompliance.Domain.Entities;

/// <summary>A raw drone video. Maps to <c>videos</c>.</summary>
public class Video
{
    public Guid Id { get; set; }

    public Guid FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public string StoragePath { get; set; } = null!;
    public string Sha256Hash { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
