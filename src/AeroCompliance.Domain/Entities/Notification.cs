using AeroCompliance.Domain.Enums;

namespace AeroCompliance.Domain.Entities;

/// <summary>An outbound notification tied to a violation. Maps to <c>notifications</c>.</summary>
public class Notification
{
    public Guid Id { get; set; }

    public Guid? ViolationId { get; set; }
    public Violation? Violation { get; set; }

    /// <summary><c>municipality_user</c>, <c>property_owner</c>, <c>police</c>.</summary>
    public string RecipientType { get; set; } = null!;

    /// <summary>A user id or free-form contact reference.</summary>
    public string? RecipientRef { get; set; }

    /// <summary><c>email</c>, <c>sms</c>, <c>api</c>, <c>dashboard</c>.</summary>
    public string Channel { get; set; } = null!;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
