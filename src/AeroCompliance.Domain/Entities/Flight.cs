using AeroCompliance.Domain.Enums;
using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>A drone flight. Maps to <c>flights</c>.</summary>
public class Flight
{
    public Guid Id { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;

    public Guid DroneOperatorId { get; set; }
    public User DroneOperator { get; set; } = null!;

    public string? MissionName { get; set; }

    /// <summary>Planned coverage area, MultiPolygon SRID 4326 (nullable).</summary>
    public MultiPolygon? PlannedArea { get; set; }

    public FlightStatus Status { get; set; } = FlightStatus.Planned;

    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Raw weather conditions captured at flight time, stored as JSONB.</summary>
    public string? WeatherConditions { get; set; }

    /// <summary>False means the flight needs to be re-flown for quality.</summary>
    public bool QualityFlag { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<FlightMission> Missions { get; set; } = new List<FlightMission>();
    public ICollection<Image> Images { get; set; } = new List<Image>();
    public ICollection<Video> Videos { get; set; } = new List<Video>();
    public ICollection<Orthomosaic> Orthomosaics { get; set; } = new List<Orthomosaic>();
}
