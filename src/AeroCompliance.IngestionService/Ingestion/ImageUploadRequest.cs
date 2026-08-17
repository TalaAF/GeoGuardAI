namespace AeroCompliance.IngestionService.Ingestion;

/// <summary>
/// Metadata accompanying a drone image upload. Epic 1: shape only — the endpoint records the
/// metadata and emits the <c>ImageUploaded</c> event; actual multipart file receipt and MinIO
/// storage arrive in a later epic.
/// </summary>
/// <param name="FlightId">Flight this image belongs to.</param>
/// <param name="StoragePath">Intended object-storage key/path for the image.</param>
/// <param name="Sha256Hash">SHA-256 hex digest of the image bytes.</param>
/// <param name="CapturedAt">Capture timestamp from the drone.</param>
/// <param name="Latitude">Capture latitude (WGS84).</param>
/// <param name="Longitude">Capture longitude (WGS84).</param>
public record ImageUploadRequest(
    Guid FlightId,
    string StoragePath,
    string Sha256Hash,
    DateTimeOffset CapturedAt,
    double Latitude,
    double Longitude);
