using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AeroCompliance.Contracts.Dtos.Gis;

public sealed record ZoneSummaryDto(Guid Id, Guid MunicipalityId, string ZoneNumber, string? Name, string? ZoneType, JsonElement Geometry);
public sealed record ZoneDetailsDto(Guid Id, Guid MunicipalityId, string ZoneNumber, string? Name, string? ZoneType, JsonElement Geometry, string? SourceDocument, DateTimeOffset CreatedAt);
public sealed class SaveZoneRequest
{
    public Guid? MunicipalityId { get; init; }
    [Required, StringLength(50)] public string ZoneNumber { get; init; } = string.Empty;
    [StringLength(200)] public string? Name { get; init; }
    [StringLength(50)] public string? ZoneType { get; init; }
    [Required] public JsonElement Geometry { get; init; }
    [StringLength(255)] public string? SourceDocument { get; init; }
}

public sealed record BlockDto(Guid Id, Guid ZoneId, string? ZoneNumber, string BlockNumber, JsonElement Geometry, DateTimeOffset CreatedAt);
public sealed class SaveBlockRequest
{
    public Guid ZoneId { get; init; }
    [Required, StringLength(50)] public string BlockNumber { get; init; } = string.Empty;
    [Required] public JsonElement Geometry { get; init; }
}

public sealed record ParcelSummaryDto(Guid Id, Guid BlockId, string? BlockNumber, string? ZoneNumber, string ParcelNumber, string? ZoneType, decimal? AreaM2, JsonElement Geometry);
public sealed record ParcelDetailsDto(Guid Id, Guid BlockId, string? BlockNumber, string? ZoneNumber, string ParcelNumber, string? OwnerName, string? ZoneType, decimal? AreaM2, JsonElement Geometry, string Source, DateTimeOffset CreatedAt);
public sealed class SaveParcelRequest
{
    public Guid BlockId { get; init; }
    [Required, StringLength(50)] public string ParcelNumber { get; init; } = string.Empty;
    [StringLength(200)] public string? OwnerName { get; init; }
    [StringLength(50)] public string? ZoneType { get; init; }
    [Range(0, double.MaxValue)] public decimal? AreaM2 { get; init; }
    [Required] public JsonElement Geometry { get; init; }
    [StringLength(30)] public string Source { get; init; } = "municipality_gis";
}

public sealed record RoadDto(Guid Id, Guid MunicipalityId, string RoadCode, string? Name, string? RoadType,
    decimal? WidthM, int? SpeedLimitKmh, bool IsSensitive, JsonElement Geometry, DateTimeOffset CreatedAt);
public sealed class SaveRoadRequest
{
    public Guid? MunicipalityId { get; init; }
    [Required, StringLength(50)] public string RoadCode { get; init; } = string.Empty;
    [StringLength(200)] public string? Name { get; init; }
    [StringLength(50)] public string? RoadType { get; init; }
    [Range(0, double.MaxValue)] public decimal? WidthM { get; init; }
    [Range(0, 300)] public int? SpeedLimitKmh { get; init; }
    public bool IsSensitive { get; init; }
    [Required] public JsonElement Geometry { get; init; }
}

public sealed record ZoningRuleDto(Guid Id, Guid MunicipalityId, Guid? ZoneId, string? ZoneNumber, string? ZoneType, string RuleKey, decimal RuleValue,
    string Unit, string? SourceDocument, bool ExtractedByAi, bool IsValidated, Guid? ValidatedBy, string? ValidatorName,
    DateTimeOffset? ValidatedAt, bool IsActive, DateTimeOffset CreatedAt,
    // Phase 7: resolved server-side from the rule catalogue so the UI never interprets a rule key.
    string? RuleLabel = null, string? RuleDescription = null, string? EnforcementState = null,
    string? RequirementDisplay = null, bool IsSupported = false);
public sealed class SaveZoningRuleRequest
{
    public Guid? MunicipalityId { get; init; }
    public Guid? ZoneId { get; init; }
    [StringLength(50)] public string? ZoneType { get; init; }
    [Required, StringLength(100)] public string RuleKey { get; init; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal RuleValue { get; init; }
    [Required, StringLength(20)] public string Unit { get; init; } = string.Empty;
    [StringLength(255)] public string? SourceDocument { get; init; }
    public bool ExtractedByAi { get; init; }
    public bool IsActive { get; init; }
}
