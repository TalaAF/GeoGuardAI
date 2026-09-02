using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AeroCompliance.Contracts.Dtos.Flights;

public sealed record FlightSummaryDto(Guid Id, Guid MunicipalityId, Guid DroneOperatorId, string? OperatorName,
    string? MissionName, string Status, DateTimeOffset? ScheduledAt, DateTimeOffset CreatedAt);
public sealed record FlightDetailsDto(Guid Id, Guid MunicipalityId, Guid DroneOperatorId, string? OperatorName,
    string? MissionName, JsonElement? PlannedArea, string Status, DateTimeOffset? ScheduledAt,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, string? WeatherConditions, bool QualityFlag,
    int MissionCount, int ImageCount, int VideoCount, int OrthomosaicCount, DateTimeOffset CreatedAt);
public sealed class SaveFlightRequest
{
    public Guid? MunicipalityId { get; init; }
    public Guid? DroneOperatorId { get; init; }
    [StringLength(200)] public string? MissionName { get; init; }
    public JsonElement? PlannedArea { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? WeatherConditions { get; init; }
    public bool QualityFlag { get; init; } = true;
}
public sealed class FlightTransitionRequest
{
    [Required] public string TargetStatus { get; init; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; init; }
}
public sealed record FlightMissionDto(Guid Id, Guid FlightId, string? Purpose, decimal? AltitudeM, decimal? OverlapPercent, string? Notes);
public sealed class CreateFlightMissionRequest
{
    [StringLength(50)] public string? Purpose { get; init; }
    [Range(0.01, 10000)] public decimal? AltitudeM { get; init; }
    [Range(0, 100)] public decimal? OverlapPercent { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}
