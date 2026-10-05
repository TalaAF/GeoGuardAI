using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Common;
using AeroCompliance.Contracts.Dtos.Operations;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.Admin + "," + Roles.MunicipalityManager)]
public sealed class AuditController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> List(
        int page = 1,
        int pageSize = 25,
        Guid? actorId = null,
        string? action = null,
        string? entityType = null,
        Guid? entityId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        if (!ApiSupport.IsValidPagination(page, pageSize)) return Problem(statusCode: 400, title: "Invalid pagination");
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!user.IsPlatformAdmin)
        {
            var tenant = user.MunicipalityId;
            q = q.Where(x => x.MunicipalityId == tenant ||
                (x.EntityType == "Municipality" && x.EntityId == tenant) ||
                (x.EntityType == "User" && db.Users.Any(e => e.Id == x.EntityId && e.MunicipalityId == tenant)) ||
                (x.EntityType == "Zone" && db.Zones.Any(e => e.Id == x.EntityId && e.MunicipalityId == tenant)) ||
                (x.EntityType == "Block" && db.Blocks.Any(e => e.Id == x.EntityId && e.Zone.MunicipalityId == tenant)) ||
                (x.EntityType == "Parcel" && db.Parcels.Any(e => e.Id == x.EntityId && e.Block.Zone.MunicipalityId == tenant)) ||
                (x.EntityType == "Road" && db.Roads.Any(e => e.Id == x.EntityId && e.MunicipalityId == tenant)) ||
                (x.EntityType == "ZoningRule" && db.ZoningRules.Any(e => e.Id == x.EntityId && e.MunicipalityId == tenant)) ||
                (x.EntityType == "Flight" && db.Flights.Any(e => e.Id == x.EntityId && e.MunicipalityId == tenant)) ||
                (x.EntityType == "FlightMission" && db.FlightMissions.Any(e => e.Id == x.EntityId && e.Flight.MunicipalityId == tenant)));
        }
        if (actorId.HasValue) q = q.Where(x => x.ActorId == actorId);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(x => x.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.EntityType == entityType.Trim());
        if (entityId.HasValue) q = q.Where(x => x.EntityId == entityId);
        if (from.HasValue) q = q.Where(x => x.CreatedAt >= from);
        if (to.HasValue) q = q.Where(x => x.CreatedAt <= to);
        return Ok(await q.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AuditLogDto(x.Id, x.ActorId, x.Actor != null ? x.Actor.FullName : null,
                x.MunicipalityId, x.Action, x.EntityType, x.EntityId, x.BeforeState, x.AfterState,
                x.IpAddress != null ? x.IpAddress.ToString() : null, x.CreatedAt))
            .ToPagedAsync(page, pageSize, ct));
    }
}
