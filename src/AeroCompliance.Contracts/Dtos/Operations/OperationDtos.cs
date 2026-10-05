using System.ComponentModel.DataAnnotations;

namespace AeroCompliance.Contracts.Dtos.Operations;

public sealed record ViolationSummaryDto(Guid Id, string ViolationType, string Status, decimal AiConfidenceScore,
    Guid? FlightId, string? FlightMissionName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    // Phase 8 (additive, defaults describe the pre-existing exact case): a candidate raised from an
    // estimated or synthetic geometry must never read as an authoritative municipal violation.
    string? ResultSource = null, bool IsApproximate = false, bool IsExperimental = false,
    bool RequiresManualReview = false);
public sealed record ViolationHistoryDto(string Status, Guid? ChangedBy, string? Notes, DateTimeOffset ChangedAt);
public sealed record ViolationDetailsDto(Guid Id, string ViolationType, string Status, decimal AiConfidenceScore,
    decimal? MeasuredValue, decimal? RequiredValue, decimal? ExceedanceValue, decimal? ExceedanceAreaM2,
    bool IsZeroState, Guid DetectionId, string DetectionClass, decimal DetectionConfidence, Guid? FlightId,
    string? FlightMissionName, string? SourceImageFileName,
    Guid? ParcelId, string? ParcelNumber, Guid? RoadId, string? RoadCode, Guid? ZoneId, string? ZoneNumber,
    Guid? BlockId, string? BlockNumber, Guid? RuleId, string? RuleKey, Guid? EvidenceImageId,
    string? EvidenceStoragePath, IReadOnlyList<ViolationHistoryDto> StatusHistory,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string? ResultSource = null, bool IsApproximate = false, bool IsExperimental = false,
    bool RequiresManualReview = false, string? GeoreferencingConfidence = null,
    string? SourceGeoreferencingMethod = null, int? DetectionClassId = null);
public sealed class ViolationDecisionRequest
{
    [Required, StringLength(2000)] public string Notes { get; init; } = string.Empty;
}

public sealed record StatusCountDto(string Status, int Count);
public sealed record DashboardSummaryDto(int TotalFlights, int Images, int PendingViolations,
    int ConfirmedViolations, int DismissedViolations, int? CompletedProcessingCount,
    IReadOnlyList<ViolationSummaryDto> RecentViolations, IReadOnlyList<StatusCountDto> CountsByStatus,
    // Phase 8 (additive): the headline counts above mix real, experimental and synthetic results.
    // This breakdown lets the UI show what each number is actually made of.
    ResultBreakdownDto? Breakdown = null);

/// <summary>
/// Splits results by how they were produced, so development and experimental data can never be
/// presented as confirmed municipal enforcement figures.
/// </summary>
public sealed record ResultBreakdownDto(
    int RealYoloAnalyses, int MockAnalyses,
    int GeographicDetections, int ModelOnlyDetections,
    int ExperimentalCandidates, int MockCandidates, int ValidatedCandidates,
    // Rows written before provenance tracking. Their origin is genuinely unknown, so they are
    // counted on their own rather than folded into "validated" — most of them are development data.
    int LegacyUnclassifiedCandidates,
    int HumanConfirmedViolations, int HumanConfirmedExcludingSynthetic);

public sealed record AuditLogDto(long Id, Guid? ActorId, string? ActorName, Guid? MunicipalityId, string Action, string EntityType,
    Guid? EntityId, string? BeforeState, string? AfterState, string? IpAddress, DateTimeOffset CreatedAt);
