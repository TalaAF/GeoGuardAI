using NetTopologySuite.Geometries;
using AeroCompliance.Domain.Enums;

namespace AeroCompliance.Domain.Entities;

/// <summary>A raw drone image. Maps to <c>images</c>.</summary>
public class Image
{
    public Guid Id { get; set; }

    public Guid FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    /// <summary>Object-storage path in MinIO/S3.</summary>
    public string StoragePath { get; set; } = null!;
    public string Bucket { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long ByteLength { get; set; }

    /// <summary>SHA-256 hex digest (CHAR(64)) for tamper detection.</summary>
    public string Sha256Hash { get; set; } = null!;

    public DateTimeOffset CapturedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public decimal? AltitudeM { get; set; }
    public decimal? CameraHeadingDeg { get; set; }

    /// <summary>Horizontal accuracy in cm from RTK GPS.</summary>
    public decimal? GpsAccuracyCm { get; set; }

    /// <summary>
    /// Point geometry (SRID 4326) derived from <see cref="Longitude"/>/<see cref="Latitude"/>.
    /// In PostgreSQL this is a <c>GENERATED ALWAYS AS ... STORED</c> column — the database computes it,
    /// so the property is read-only from the application's perspective (configured as a computed column
    /// in EF; never written by the app).
    /// </summary>
    public Point? Geom { get; private set; }

    public bool IsPartOfOrtho { get; set; }
    public DateTimeOffset? UploadCompletedAt { get; set; }
    public ImageProcessingStatus ProcessingStatus { get; set; } = ImageProcessingStatus.Uploading;
    public Guid? ProcessingRequestId { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? ProcessingStartedAt { get; set; }
    public DateTimeOffset? ProcessingCompletedAt { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? SourceRasterCrs { get; set; }
    public int? RasterWidth { get; set; }
    public int? RasterHeight { get; set; }
    public string? AffineTransform { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ImageAnalysis> Analyses { get; set; } = new List<ImageAnalysis>();
}
