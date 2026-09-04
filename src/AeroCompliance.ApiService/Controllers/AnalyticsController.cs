using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Analytics;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>
/// Operational analytics over the tenant's own flights, images, detections and violations.
/// Every figure is produced by a database aggregation over the requested window — no entity
/// lists are materialised, and nothing is synthesised when data is absent.
/// </summary>
[ApiController, Route("api/analytics"), Authorize(Policy = Policies.ViewAnalytics)]
public sealed class AnalyticsController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    private const int MaxRangeDays = 366;
    private const int DefaultRangeDays = 30;
    private const int RankingSize = 5;

    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverviewDto>> Overview(
        DateTimeOffset? from = null, DateTimeOffset? to = null, Guid? municipalityId = null, CancellationToken ct = default)
    {
        // --- Range: normalise to whole UTC days so the daily series lines up with the buckets. ---
        var toUtc = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddDays(-(DefaultRangeDays - 1))).ToUniversalTime();
        var firstDay = DateOnly.FromDateTime(fromUtc.UtcDateTime);
        var lastDay = DateOnly.FromDateTime(toUtc.UtcDateTime);
        if (lastDay < firstDay)
            return Problem(statusCode: 400, title: "Invalid date range", detail: "The start date must fall on or before the end date.");
        var days = lastDay.DayNumber - firstDay.DayNumber + 1;
        if (days > MaxRangeDays)
            return Problem(statusCode: 400, title: "Date range is too long", detail: "Analytics can cover at most one year at a time.");

        var start = new DateTimeOffset(firstDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var endExclusive = new DateTimeOffset(lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // --- Tenant scope: a municipality override is honoured for platform admins only. ---
        Guid? scope;
        if (user.IsPlatformAdmin) scope = municipalityId;
        else
        {
            scope = user.MunicipalityId;
            if (scope is null) return Problem(statusCode: 403, title: "No municipality is assigned to this account.");
            // A non-admin asking for someone else's municipality is refused rather than silently rescoped.
            if (municipalityId.HasValue && municipalityId != scope)
                return Problem(statusCode: 403, title: "Analytics are limited to your own municipality.");
        }
        if (scope.HasValue && !await db.Municipalities.AnyAsync(x => x.Id == scope, ct)) return NotFound();

        var municipalityName = scope.HasValue
            ? await db.Municipalities.Where(x => x.Id == scope).Select(x => x.Name).SingleAsync(ct)
            : null;

        // --- Scoped, range-filtered base queries. ---
        var flights = db.Flights.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < endExclusive);
        if (scope.HasValue) flights = flights.Where(x => x.MunicipalityId == scope);

        var images = db.Images.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < endExclusive);
        if (scope.HasValue) images = images.Where(x => x.Flight.MunicipalityId == scope);

        var detections = db.Detections.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < endExclusive);
        if (scope.HasValue) detections = detections.Where(x =>
            (x.Image != null && x.Image.Flight.MunicipalityId == scope) ||
            (x.Orthomosaic != null && x.Orthomosaic.Flight.MunicipalityId == scope));

        var violations = db.Violations.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < endExclusive);
        if (scope.HasValue) violations = violations.Where(x =>
            (x.Detection.Image != null && x.Detection.Image.Flight.MunicipalityId == scope) ||
            (x.Detection.Orthomosaic != null && x.Detection.Orthomosaic.Flight.MunicipalityId == scope));

        // Confirm/dismiss decisions are dated by when the decision was taken, not when the
        // violation was first raised, so they are counted off the status-history table.
        var decisions = db.ViolationStatusHistory.AsNoTracking()
            .Where(x => x.ChangedAt >= start && x.ChangedAt < endExclusive);
        if (scope.HasValue) decisions = decisions.Where(x =>
            (x.Violation.Detection.Image != null && x.Violation.Detection.Image.Flight.MunicipalityId == scope) ||
            (x.Violation.Detection.Orthomosaic != null && x.Violation.Detection.Orthomosaic.Flight.MunicipalityId == scope));

        // --- Summary counters. ---
        var totalFlights = await flights.CountAsync(ct);
        var completedFlights = await flights.CountAsync(x => x.Status == FlightStatus.Completed, ct);
        var imagesUploaded = await images.CountAsync(ct);
        var imagesProcessed = await images.CountAsync(x => x.ProcessingStatus == ImageProcessingStatus.Completed, ct);
        var imagesFailed = await images.CountAsync(x => FailedStatuses.Contains(x.ProcessingStatus), ct);
        var totalDetections = await detections.CountAsync(ct);
        var averageConfidence = await detections.AnyAsync(ct)
            ? await detections.AverageAsync(x => x.Confidence, ct)
            : (decimal?)null;

        var statusCounts = (await violations
                .GroupBy(x => new { x.CurrentStatus.Code, x.CurrentStatus.LabelEn })
                .Select(g => new { g.Key.Code, g.Key.LabelEn, Count = g.Count() })
                .ToListAsync(ct))
            .Select(x => new AnalyticsCategoryDto(x.Code, x.LabelEn ?? Humanize(x.Code), x.Count))
            .ToList();
        int StatusCount(string code) => statusCounts.FirstOrDefault(x => x.Key == code)?.Count ?? 0;

        // --- Distributions. ---
        // EF cannot construct a DTO whose constructor repeats a grouping key, so each grouping
        // projects to an anonymous shape in SQL and is mapped to the DTO once materialised.
        var detectionsByClass = (await detections
                .GroupBy(x => x.ClassLabel)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ToListAsync(ct))
            .Select(x => new AnalyticsCategoryDto(x.Key, Humanize(x.Key), x.Count)).ToList();

        var violationsByType = (await violations
                .GroupBy(x => x.ViolationType)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ToListAsync(ct))
            .Select(x => new AnalyticsCategoryDto(x.Key, Humanize(x.Key), x.Count)).ToList();

        var flightsByStatus = await flights
            .GroupBy(x => x.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var imagesByStatus = await images
            .GroupBy(x => x.ProcessingStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // --- Rankings (human references only; ids never leave the query). ---
        var topRoads = (await violations.Where(x => x.Road != null)
                .GroupBy(x => new { x.Road!.RoadCode, x.Road.Name })
                .Select(g => new { g.Key.RoadCode, g.Key.Name, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.RoadCode).Take(RankingSize).ToListAsync(ct))
            .Select(x => new AnalyticsRankingDto(x.RoadCode, x.Name, x.Count)).ToList();

        var topZones = (await violations.Where(x => x.Zone != null)
                .GroupBy(x => new { x.Zone!.ZoneNumber, x.Zone.Name })
                .Select(g => new { g.Key.ZoneNumber, g.Key.Name, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.ZoneNumber).Take(RankingSize).ToListAsync(ct))
            .Select(x => new AnalyticsRankingDto(x.ZoneNumber, x.Name, x.Count)).ToList();

        var topParcels = (await violations.Where(x => x.Parcel != null)
                .GroupBy(x => x.Parcel!.ParcelNumber)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ThenBy(x => x.Key).Take(RankingSize).ToListAsync(ct))
            .Select(x => new AnalyticsRankingDto(x.Key, null, x.Count)).ToList();

        // --- Processing durations, over images that carry both timestamps. ---
        var measured = images.Where(x => x.ProcessingStartedAt != null && x.ProcessingCompletedAt != null);
        var measuredCount = await measured.CountAsync(ct);
        double? avgSeconds = null, fastest = null, slowest = null;
        if (measuredCount > 0)
        {
            // Npgsql turns the timestamptz difference into an interval and TotalSeconds into
            // EXTRACT(EPOCH FROM ...), so this aggregates in SQL rather than in the client.
            var durations = measured.Select(x =>
                (x.ProcessingCompletedAt!.Value - x.ProcessingStartedAt!.Value).TotalSeconds);
            avgSeconds = await durations.AverageAsync(ct);
            fastest = await durations.MinAsync(ct);
            slowest = await durations.MaxAsync(ct);
        }
        var finished = imagesProcessed + imagesFailed;
        var processing = new AnalyticsProcessingDto(avgSeconds, fastest, slowest,
            finished == 0 ? null : (double)imagesProcessed / finished, imagesFailed, measuredCount);

        // --- Daily series: grouped in SQL, then padded so every day in the window is present. ---
        var imagesDaily = await DailyAsync(images.Select(x => x.CreatedAt), start, ct);
        var detectionsDaily = await DailyAsync(detections.Select(x => x.CreatedAt), start, ct);
        var candidatesDaily = await DailyAsync(violations.Select(x => x.CreatedAt), start, ct);
        var confirmedDaily = await DailyAsync(decisions.Where(x => x.Status.Code == ConfirmedCode).Select(x => x.ChangedAt), start, ct);
        var dismissedDaily = await DailyAsync(decisions.Where(x => x.Status.Code == DismissedCode).Select(x => x.ChangedAt), start, ct);

        var daily = Enumerable.Range(0, days).Select(offset =>
            new AnalyticsDailyPointDto(firstDay.AddDays(offset),
                imagesDaily.GetValueOrDefault(offset), detectionsDaily.GetValueOrDefault(offset),
                candidatesDaily.GetValueOrDefault(offset), confirmedDaily.GetValueOrDefault(offset),
                dismissedDaily.GetValueOrDefault(offset))).ToList();

        return Ok(new AnalyticsOverviewDto(
            new AnalyticsRangeDto(start, endExclusive.AddTicks(-1), days),
            municipalityName,
            scope is null,
            new AnalyticsSummaryDto(totalFlights, completedFlights, imagesUploaded, imagesProcessed, imagesFailed,
                totalDetections, StatusCount(CandidateCode), StatusCount(ConfirmedCode), StatusCount(DismissedCode),
                averageConfidence, avgSeconds),
            daily,
            statusCounts.OrderByDescending(x => x.Count).ToList(),
            detectionsByClass,
            violationsByType,
            flightsByStatus.Select(x => new AnalyticsCategoryDto(x.Key.ToString(), Humanize(x.Key.ToString()), x.Count))
                .OrderByDescending(x => x.Count).ToList(),
            imagesByStatus.Select(x => new AnalyticsCategoryDto(x.Key.ToString(), Humanize(x.Key.ToString()), x.Count))
                .OrderByDescending(x => x.Count).ToList(),
            topRoads, topZones, topParcels, processing));
    }

    /// <summary>
    /// Counts rows per UTC calendar day, keyed by whole days elapsed since the window start.
    /// The provider has no date_trunc translation, but it does translate the timestamp
    /// difference and Math.Floor, so the bucketing still happens in SQL rather than in memory.
    /// Every row is at or after <paramref name="start"/> (the callers filter on it), so the
    /// offset is never negative and flooring is the correct rounding.
    /// </summary>
    private static async Task<Dictionary<int, int>> DailyAsync(
        IQueryable<DateTimeOffset> timestamps, DateTimeOffset start, CancellationToken ct)
        => (await timestamps
                .GroupBy(x => Math.Floor((x - start).TotalDays))
                .Select(g => new { Offset = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => (int)x.Offset, x => x.Count);

    /// <summary>
    /// Turns a stored code into a display label, covering both conventions in use here:
    /// PascalCase enum names ("AnalysisFailed" -> "Analysis failed") and snake_case codes
    /// ("road_encroachment" -> "Road encroachment").
    /// </summary>
    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var text = System.Text.RegularExpressions.Regex
            .Replace(value.Replace('_', ' '), "(?<!^)(?<! )([A-Z])", " $1");
        return char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
    }

    private const string CandidateCode = "under_review";
    private const string ConfirmedCode = "confirmed_under_notification";
    private const string DismissedCode = "dismissed";

    private static readonly ImageProcessingStatus[] FailedStatuses =
        [ImageProcessingStatus.UploadFailed, ImageProcessingStatus.AnalysisFailed, ImageProcessingStatus.ComplianceFailed];
}
