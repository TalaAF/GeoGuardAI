namespace AeroCompliance.Domain.Entities;

/// <summary>
/// Lookup/reference table of violation lifecycle statuses (seeded). Maps to <c>violation_statuses</c>.
/// SERIAL PK; codes such as <c>under_review</c>, <c>notified</c>, <c>resolved</c>.
/// </summary>
public class ViolationStatus
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? LabelAr { get; set; }
    public string? LabelEn { get; set; }

    public ICollection<Violation> Violations { get; set; } = new List<Violation>();
    public ICollection<ViolationStatusHistory> History { get; set; } = new List<ViolationStatusHistory>();
}
