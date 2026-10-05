namespace AeroCompliance.Contracts.Dtos.Analytics;

/// <summary>The resolved (UTC-normalised) window an analytics response covers.</summary>
public sealed record AnalyticsRangeDto(DateTimeOffset From, DateTimeOffset To, int Days);

/// <summary>
/// Headline operational counters. <see cref="AverageConfidence"/> and
/// <see cref="AverageProcessingSeconds"/> are null when no row in the window carries the
/// underlying value, so the frontend can say "not available" instead of showing a fake zero.
/// </summary>
public sealed record AnalyticsSummaryDto(
    int TotalFlights, int CompletedFlights,
    int ImagesUploaded, int ImagesProcessed, int ImagesFailed,
    int TotalDetections,
    int CandidateViolations, int ConfirmedViolations, int DismissedViolations,
    decimal? AverageConfidence, double? AverageProcessingSeconds);

/// <summary>One calendar day (UTC) of the activity series. Gap days are returned as zeroes.</summary>
public sealed record AnalyticsDailyPointDto(DateOnly Day, int ImagesUploaded, int Detections,
    int CandidateViolations, int ViolationsConfirmed, int ViolationsDismissed);

/// <summary>A distribution bucket. <see cref="Key"/> is the canonical code, <see cref="Label"/> the display text.</summary>
public sealed record AnalyticsCategoryDto(string Key, string Label, int Count);

/// <summary>A ranked geographic entity, identified by its human-readable reference (never an id).</summary>
public sealed record AnalyticsRankingDto(string Reference, string? Name, int Count);

/// <summary>
/// Processing-duration statistics over images that have both a start and completion timestamp.
/// <see cref="MeasuredCount"/> says how many images those durations were computed from.
/// </summary>
public sealed record AnalyticsProcessingDto(double? AverageSeconds, double? FastestSeconds, double? SlowestSeconds,
    double? SuccessRate, int FailedCount, int MeasuredCount);

public sealed record AnalyticsOverviewDto(
    AnalyticsRangeDto Range,
    string? MunicipalityName,
    bool PlatformWide,
    AnalyticsSummaryDto Summary,
    IReadOnlyList<AnalyticsDailyPointDto> Daily,
    IReadOnlyList<AnalyticsCategoryDto> ViolationsByStatus,
    IReadOnlyList<AnalyticsCategoryDto> DetectionsByClass,
    IReadOnlyList<AnalyticsCategoryDto> ViolationsByType,
    IReadOnlyList<AnalyticsCategoryDto> FlightsByStatus,
    IReadOnlyList<AnalyticsCategoryDto> ImagesByStatus,
    IReadOnlyList<AnalyticsRankingDto> TopRoads,
    IReadOnlyList<AnalyticsRankingDto> TopZones,
    IReadOnlyList<AnalyticsRankingDto> TopParcels,
    AnalyticsProcessingDto Processing);
