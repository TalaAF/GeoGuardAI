namespace AeroCompliance.Contracts.Messaging.Messages;

/// <summary>
/// Event published to RabbitMQ when a drone image has been received and persisted to object
/// storage by the IngestionService. Downstream services (detection, orthomosaic stitching)
/// consume it. Epic 1: contract shape only — no producing/consuming business logic yet.
/// </summary>
/// <param name="ImageId">Identifier assigned to the stored image.</param>
/// <param name="FlightId">Flight the image belongs to.</param>
/// <param name="StoragePath">Object-storage (MinIO/S3) path/key of the uploaded object.</param>
/// <param name="Sha256Hash">SHA-256 hex digest of the image bytes, for tamper detection.</param>
/// <param name="CapturedAt">Capture timestamp reported by the drone.</param>
/// <param name="Latitude">Capture latitude (WGS84).</param>
/// <param name="Longitude">Capture longitude (WGS84).</param>
/// <param name="UploadedAt">When the ingestion service accepted and stored the object.</param>
public record ImageUploaded(
    Guid ImageId,
    Guid FlightId,
    string StoragePath,
    string Sha256Hash,
    DateTimeOffset CapturedAt,
    double Latitude,
    double Longitude,
    DateTimeOffset UploadedAt);
