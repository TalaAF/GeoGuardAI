namespace AeroCompliance.Domain.Enums;

/// <summary>
/// Lifecycle of a drone <c>flight</c>. Stored as the snake_case string value in the
/// <c>flights.status</c> column (see the EF configuration for the value converter).
/// </summary>
public enum FlightStatus
{
    Planned,
    InProgress,
    Completed,
    Aborted,
    FlaggedLowQuality
}
