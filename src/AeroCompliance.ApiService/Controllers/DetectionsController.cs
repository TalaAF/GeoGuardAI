using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Common;
using AeroCompliance.Contracts.Dtos.Images;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController, Route("api/detections"), Authorize]
public sealed class DetectionsController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<DetectionSummaryDto>>> List(int page = 1, int pageSize = 25, Guid? imageId = null,
        Guid? flightId = null, string? className = null, decimal? minConfidence = null,
        // Phase 8 filters: provenance and run mode are the two axes a reviewer actually needs to
        // separate real estimated results from synthetic development data.
        string? resultSource = null, string? mode = null, bool? isApproximate = null,
        bool? requiresManualReview = null, CancellationToken ct = default)
    {
        if (!ApiSupport.IsValidPagination(page, pageSize)) return Problem(statusCode: 400, title: "Invalid pagination");
        IQueryable<AeroCompliance.Domain.Entities.Detection> q = Visible().AsNoTracking()
            .Include(x => x.Image!).ThenInclude(i => i.Flight).Include(x => x.ImageAnalysis).Where(x => x.ImageId != null);
        if (imageId.HasValue) q = q.Where(x => x.ImageId == imageId); if (flightId.HasValue) q = q.Where(x => x.Image!.FlightId == flightId);
        if (!string.IsNullOrWhiteSpace(className)) q = q.Where(x => x.ClassLabel == className.Trim()); if (minConfidence.HasValue) q = q.Where(x => x.Confidence >= minConfidence);
        if (!string.IsNullOrWhiteSpace(resultSource)) { var s = resultSource.Trim(); q = q.Where(x => x.ResultSource == s); }
        if (!string.IsNullOrWhiteSpace(mode)) { var m = mode.Trim(); q = q.Where(x => x.ImageAnalysis != null && x.ImageAnalysis.Mode == m); }
        if (isApproximate.HasValue) q = q.Where(x => x.IsApproximate == isApproximate.Value);
        if (requiresManualReview.HasValue) q = q.Where(x => x.RequiresManualReview == requiresManualReview.Value);
        var total = await q.CountAsync(ct);
        // GeoJsonService.Write() is plain C# (NetTopologySuite), not EF-translatable — materialize the
        // page of entities first (with Image/Flight eagerly Include()d above), then map in memory.
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new PagedResponse<DetectionSummaryDto>(rows.Select(x => new DetectionSummaryDto(x.Id, x.ImageId!.Value, x.ImageAnalysisId, x.ClassId,
            x.ClassLabel, x.Confidence, GeoJsonService.Write(x.Geom), x.CreatedAt,
            x.Image!.FlightId, x.Image.OriginalFileName, x.Image.Flight.MissionName,
            x.ResultSource, x.IsApproximate, x.RequiresManualReview, x.GeoreferencingConfidence,
            x.ImageAnalysis?.Mode)).ToList(), page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)));
    }

    /// <summary>Distinct AI class labels the tenant can actually see, for the class filter.</summary>
    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<string>>> Classes(CancellationToken ct) =>
        Ok(await Visible().AsNoTracking().Where(x => x.ImageId != null)
            .Select(x => x.ClassLabel).Distinct().OrderBy(x => x).ToListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DetectionDetailsDto>> Get(Guid id, CancellationToken ct)
    {
        var x = await Visible().AsNoTracking().Include(x => x.Image).ThenInclude(i => i!.Flight)
            .Include(x => x.ImageAnalysis).SingleOrDefaultAsync(x => x.Id == id && x.ImageId != null, ct); if (x is null) return NotFound();
        return Ok(new DetectionDetailsDto(x.Id, x.ImageId!.Value, x.ImageAnalysisId, x.ExternalDetectionId, x.ClassId, x.ClassLabel,
            x.Confidence, GeoJsonService.Write(x.Geom), x.AreaM2, x.PerimeterM, x.HeightM, x.ModelName, x.ModelVersion, x.CreatedAt,
            x.Image?.FlightId, x.Image?.OriginalFileName, x.Image?.Flight.MissionName,
            x.ResultSource, x.IsApproximate, x.RequiresManualReview, x.GeoreferencingConfidence,
            x.SourceGeoreferencingMethod, x.DistanceFromAnchorM, x.ImageAnalysis?.Mode,
            x.ImageAnalysis?.RequestId, x.ImageAnalysis?.GeoreferencingStatus, x.ImageAnalysis?.TrustedRoiRadiusM));
    }
    private IQueryable<AeroCompliance.Domain.Entities.Detection> Visible() => user.IsPlatformAdmin ? db.Detections : db.Detections.Where(x =>
        (x.Image != null && x.Image.Flight.MunicipalityId == user.MunicipalityId) ||
        (x.Orthomosaic != null && x.Orthomosaic.Flight.MunicipalityId == user.MunicipalityId));
}
