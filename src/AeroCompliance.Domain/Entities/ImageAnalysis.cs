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
    /// <summary>
    /// Phase 8 — the raster's own declaration that its georeference is approximate, and the region
    /// it is trusted in. Null for a normal, fully-trusted raster.
    /// </summary>
    public string? GeoreferencingStatus { get; set; }
    public string? GeoreferencingMethod { get; set; }
    public string? GeoreferencingConfidence { get; set; }
    public double? TrustedRoiRadiusM { get; set; }

    /// <summary>
    /// Real predictions deliberately left non-geographic (outside the trusted ROI, or a raster with
    /// no usable transform), as a JSON array. Retained rather than discarded so an operator can see
    /// everything the model saw; these are NOT detections and are never evaluated for compliance.
    /// </summary>
    public string? ModelOnlyDetections { get; set; }
    public int ModelOnlyDetectionCount { get; set; }

    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
