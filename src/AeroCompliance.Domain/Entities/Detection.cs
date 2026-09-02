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

    public ICollection<ComplianceEvaluation> ComplianceEvaluations { get; set; } = new List<ComplianceEvaluation>();
}
