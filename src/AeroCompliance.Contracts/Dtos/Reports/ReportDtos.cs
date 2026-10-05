using System.Text.Json;

namespace AeroCompliance.Contracts.Dtos.Reports;

/// <summary>
/// A generated violation report, as shown in the Reports list. Deliberately carries no
/// storage path, bucket or object key — the PDF is only reachable through
/// <c>GET /api/reports/{id}/download</c>.
/// </summary>
public sealed record ReportSummaryDto(
    Guid Id,
    string Reference,
    string ViolationReference,
    string ViolationType,
    string MunicipalityName,
    string ViolationStatus,
    string? RoadCode,
    string? RoadName,
    string? ParcelNumber,
    DateTimeOffset GeneratedAt,
    string GeneratedBy,
    bool SignedOff,
    string? SignedOffByName,
    DateTimeOffset? SignedOffAt,
    bool DownloadAvailable);

public sealed record ReportHistoryDto(string Status, string StatusLabel, string? ActorName, string? Notes, DateTimeOffset ChangedAt);

/// <summary>
/// Full report content for the details screen. <see cref="Geometry"/> is the same safe GeoJSON
/// DTO shape the rest of the API uses and is intended for map rendering only — the frontend
/// must never print it.
/// </summary>
public sealed record ReportDetailsDto(
    Guid Id,
    string Reference,
    string MunicipalityName,
    Guid ViolationId,
    string ViolationReference,
    string ViolationType,
    string ViolationStatus,
    string ViolationStatusLabel,
    string DetectionClass,
    decimal DetectionConfidence,
    string? FlightMissionName,
    string? SourceImageFileName,
    string? RoadCode,
    string? RoadName,
    string? ParcelNumber,
    string? ZoneNumber,
    string? BlockNumber,
    string? EvaluationType,
    decimal? MeasuredValue,
    decimal? RequiredValue,
    decimal? ExceedanceValue,
    decimal? ExceedanceAreaM2,
    string? Unit,
    string? ReviewNotes,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    IReadOnlyList<ReportHistoryDto> StatusHistory,
    JsonElement? Geometry,
    DateTimeOffset GeneratedAt,
    string GeneratedBy,
    bool SignedOff,
    string? SignedOffByName,
    DateTimeOffset? SignedOffAt,
    bool DownloadAvailable);
