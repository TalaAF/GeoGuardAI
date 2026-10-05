using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>
/// A recorded compliance violation. Maps to <c>violations</c>. Sensitive: never hard-deleted.
/// </summary>
public class Violation
{
    public Guid Id { get; set; }

    public Guid? ComplianceEvaluationId { get; set; }
    public ComplianceEvaluation? ComplianceEvaluation { get; set; }

    public Guid DetectionId { get; set; }
    public Detection Detection { get; set; } = null!;

    public Guid? ParcelId { get; set; }
    public Parcel? Parcel { get; set; }

    public Guid? RoadId { get; set; }
    public Road? Road { get; set; }

    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public Guid? BlockId { get; set; }
    public Block? Block { get; set; }

    public Guid? RuleId { get; set; }
    public ZoningRule? Rule { get; set; }

    /// <summary>road_setback, neighbor_boundary_encroachment, parcel_overflow, tree_encroachment, ...</summary>
    public string ViolationType { get; set; } = null!;

    /// <summary>Measured value (e.g. 2.15 m).</summary>
    public decimal? MeasuredValue { get; set; }

    /// <summary>Legally required value (e.g. 3.00 m).</summary>
    public decimal? RequiredValue { get; set; }

    /// <summary>Amount of exceedance (e.g. 0.85 m).</summary>
    public decimal? ExceedanceValue { get; set; }

    /// <summary>Encroachment area in m², when applicable.</summary>
    public decimal? ExceedanceAreaM2 { get; set; }

    public decimal AiConfidenceScore { get; set; }

    /// <summary>Pre-existing at onboarding time ("zero state").</summary>
    public bool IsZeroState { get; set; }

    public int CurrentStatusId { get; set; }
    public ViolationStatus CurrentStatus { get; set; } = null!;

    public Guid? EvidenceImageId { get; set; }
    public Image? EvidenceImage { get; set; }

    /// <summary>Snapshot of the detection geometry at record time, MultiPolygon SRID 4326.</summary>
    public MultiPolygon Geom { get; set; } = null!;

    /// <summary>
    /// Phase 8 — reliability of the geometry this violation rests on. A violation derived from an
    /// estimated georeference is experimental evidence, not an enforceable finding: it must be
    /// presented as approximate and can never be auto-confirmed.
    /// </summary>
    public bool IsApproximate { get; set; }
    public bool RequiresManualReview { get; set; }
    public string? GeoreferencingConfidence { get; set; }
    public string? SourceGeoreferencingMethod { get; set; }
    public string? ResultSource { get; set; }

    /// <summary>
    /// Set when this violation may only be used for experimentation — never for enforcement,
    /// cadastral, surveying or legal purposes.
    /// </summary>
    public bool IsExperimental { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ViolationStatusHistory> StatusHistory { get; set; } = new List<ViolationStatusHistory>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
