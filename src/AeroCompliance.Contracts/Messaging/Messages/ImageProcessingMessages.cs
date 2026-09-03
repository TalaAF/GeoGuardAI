using System.Text.Json;

namespace AeroCompliance.Contracts.Messaging.Messages;

public sealed record ImageProcessingRequested(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, Guid FlightId, Guid MunicipalityId, string Bucket, string ObjectKey,
    string Sha256, string ContentType, DateTimeOffset CapturedAt, double? Latitude, double? Longitude,
    string? MockScenario,
    // Phase 5 (additive, optional — old producers/consumers unaffected): provenance of the capture
    // time/location above, so the AI Worker and ProcessingService can tell "known from EXIF/GeoTIFF"
    // apart from "typed/picked by a user" apart from "missing" without re-deriving it.
    string? LocationSource = null, string? CaptureTimeSource = null);

public sealed record ImageAnalysisStarted(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Mode, string ModelName, string ModelVersion,
    decimal ConfidenceThreshold, DateTimeOffset StartedAt);

public sealed record AnalysisDetection(
    Guid DetectionId, int ClassId, string ClassName, decimal Confidence, JsonElement Geometry);

public sealed record ImageAnalysisCompleted(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Mode, string ModelName, string ModelVersion,
    decimal ConfidenceThreshold, string? SourceCrs, string OutputCrs, DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt, long DurationMs, IReadOnlyList<AnalysisDetection> Detections,
    // Phase 5 (additive, optional): raster georeferencing the AI Worker read via rasterio when the
    // uploaded file was a GeoTIFF and the raster-capable build is running. Null whenever the source
    // wasn't a raster, or the worker's lightweight (mock-only) build has no rasterio installed.
    int? RasterWidth = null, int? RasterHeight = null, string? AffineTransform = null);

public sealed record ImageAnalysisFailed(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Stage, string ErrorCode, string Message,
    bool Retryable, DateTimeOffset FailedAt);
