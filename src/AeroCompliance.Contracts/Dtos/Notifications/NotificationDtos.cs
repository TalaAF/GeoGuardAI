namespace AeroCompliance.Contracts.Dtos.Notifications;

/// <summary>
/// A notification as shown to its recipient. Carries no payload, geometry, storage key, or
/// identifier intended for display — <see cref="RelatedLabel"/> is the human reference.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Severity,
    string SeverityLabel,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    string? Route,
    string? RelatedLabel,
    string ActionLabel);

public sealed record UnreadCountDto(int UnreadCount);

public sealed record MarkAllReadResultDto(int Updated);

/// <summary>Per-user in-app category switches.</summary>
public sealed record NotificationPreferencesDto(
    bool CandidateViolations,
    bool ProcessingFailures,
    bool ReviewOutcomes,
    bool GeneratedReports,
    /// <summary>Categories this user is not permitted to disable, with the reason.</summary>
    IReadOnlyList<string> LockedCategories);

public sealed class SaveNotificationPreferencesRequest
{
    public bool CandidateViolations { get; init; } = true;
    public bool ProcessingFailures { get; init; } = true;
    public bool ReviewOutcomes { get; init; } = true;
    public bool GeneratedReports { get; init; } = true;
}
