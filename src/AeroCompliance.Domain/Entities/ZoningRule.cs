namespace AeroCompliance.Domain.Entities;

/// <summary>
/// An extracted zoning/legal rule (e.g. road setback). May be AI-extracted but must be
/// validated by a GIS/legal analyst before use. Maps to <c>zoning_rules</c>.
/// A rule targets either a specific <see cref="Zone"/> or a <see cref="ZoneType"/> (DB CHECK enforces one).
/// </summary>
public class ZoningRule
{
    public Guid Id { get; set; }

    /// <summary>Optional: a rule scoped to a specific zone.</summary>
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    /// <summary>Optional: a rule generic to a zone type when <see cref="ZoneId"/> is null.</summary>
    public string? ZoneType { get; set; }

    /// <summary>e.g. <c>road_setback</c>, <c>neighbor_setback</c>, <c>max_coverage</c>.</summary>
    public string RuleKey { get; set; } = null!;
    public decimal RuleValue { get; set; }

    /// <summary><c>m</c>, <c>m2</c>, or <c>percent</c>.</summary>
    public string Unit { get; set; } = null!;

    public string? SourceDocument { get; set; }
    public bool ExtractedByAi { get; set; } = true;

    /// <summary>A rule must not be applied until validated.</summary>
    public bool IsValidated { get; set; }

    public Guid? ValidatedBy { get; set; }
    public User? Validator { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
