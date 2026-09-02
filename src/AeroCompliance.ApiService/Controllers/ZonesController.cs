using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Common;
using AeroCompliance.Contracts.Dtos.Gis;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController, Route("api/zones"), Authorize]
public sealed class ZonesController(AeroComplianceDbContext db, ICurrentUserContext user, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ZoneSummaryDto>>> List(int page = 1, int pageSize = 25, Guid? municipalityId = null, string? search = null, string? zoneType = null, CancellationToken ct = default)
    {
        if (!ApiSupport.IsValidPagination(page, pageSize)) return BadPage();
        var q = Visible().AsNoTracking();
        if (user.IsPlatformAdmin && municipalityId.HasValue) q = q.Where(x => x.MunicipalityId == municipalityId);
        var s = ApiSupport.NormalizeSearch(search); if (s is not null) q = q.Where(x => x.ZoneNumber.ToLower().Contains(s) || (x.Name != null && x.Name.ToLower().Contains(s)));
        if (!string.IsNullOrWhiteSpace(zoneType)) q = q.Where(x => x.ZoneType == zoneType.Trim());
        var total = await q.CountAsync(ct); var rows = await q.OrderBy(x => x.ZoneNumber).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new PagedResponse<ZoneSummaryDto>(rows.Select(x => new ZoneSummaryDto(x.Id, x.MunicipalityId, x.ZoneNumber, x.Name, x.ZoneType, GeoJsonService.Write(x.Geom))).ToList(), page, pageSize, total, Pages(total, pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ZoneDetailsDto>> Get(Guid id, CancellationToken ct)
    { var x = await Visible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return x is null ? NotFound() : Ok(Map(x)); }

    [HttpPost, Authorize(Policy = Policies.ManageGis)]
    public async Task<ActionResult<ZoneDetailsDto>> Create(SaveZoneRequest request, CancellationToken ct)
    {
        if (!TryTenant(request.MunicipalityId, out var tenant)) return Forbid();
        if (!await db.Municipalities.AnyAsync(x => x.Id == tenant, ct)) return NotFound();
        if (!GeoJsonService.TryReadMultiPolygon(request.Geometry, out var geom, out var error)) return GeometryProblem(error);
        var number = request.ZoneNumber.Trim();
        if (await db.Zones.AnyAsync(x => x.MunicipalityId == tenant && x.ZoneNumber == number, ct)) return ConflictProblem("Zone number already exists in this municipality.");
        var x = new Zone { Id = Guid.NewGuid(), MunicipalityId = tenant, ZoneNumber = number, Name = request.Name?.Trim(), ZoneType = request.ZoneType?.Trim(), Geom = geom, SourceDocument = request.SourceDocument?.Trim(), CreatedAt = DateTimeOffset.UtcNow };
        db.Zones.Add(x); audit.Add("zone.create", "Zone", x.Id, after: new { x.MunicipalityId, x.ZoneNumber, x.Name, x.ZoneType }, municipalityId: x.MunicipalityId); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = x.Id }, Map(x));
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManageGis)]
    public async Task<ActionResult<ZoneDetailsDto>> Update(Guid id, SaveZoneRequest request, CancellationToken ct)
    {
        var x = await Visible().SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return NotFound();
        if (user.IsPlatformAdmin && request.MunicipalityId.HasValue && request.MunicipalityId != x.MunicipalityId) return ConflictProblem("A zone cannot be moved between municipalities.");
        if (!GeoJsonService.TryReadMultiPolygon(request.Geometry, out var geom, out var error)) return GeometryProblem(error);
        var number = request.ZoneNumber.Trim(); if (await db.Zones.AnyAsync(z => z.Id != id && z.MunicipalityId == x.MunicipalityId && z.ZoneNumber == number, ct)) return ConflictProblem("Zone number already exists in this municipality.");
        var before = new { x.ZoneNumber, x.Name, x.ZoneType, x.SourceDocument }; x.ZoneNumber = number; x.Name = request.Name?.Trim(); x.ZoneType = request.ZoneType?.Trim(); x.Geom = geom; x.SourceDocument = request.SourceDocument?.Trim();
        audit.Add("zone.update", "Zone", id, before, new { x.ZoneNumber, x.Name, x.ZoneType, x.SourceDocument }, x.MunicipalityId); await db.SaveChangesAsync(ct); return Ok(Map(x));
    }

    private IQueryable<Zone> Visible() => user.IsPlatformAdmin ? db.Zones : db.Zones.Where(x => x.MunicipalityId == user.MunicipalityId);
    private bool TryTenant(Guid? requested, out Guid tenant) { if (user.IsPlatformAdmin) { tenant = requested ?? Guid.Empty; return requested.HasValue; } tenant = user.MunicipalityId ?? Guid.Empty; return user.MunicipalityId.HasValue; }
    private static ZoneDetailsDto Map(Zone x) => new(x.Id, x.MunicipalityId, x.ZoneNumber, x.Name, x.ZoneType, GeoJsonService.Write(x.Geom), x.SourceDocument, x.CreatedAt);
    private static int Pages(int total, int size) => total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
    private ObjectResult BadPage() => Problem(statusCode: 400, title: "Invalid pagination"); private ObjectResult GeometryProblem(string d) => Problem(statusCode: 400, title: "Invalid GeoJSON geometry", detail: d); private ObjectResult ConflictProblem(string d) => Problem(statusCode: 409, title: "Conflict", detail: d);
}
