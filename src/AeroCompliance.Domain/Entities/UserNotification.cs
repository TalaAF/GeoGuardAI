namespace AeroCompliance.Domain.Entities;

/// <summary>
/// An in-app notification addressed to one user. Maps to <c>user_notifications</c>.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="Notification"/>, which models outbound external delivery
/// (email/SMS/police) and is not used by this feature. Rows here carry only human-readable text and
/// a server-generated relative route — never payloads, geometry, storage keys, or exception detail.
/// </remarks>
public class UserNotification
{
    public Guid Id { get; set; }

    /// <summary>Tenant the notification belongs to. Always the recipient's own municipality.</summary>
    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;

    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;

    /// <summary>A value from <c>NotificationTypes</c>; never shown to a user as-is.</summary>
    public string NotificationType { get; set; } = null!;

    /// <summary><c>Information</c>, <c>Attention</c>, or <c>Critical</c>.</summary>
    public string Severity { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;

    /// <summary>Entity kind the notification points at, e.g. <c>Violation</c>, for labelling only.</summary>
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    /// <summary>
    /// A relative in-app route built server-side from a known entity type (e.g. <c>/violations/{id}</c>).
    /// Never accepted from a client, and never absolute, so it cannot become an open redirect.
    /// </summary>
    public string? Route { get; set; }

    /// <summary>
    /// Deterministic key for the originating occurrence (e.g. the violation id). Combined with
    /// recipient and type it forms the uniqueness constraint that makes redelivery a no-op.
    /// </summary>
    public string SourceEventKey { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }

    /// <summary>Set by a soft dismissal; the row is never physically deleted.</summary>
    public DateTimeOffset? DismissedAtUtc { get; set; }
}

/// <summary>Per-user in-app notification category switches. Maps to <c>notification_preferences</c>.</summary>
public class NotificationPreference
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public bool CandidateViolations { get; set; } = true;
    public bool ProcessingFailures { get; set; } = true;
    public bool ReviewOutcomes { get; set; } = true;
    public bool GeneratedReports { get; set; } = true;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
