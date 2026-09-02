namespace AeroCompliance.Domain.Entities;

public class ImageAnalysis
{
    public Guid Id { get; set; }
    public Guid ImageId { get; set; }
    public Image Image { get; set; } = null!;
    public Guid RequestId { get; set; }
    public string Mode { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public string ModelVersion { get; set; } = null!;
    public decimal ConfidenceThreshold { get; set; }
    public string? SourceCrs { get; set; }
    public string OutputCrs { get; set; } = "EPSG:4326";
    public string Status { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
