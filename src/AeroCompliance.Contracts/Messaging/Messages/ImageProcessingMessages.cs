using System.Text.Json;

namespace AeroCompliance.Contracts.Messaging.Messages;

public sealed record ImageProcessingRequested(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, Guid FlightId, Guid MunicipalityId, string Bucket, string ObjectKey,
    string Sha256, string ContentType, DateTimeOffset CapturedAt, double? Latitude, double? Longitude,
    string? MockScenario);

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
    DateTimeOffset CompletedAt, long DurationMs, IReadOnlyList<AnalysisDetection> Detections);

public sealed record ImageAnalysisFailed(
    Guid EventId, int SchemaVersion, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAt,
    Guid RequestId, Guid ImageId, int Attempt, string Stage, string ErrorCode, string Message,
    bool Retryable, DateTimeOffset FailedAt);
