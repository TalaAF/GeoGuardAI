namespace AeroCompliance.Domain.Entities;

public class ComplianceEvaluation
{
    public Guid Id { get; set; }
    public Guid ImageAnalysisId { get; set; }
    public ImageAnalysis ImageAnalysis { get; set; } = null!;
    public Guid DetectionId { get; set; }
    public Detection Detection { get; set; } = null!;
    public Guid? ZoningRuleId { get; set; }
    public ZoningRule? ZoningRule { get; set; }
    public string EvaluationType { get; set; } = null!;
    public decimal? MeasuredValue { get; set; }
    public decimal? RequiredValue { get; set; }
    public string Unit { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public int MetricCrs { get; set; } = 32636;
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// Phase 8 — provenance inherited from the evaluated detection, so an evaluation carries its
    /// own reliability rather than requiring a join to discover it.
    /// </summary>
    public bool IsApproximate { get; set; }
    public bool RequiresManualReview { get; set; }
    public string? GeoreferencingConfidence { get; set; }
    public string? SourceGeoreferencingMethod { get; set; }
    public string? ResultSource { get; set; }

    public Violation? Violation { get; set; }
}
