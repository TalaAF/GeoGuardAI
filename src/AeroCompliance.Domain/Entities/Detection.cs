using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>
/// An AI segmentation output vectorised to polygons. Maps to <c>detections</c>.
/// Belongs to either an <see cref="Orthomosaic"/> or an <see cref="Image"/> (DB CHECK enforces one).
/// </summary>
public class Detection
{
    public Guid Id { get; set; }

    public Guid? ImageAnalysisId { get; set; }
    public ImageAnalysis? ImageAnalysis { get; set; }
    public Guid? ExternalDetectionId { get; set; }
    public int? ClassId { get; set; }

    public Guid? OrthomosaicId { get; set; }
    public Orthomosaic? Orthomosaic { get; set; }

    public Guid? ImageId { get; set; }
    public Image? Image { get; set; }

    /// <summary>building, road, tree, fence, sidewalk, ...</summary>
    public string ClassLabel { get; set; } = null!;

    /// <summary>Model confidence in [0, 1] (NUMERIC(5,4)).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Vectorised shape, MultiPolygon SRID 4326.</summary>
    public MultiPolygon Geom { get; set; } = null!;

    public decimal? AreaM2 { get; set; }
    public decimal? PerimeterM { get; set; }

    /// <summary>Height in metres from photogrammetry, when available.</summary>
    public decimal? HeightM { get; set; }

    public string ModelName { get; set; } = null!;
    public string ModelVersion { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Phase 8 — geometry provenance. <see cref="Geom"/> alone cannot say how a coordinate was
    /// obtained, and an approximate georeference must never be mistaken for a surveyed one.
    /// Defaults describe the pre-existing exact case, so untouched rows keep their meaning.
    /// </summary>
    /// <remarks>
    /// RealYoloEstimatedGeometry | RealYoloGeoreferencedGeometry | RealYoloModelOnly |
    /// MockGeographicScenario | ImportedGisReference
    /// </remarks>
    public string? ResultSource { get; set; }

    /// <summary>Geometry came from an estimated transform; positions are indicative only.</summary>
    public bool IsApproximate { get; set; }

    /// <summary>No downstream decision may be treated as final without a human check.</summary>
    public bool RequiresManualReview { get; set; }

    /// <summary>Low | Deterministic | Surveyed | NotGeoreferenced.</summary>
    public string? GeoreferencingConfidence { get; set; }

    /// <summary>e.g. EstimatedFromSingleAnchorAndOsm.</summary>
    public string? SourceGeoreferencingMethod { get; set; }

    /// <summary>Distance from the trusted-ROI anchor, when an ROI restriction applied.</summary>
    public double? DistanceFromAnchorM { get; set; }

    public ICollection<ComplianceEvaluation> ComplianceEvaluations { get; set; } = new List<ComplianceEvaluation>();
}
