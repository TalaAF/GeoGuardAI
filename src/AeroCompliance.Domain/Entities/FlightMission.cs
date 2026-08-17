namespace AeroCompliance.Domain.Entities;

/// <summary>A sub-mission of a flight (e.g. a road-boundary scan). Maps to <c>flight_missions</c>.</summary>
public class FlightMission
{
    public Guid Id { get; set; }

    public Guid FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    /// <summary>e.g. <c>road_boundary_scan</c>, <c>building_scan</c>.</summary>
    public string? Purpose { get; set; }
    public decimal? AltitudeM { get; set; }

    /// <summary>Image overlap percentage used for orthomosaic generation.</summary>
    public decimal? OverlapPercent { get; set; }
    public string? Notes { get; set; }
}
