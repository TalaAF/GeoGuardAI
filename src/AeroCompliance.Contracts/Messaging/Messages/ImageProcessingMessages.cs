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
    Guid DetectionId, int ClassId, string ClassName, decimal Confidence, JsonElement Geometry,
    // Phase 8 (additive, optional): provenance of this detection's geometry. Defaults keep the
    // pre-existing behaviour for any producer that does not send them — a detection with no
    // provenance is treated as exact, not approximate.
    string? ResultSource = null, bool IsApproximate = false, bool RequiresManualReview = false,
    string? GeoreferencingConfidence = null, string? SourceGeoreferencingMethod = null,
    double? DistanceFromAnchorMetres = null);

public sealed record ImageAnalysisCompleted(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Mode, string ModelName, string ModelVersion,
    decimal ConfidenceThreshold, string? SourceCrs, string OutputCrs, DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt, long DurationMs, IReadOnlyList<AnalysisDetection> Detections,
    // Phase 5 (additive, optional): raster georeferencing the AI Worker read via rasterio when the
    // uploaded file was a GeoTIFF and the raster-capable build is running. Null whenever the source
    // wasn't a raster, or the worker's lightweight (mock-only) build has no rasterio installed.
    int? RasterWidth = null, int? RasterHeight = null, string? AffineTransform = null,
    // Phase 8 (additive, optional): real predictions the worker deliberately left non-geographic
    // (outside a trusted ROI, or an ungeoreferenced raster), plus the raster's own declaration that
    // its georeference is approximate. Reported so nothing the model saw is silently discarded.
    string? ModelOnlyDetections = null, int ModelOnlyCount = 0, string? GeoreferencingPolicy = null);

public sealed record ImageAnalysisFailed(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Stage, string ErrorCode, string Message,
    bool Retryable, DateTimeOffset FailedAt);
