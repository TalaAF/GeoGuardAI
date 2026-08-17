namespace AeroCompliance.Domain.Entities;

/// <summary>
/// A fast police alert for sensitive roads, separate from the normal violation workflow.
/// Maps to <c>police_alerts</c>.
/// </summary>
public class PoliceAlert
{
    public Guid Id { get; set; }

    public Guid DetectionId { get; set; }
    public Detection Detection { get; set; } = null!;

    public Guid RoadId { get; set; }
    public Road Road { get; set; } = null!;

    /// <summary><c>road_blocking_vehicle</c>, <c>obstruction</c>, ...</summary>
    public string AlertType { get; set; } = null!;

    /// <summary><c>sms</c>, <c>email</c>, <c>api</c>.</summary>
    public string Channel { get; set; } = null!;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
