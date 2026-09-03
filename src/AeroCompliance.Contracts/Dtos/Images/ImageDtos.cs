using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AeroCompliance.Contracts.Dtos.Images;

public sealed class ImageUploadForm
{
    /// <summary>
    /// Optional as of Phase 5: when omitted, the server falls back to EXIF <c>DateTimeOriginal</c>
    /// (JPEG) or, failing that, the upload time (recorded as <see cref="CaptureTimeSource"/> = "Missing").
    /// </summary>
    public DateTimeOffset? CapturedAt { get; init; }
    [Range(-90, 90)] public double? Latitude { get; init; }
    [Range(-180, 180)] public double? Longitude { get; init; }
    public decimal? AltitudeM { get; init; }
    public decimal? CameraHeadingDeg { get; init; }
    public decimal? GpsAccuracyCm { get; init; }
    public string? MockScenario { get; init; }

    /// <summary>
    /// When true, trust this request's <see cref="Latitude"/>/<see cref="Longitude"/> even if the
    /// file's own EXIF GPS tags disagree or are present — an explicit manual/map-picked override
    /// rather than a fallback for a file with no location of its own.
    /// </summary>
    public bool OverrideLocation { get; init; }
}

public sealed record ImageReceiptDto(Guid ImageId, Guid FlightId, string ProcessingStatus,
    string OriginalFileName, long Size, string Sha256, string StatusUrl,
    DateTimeOffset EffectiveCapturedAt, double? EffectiveLatitude, double? EffectiveLongitude,
    string LocationSource, string CaptureTimeSource, bool MetadataRequiresReview);
public sealed record ImageDto(Guid Id, Guid FlightId, string OriginalFileName, string ContentType,
    long ByteLength, string Sha256, DateTimeOffset CapturedAt, double? Latitude, double? Longitude,
    decimal? AltitudeM, decimal? CameraHeadingDeg, decimal? GpsAccuracyCm, string ProcessingStatus,
    int AttemptCount, DateTimeOffset? UploadedAt, DateTimeOffset CreatedAt,
    string LocationSource, string CaptureTimeSource, bool MetadataRequiresReview,
    string? SourceRasterCrs, int? RasterWidth, int? RasterHeight);
public sealed record ImageProcessingStatusDto(Guid ImageId, string Status, Guid? RequestId, int AttemptCount,
    DateTimeOffset? QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    string? ErrorCode, string? ErrorMessage);

public sealed record DetectionSummaryDto(Guid Id, Guid ImageId, Guid? ImageAnalysisId, int? ClassId,
    string ClassName, decimal Confidence, JsonElement Geometry, DateTimeOffset CreatedAt);
public sealed record DetectionDetailsDto(Guid Id, Guid ImageId, Guid? ImageAnalysisId, Guid? ExternalDetectionId,
    int? ClassId, string ClassName, decimal Confidence, JsonElement Geometry, decimal? AreaM2,
    decimal? PerimeterM, decimal? HeightM, string ModelName, string ModelVersion, DateTimeOffset CreatedAt);
