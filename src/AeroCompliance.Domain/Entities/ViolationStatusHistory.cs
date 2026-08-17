namespace AeroCompliance.Domain.Entities;

/// <summary>Immutable audit record of a violation status change. Maps to <c>violation_status_history</c>.</summary>
public class ViolationStatusHistory
{
    public Guid Id { get; set; }

    public Guid ViolationId { get; set; }
    public Violation Violation { get; set; } = null!;

    public int StatusId { get; set; }
    public ViolationStatus Status { get; set; } = null!;

    /// <summary>NULL when the change was automated (e.g. initial AI flag).</summary>
    public Guid? ChangedBy { get; set; }
    public User? ChangedByUser { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
