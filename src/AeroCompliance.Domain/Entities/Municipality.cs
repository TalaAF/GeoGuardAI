namespace AeroCompliance.Domain.Entities;

/// <summary>A municipality (tenant boundary). Maps to <c>municipalities</c>.</summary>
public class Municipality
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Region { get; set; }

    /// <summary>Date the data-sharing MoU was signed (<c>mou_signed_at</c>, SQL DATE).</summary>
    public DateOnly? MouSignedAt { get; set; }

    /// <summary>Defaults to false: dashboard-only access until API access is explicitly granted.</summary>
    public bool ApiAccessEnabled { get; set; }
    public bool IsPilot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<Road> Roads { get; set; } = new List<Road>();
    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
}
