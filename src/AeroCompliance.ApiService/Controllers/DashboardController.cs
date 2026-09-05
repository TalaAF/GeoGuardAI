using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Operations;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken ct)
    {
        var flights = user.IsPlatformAdmin ? db.Flights.AsQueryable() : db.Flights.Where(x => x.MunicipalityId == user.MunicipalityId);
        var flightIds = flights.Select(x => x.Id);
        var violations = user.IsPlatformAdmin ? db.Violations.AsQueryable() : db.Violations.Where(x =>
            (x.Detection.Image != null && x.Detection.Image.Flight.MunicipalityId == user.MunicipalityId) ||
            (x.Detection.Orthomosaic != null && x.Detection.Orthomosaic.Flight.MunicipalityId == user.MunicipalityId));
        var grouped = await violations.AsNoTracking().GroupBy(x => x.CurrentStatus.Code).Select(g => new StatusCountDto(g.Key, g.Count())).ToListAsync(ct);
        var recent = await violations.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(10)
            .Select(x => new ViolationSummaryDto(x.Id, x.ViolationType, x.CurrentStatus.Code, x.AiConfidenceScore,
                x.Detection.Image != null ? x.Detection.Image.FlightId : x.Detection.Orthomosaic != null ? x.Detection.Orthomosaic.FlightId : null,
                x.Detection.Image != null ? x.Detection.Image.Flight.MissionName : x.Detection.Orthomosaic != null ? x.Detection.Orthomosaic.Flight.MissionName : null,
                x.CreatedAt, x.UpdatedAt,
                x.ResultSource, x.IsApproximate, x.IsExperimental, x.RequiresManualReview)).ToListAsync(ct);
        int Count(string code) => grouped.FirstOrDefault(x => x.Status == code)?.Count ?? 0;

        // Break the headline numbers down by provenance. Synthetic and experimental results are
        // counted separately so a confirmed-violation figure is never inflated by mock data.
        var analyses = db.ImageAnalyses.AsNoTracking().Where(a => flightIds.Contains(a.Image.FlightId));
        var scopedDetections = db.Detections.AsNoTracking()
            .Where(d => d.Image != null && flightIds.Contains(d.Image.FlightId));
        var confirmed = violations.AsNoTracking().Where(v => v.CurrentStatus.Code == "confirmed_under_notification");
        var breakdown = new ResultBreakdownDto(
            RealYoloAnalyses: await analyses.CountAsync(a => a.Mode == "yolo", ct),
            MockAnalyses: await analyses.CountAsync(a => a.Mode == "mock", ct),
            GeographicDetections: await scopedDetections.CountAsync(ct),
            ModelOnlyDetections: await analyses.SumAsync(a => (int?)a.ModelOnlyDetectionCount, ct) ?? 0,
            ExperimentalCandidates: await violations.AsNoTracking().CountAsync(v => v.IsExperimental, ct),
            MockCandidates: await violations.AsNoTracking().CountAsync(v => v.ResultSource == "MockGeographicScenario", ct),
            // "Validated" requires a positively recorded non-synthetic provenance. A row with no
            // recorded source is NOT evidence of a validated result and must not be counted as one.
            ValidatedCandidates: await violations.AsNoTracking().CountAsync(
                v => !v.IsExperimental && v.ResultSource != null
                     && v.ResultSource != "MockGeographicScenario", ct),
            LegacyUnclassifiedCandidates: await violations.AsNoTracking()
                .CountAsync(v => v.ResultSource == null, ct),
            HumanConfirmedViolations: await confirmed.CountAsync(ct),
            HumanConfirmedExcludingSynthetic: await confirmed.CountAsync(
                v => !v.IsExperimental && v.ResultSource != null
                     && v.ResultSource != "MockGeographicScenario", ct));

        return Ok(new DashboardSummaryDto(await flights.CountAsync(ct), await db.Images.CountAsync(x => flightIds.Contains(x.FlightId), ct),
            Count("under_review"), Count("confirmed_under_notification"), Count("dismissed"),
            await db.Images.CountAsync(x => flightIds.Contains(x.FlightId) && x.ProcessingStatus == ImageProcessingStatus.Completed, ct), recent, grouped,
            breakdown));
    }
}
