using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Common;
using AeroCompliance.Contracts.Dtos.Municipalities;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController, Route("api/municipalities"), Authorize]
public sealed class MunicipalitiesController(AeroComplianceDbContext db, ICurrentUserContext currentUser, IAuditService audit) : ControllerBase
{
    [HttpGet, Authorize(Policy = Policies.RequireAdmin)]
    public async Task<ActionResult<PagedResponse<MunicipalityDto>>> GetAll(int page = 1, int pageSize = 25, string? search = null, bool? apiAccessEnabled = null, CancellationToken ct = default)
    {
        if (!ApiSupport.IsValidPagination(page, pageSize)) return BadPagination();
        var normalized = ApiSupport.NormalizeSearch(search);
        var query = db.Municipalities.AsNoTracking();
        if (normalized is not null) query = query.Where(x => x.Name.ToLower().Contains(normalized) || (x.Region != null && x.Region.ToLower().Contains(normalized)));
        if (apiAccessEnabled.HasValue) query = query.Where(x => x.ApiAccessEnabled == apiAccessEnabled);
        return Ok(await query.OrderBy(x => x.Name).Select(ToDto()).ToPagedAsync(page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MunicipalityDto>> GetById(Guid id, CancellationToken ct)
    {
        if (!currentUser.IsPlatformAdmin && currentUser.MunicipalityId != id) return NotFound();
        var item = await db.Municipalities.AsNoTracking().Where(x => x.Id == id).Select(ToDto()).SingleOrDefaultAsync(ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost, Authorize(Policy = Policies.ManageMunicipalities)]
    public async Task<ActionResult<MunicipalityDto>> Create(SaveMunicipalityRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.Municipalities.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct)) return ConflictProblem("A municipality with this name already exists.");
        var entity = new Municipality { Id = Guid.NewGuid(), Name = name, Region = request.Region?.Trim(), MouSignedAt = request.MouSignedAt, ApiAccessEnabled = request.ApiAccessEnabled, IsPilot = request.IsPilot, CreatedAt = DateTimeOffset.UtcNow };
        db.Municipalities.Add(entity);
        audit.Add("municipality.create", "Municipality", entity.Id, after: new { entity.Name, entity.Region, entity.ApiAccessEnabled, entity.IsPilot }, municipalityId: entity.Id);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, Map(entity));
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Policies.ManageMunicipalities)]
    public async Task<ActionResult<MunicipalityDto>> Update(Guid id, SaveMunicipalityRequest request, CancellationToken ct)
    {
        var entity = await db.Municipalities.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        var name = request.Name.Trim();
        if (await db.Municipalities.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct)) return ConflictProblem("A municipality with this name already exists.");
        var before = new { entity.Name, entity.Region, entity.MouSignedAt, entity.ApiAccessEnabled, entity.IsPilot };
        entity.Name = name; entity.Region = request.Region?.Trim(); entity.MouSignedAt = request.MouSignedAt; entity.ApiAccessEnabled = request.ApiAccessEnabled; entity.IsPilot = request.IsPilot;
        audit.Add("municipality.update", "Municipality", id, before, new { entity.Name, entity.Region, entity.MouSignedAt, entity.ApiAccessEnabled, entity.IsPilot }, entity.Id);
        await db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    private static System.Linq.Expressions.Expression<Func<Municipality, MunicipalityDto>> ToDto() => x => new(x.Id, x.Name, x.Region, x.MouSignedAt, x.ApiAccessEnabled, x.IsPilot, x.CreatedAt);
    private static MunicipalityDto Map(Municipality x) => new(x.Id, x.Name, x.Region, x.MouSignedAt, x.ApiAccessEnabled, x.IsPilot, x.CreatedAt);
    private ObjectResult BadPagination() => Problem(statusCode: 400, title: "Invalid pagination", detail: "page must be at least 1 and pageSize must be between 1 and 100.");
    private ObjectResult ConflictProblem(string detail) => Problem(statusCode: 409, title: "Conflict", detail: detail);
}
