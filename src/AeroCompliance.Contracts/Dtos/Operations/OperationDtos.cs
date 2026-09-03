using System.ComponentModel.DataAnnotations;

namespace AeroCompliance.Contracts.Dtos.Operations;

public sealed record ViolationSummaryDto(Guid Id, string ViolationType, string Status, decimal AiConfidenceScore,
    Guid? FlightId, string? FlightMissionName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ViolationHistoryDto(string Status, Guid? ChangedBy, string? Notes, DateTimeOffset ChangedAt);
public sealed record ViolationDetailsDto(Guid Id, string ViolationType, string Status, decimal AiConfidenceScore,
    decimal? MeasuredValue, decimal? RequiredValue, decimal? ExceedanceValue, decimal? ExceedanceAreaM2,
    bool IsZeroState, Guid DetectionId, string DetectionClass, decimal DetectionConfidence, Guid? FlightId,
    string? FlightMissionName, string? SourceImageFileName,
    Guid? ParcelId, string? ParcelNumber, Guid? RoadId, string? RoadCode, Guid? ZoneId, string? ZoneNumber,
    Guid? BlockId, string? BlockNumber, Guid? RuleId, string? RuleKey, Guid? EvidenceImageId,
    string? EvidenceStoragePath, IReadOnlyList<ViolationHistoryDto> StatusHistory,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed class ViolationDecisionRequest
{
    [Required, StringLength(2000)] public string Notes { get; init; } = string.Empty;
}

public sealed record StatusCountDto(string Status, int Count);
public sealed record DashboardSummaryDto(int TotalFlights, int Images, int PendingViolations,
    int ConfirmedViolations, int DismissedViolations, int? CompletedProcessingCount,
    IReadOnlyList<ViolationSummaryDto> RecentViolations, IReadOnlyList<StatusCountDto> CountsByStatus);

public sealed record AuditLogDto(long Id, Guid? ActorId, string? ActorName, Guid? MunicipalityId, string Action, string EntityType,
    Guid? EntityId, string? BeforeState, string? AfterState, string? IpAddress, DateTimeOffset CreatedAt);
