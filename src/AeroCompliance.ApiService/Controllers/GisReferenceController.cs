using AeroCompliance.ApiService.Services;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Gis;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>How much reference data exists for a tenant, by type.</summary>
public sealed record GisReferenceSummaryDto(
    string ReferenceType,
    string Label,
    string Description,
    int FeatureCount,
    double? AverageAreaM2,
    string? SourceLayer,
    string? SourceCrs,
    DateTimeOffset? LastImportedAt);

/// <summary>
/// Read-only access to the imported municipal reference layers, and the comparison of an AI
/// detection against them.
/// </summary>
/// <remarks>
/// Tenant-scoped like every other controller. Nothing here is a compliance finding: these endpoints
/// describe imported baselines and measured overlap, which the UI must present as evidence rather
/// than as a violation.
/// </remarks>
[ApiController, Route("api/gis-reference"), Authorize]
public sealed class GisReferenceController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyList<GisReferenceSummaryDto>>> Summary(CancellationToken ct)
    {
        var q = db.GisReferenceFeatures.AsNoTracking();
        if (!user.IsPlatformAdmin) q = q.Where(x => x.MunicipalityId == user.MunicipalityId);

        var grouped = await q
            .GroupBy(x => new { x.ReferenceType, x.SourceLayer, x.SourceCrs })
            .Select(g => new
            {
                g.Key.ReferenceType,
                g.Key.SourceLayer,
                g.Key.SourceCrs,
                Count = g.Count(),
                AverageArea = g.Average(x => x.AreaM2),
                LastImported = g.Max(x => x.ImportedAt),
            })
            .ToListAsync(ct);

        return Ok(grouped.Select(g => new GisReferenceSummaryDto(
            g.ReferenceType,
            Label(g.ReferenceType),
            Description(g.ReferenceType),
            g.Count,
            g.AverageArea is null ? null : Math.Round(g.AverageArea.Value, 2),
            g.SourceLayer,
            g.SourceCrs,
            g.LastImported)).OrderBy(x => x.ReferenceType).ToList());
    }

    /// <summary>
    /// Compares one detection with the imported reference layers. Read-only: it records nothing and
    /// never creates a violation.
    /// </summary>
    [HttpGet("detections/{detectionId:guid}/matches")]
    public async Task<ActionResult<DetectionReferenceReport>> Matches(
        Guid detectionId, double iouThreshold = GisReferenceMatcher.DefaultBuildingIouThreshold,
        CancellationToken ct = default)
    {
        if (iouThreshold is < 0 or > 1)
            return Problem(statusCode: 400, title: "iouThreshold must be between 0 and 1.");

        var detection = await Visible()
            .Include(x => x.Image!).ThenInclude(i => i.Flight)
            .Include(x => x.Orthomosaic!).ThenInclude(o => o.Flight)
            .SingleOrDefaultAsync(x => x.Id == detectionId, ct);
        if (detection is null) return NotFound();

        var municipalityId = detection.Image?.Flight.MunicipalityId
            ?? detection.Orthomosaic?.Flight.MunicipalityId;
        if (municipalityId is null)
            return Problem(statusCode: 409, title: "This detection is not linked to a municipality.");

        var matcher = new GisReferenceMatcher(db);
        return Ok(await matcher.CompareAsync(detectionId, municipalityId.Value, iouThreshold, ct));
    }

    private IQueryable<Detection> Visible() => user.IsPlatformAdmin
        ? db.Detections
        : db.Detections.Where(x =>
            (x.Image != null && x.Image.Flight.MunicipalityId == user.MunicipalityId) ||
            (x.Orthomosaic != null && x.Orthomosaic.Flight.MunicipalityId == user.MunicipalityId));

    private static string Label(string referenceType) => referenceType switch
    {
        GisReferenceTypes.BuildingGroundTruth => "Municipal building records",
        GisReferenceTypes.ParcelEncroachmentReference => "Known parcel-encroachment areas",
        GisReferenceTypes.RoadEncroachmentReference => "Known road-encroachment areas",
        _ => referenceType,
    };

    private static string Description(string referenceType) => referenceType switch
    {
        GisReferenceTypes.BuildingGroundTruth =>
            "Building footprints supplied by the municipality. Used to measure how well AI detections "
            + "agree with existing records. A match is not a violation.",
        GisReferenceTypes.ParcelEncroachmentReference =>
            "Parcel-encroachment polygons from an earlier analysis. Historical reference only — these "
            + "are not current findings and have not been reviewed here.",
        GisReferenceTypes.RoadEncroachmentReference =>
            "Road-encroachment polygons from an earlier analysis. The source data contains no complete "
            + "road network, so an overlap means the detection falls inside a previously-identified "
            + "area — it is not a general road-encroachment check.",
        _ => "",
    };
}
