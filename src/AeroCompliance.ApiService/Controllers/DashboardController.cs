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
                x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
        int Count(string code) => grouped.FirstOrDefault(x => x.Status == code)?.Count ?? 0;
        return Ok(new DashboardSummaryDto(await flights.CountAsync(ct), await db.Images.CountAsync(x => flightIds.Contains(x.FlightId), ct),
            Count("under_review"), Count("confirmed_under_notification"), Count("dismissed"),
            await db.Images.CountAsync(x => flightIds.Contains(x.FlightId) && x.ProcessingStatus == ImageProcessingStatus.Completed, ct), recent, grouped));
    }
}
