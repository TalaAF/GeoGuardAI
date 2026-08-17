namespace AeroCompliance.Domain.Entities;

/// <summary>A generated (PDF) violation report. Maps to <c>reports</c>.</summary>
public class Report
{
    public Guid Id { get; set; }

    public Guid ViolationId { get; set; }
    public Violation Violation { get; set; } = null!;

    /// <summary>Object-storage path of the PDF.</summary>
    public string StoragePath { get; set; } = null!;

    /// <summary>Generator provenance; defaults to <c>ai_draft</c>.</summary>
    public string GeneratedBy { get; set; } = "ai_draft";
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>Human reviewer who signed off the report.</summary>
    public Guid? SignedOffBy { get; set; }
    public User? SignedOffByUser { get; set; }
    public DateTimeOffset? SignedOffAt { get; set; }
}
