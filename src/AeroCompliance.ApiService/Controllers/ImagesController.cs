using System.Text.Json;
using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Images;
using AeroCompliance.Contracts.Messaging;
using AeroCompliance.Contracts.Messaging.Messages;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

[ApiController, Route("api/images"), Authorize]
public sealed class ImagesController(AeroComplianceDbContext db, ICurrentUserContext user, IHostEnvironment environment, IAuditService audit) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImageDto>> Get(Guid id, CancellationToken ct)
    { var x = await Visible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return x is null ? NotFound() : Ok(Map(x)); }

    [HttpGet("{id:guid}/processing-status")]
    public async Task<ActionResult<ImageProcessingStatusDto>> Status(Guid id, CancellationToken ct)
    {
        var x = await Visible().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return x is null ? NotFound() : Ok(StatusMap(x) with { Analysis = await AnalysisSummary(id, ct) });
    }

    /// <summary>
    /// What the latest analysis for this image actually produced. Model-only predictions are counted
    /// separately from geographic ones: they are real detections that were deliberately left without
    /// a map position, and reporting them together would hide the ROI restriction.
    /// </summary>
    private async Task<AnalysisSummaryDto?> AnalysisSummary(Guid imageId, CancellationToken ct)
    {
        var a = await db.ImageAnalyses.AsNoTracking().Where(x => x.ImageId == imageId)
            .OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.AttemptNumber).FirstOrDefaultAsync(ct);
        if (a is null) return null;

        var geographic = await db.Detections.AsNoTracking().CountAsync(d => d.ImageAnalysisId == a.Id, ct);
        var candidates = await db.Violations.AsNoTracking()
            .CountAsync(v => v.Detection.ImageAnalysisId == a.Id, ct);
        var modelOnly = a.ModelOnlyDetectionCount;

        // Only count an "outside ROI" skip when the raster actually declared a trusted region.
        var roiRestricted = a.TrustedRoiRadiusM is > 0;
        var outsideRoi = roiRestricted ? modelOnly : 0;

        var geographicStatus = a.GeoreferencingStatus switch
        {
            "Estimated" => "Estimated",
            null or "" => geographic > 0 ? "Validated" : "Missing",
            _ => a.GeoreferencingStatus!,
        };
        var eligibility = a.GeoreferencingStatus == "Estimated" ? "ExperimentalOnly"
            : geographic > 0 ? "Eligible" : "NotEligible";
        var skipReason = modelOnly == 0 ? null
            : roiRestricted
                ? $"Geographic evaluation skipped for {modelOnly} detection(s) outside the trusted {a.TrustedRoiRadiusM:0.#} m region."
                : $"{modelOnly} detection(s) had no usable raster georeferencing and were retained as model-only.";

        return new AnalysisSummaryDto(a.Id, a.RequestId, a.Mode, a.Status, a.ModelName, a.ModelVersion,
            geographic + modelOnly, geographic, modelOnly, outsideRoi, candidates,
            geographicStatus, eligibility, skipReason,
            a.GeoreferencingMethod, a.GeoreferencingConfidence, a.TrustedRoiRadiusM,
            a.StartedAt, a.CompletedAt);
    }

    [HttpPost("{id:guid}/retry-processing"), Authorize(Policy = Policies.ManageFlights)]
    public async Task<ActionResult<ImageProcessingStatusDto>> Retry(Guid id, CancellationToken ct)
    {
        var x = await Manageable().Include(x => x.Flight).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return NotFound();
        if (x.ProcessingStatus is not (ImageProcessingStatus.AnalysisFailed or ImageProcessingStatus.ComplianceFailed))
            return Problem(statusCode: 409, title: "Image is not in a retryable terminal state.");
        var now = DateTimeOffset.UtcNow; var requestId = Guid.NewGuid(); var prior = x.ProcessingRequestId;
        var message = new ImageProcessingRequested(requestId, 1, requestId, prior, now, requestId, x.Id, x.FlightId,
            x.Flight.MunicipalityId, x.Bucket, x.ObjectKey, x.Sha256Hash, x.ContentType, x.CapturedAt, x.Latitude, x.Longitude,
            environment.IsDevelopment() ? "none" : null, x.LocationSource, x.CaptureTimeSource);
        x.ProcessingRequestId = requestId; x.ProcessingStatus = ImageProcessingStatus.Queued; x.AttemptCount++;
        x.QueuedAt = now; x.ProcessingStartedAt = null; x.ProcessingCompletedAt = null; x.LastErrorCode = null; x.LastErrorMessage = null;
        db.OutboxMessages.Add(new OutboxMessage { EventId = requestId, Type = nameof(ImageProcessingRequested), RoutingKey = MessagingConstants.ProcessingRequestRoutingKey,
            SchemaVersion = 1, Payload = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)), CreatedAt = now });
        audit.Add("image.retry", "Image", id, new { PriorRequestId = prior }, new { RequestId = requestId, x.AttemptCount }, x.Flight.MunicipalityId);
        await db.SaveChangesAsync(ct); return Accepted($"/api/images/{id}/processing-status", StatusMap(x));
    }

    private IQueryable<Image> Visible() => user.IsPlatformAdmin ? db.Images : db.Images.Where(x => x.Flight.MunicipalityId == user.MunicipalityId);
    private IQueryable<Image> Manageable() { var q = Visible(); return user.Role == Roles.DroneOperator ? q.Where(x => x.Flight.DroneOperatorId == user.UserId) : q; }
    private static ImageDto Map(Image x) => new(x.Id, x.FlightId, x.OriginalFileName, x.ContentType, x.ByteLength, x.Sha256Hash, x.CapturedAt,
        x.Latitude, x.Longitude, x.AltitudeM, x.CameraHeadingDeg, x.GpsAccuracyCm, x.ProcessingStatus.ToString(), x.AttemptCount, x.UploadCompletedAt, x.CreatedAt,
        x.LocationSource, x.CaptureTimeSource, x.MetadataRequiresReview, x.SourceRasterCrs, x.RasterWidth, x.RasterHeight);
    private static ImageProcessingStatusDto StatusMap(Image x) => new(x.Id, x.ProcessingStatus.ToString(), x.ProcessingRequestId, x.AttemptCount,
        x.QueuedAt, x.ProcessingStartedAt, x.ProcessingCompletedAt, x.LastErrorCode, x.LastErrorMessage);
}
