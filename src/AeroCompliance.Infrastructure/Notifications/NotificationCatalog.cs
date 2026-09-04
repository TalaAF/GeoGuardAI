namespace AeroCompliance.Infrastructure.Notifications;

/// <summary>Explicit notification types. Stored verbatim; never rendered to a user as-is.</summary>
public static class NotificationTypes
{
    public const string CandidateViolationCreated = "CandidateViolationCreated";
    public const string ImageProcessingFailed = "ImageProcessingFailed";
    public const string ComplianceEvaluationFailed = "ComplianceEvaluationFailed";
    public const string ReportGenerated = "ReportGenerated";
    public const string ViolationConfirmed = "ViolationConfirmed";
    public const string ViolationDismissed = "ViolationDismissed";
}

public static class NotificationSeverities
{
    public const string Information = "Information";
    public const string Attention = "Attention";
    public const string Critical = "Critical";

    /// <summary>Display text for the severity chip.</summary>
    public static string Label(string severity) => severity switch
    {
        Information => "Information",
        Attention => "Needs attention",
        Critical => "Critical",
        _ => severity
    };
}

/// <summary>
/// The preference category a notification type belongs to, and whether a user may switch it off.
/// </summary>
public static class NotificationCategories
{
    public const string CandidateViolations = "CandidateViolations";
    public const string ProcessingFailures = "ProcessingFailures";
    public const string ReviewOutcomes = "ReviewOutcomes";
    public const string GeneratedReports = "GeneratedReports";

    public static string For(string notificationType) => notificationType switch
    {
        NotificationTypes.CandidateViolationCreated => CandidateViolations,
        NotificationTypes.ImageProcessingFailed or NotificationTypes.ComplianceEvaluationFailed => ProcessingFailures,
        NotificationTypes.ViolationConfirmed or NotificationTypes.ViolationDismissed => ReviewOutcomes,
        NotificationTypes.ReportGenerated => GeneratedReports,
        _ => CandidateViolations
    };
}

/// <summary>
/// Builds the relative in-app routes a notification may point at. The set is closed: a route is
/// only ever produced from a known entity type plus an id this service already holds, so a client
/// can never influence the destination and an external URL can never be stored.
/// </summary>
public static class NotificationRoutes
{
    public static string Violation(Guid id) => $"/violations/{id}";
    // NOTE: the frontend has no standalone detection-detail route today (Detections.tsx is a
    // single list page with an inline detail panel, selected by row click, not by URL). This
    // route is kept for the day a real /detections/{id} page exists; until then the frontend's
    // notification bell recognises it does not match a real route and omits the action button
    // rather than linking to a dead page.
    public static string Detection(Guid id) => $"/detections/{id}";

    // The frontend shows a flight's images inline on the flight detail page — there is no
    // separate "/images" sub-route — so this must point at the flight itself.
    public static string FlightImages(Guid flightId) => $"/flights/{flightId}";
    public static string Report(Guid id) => $"/reports/{id}";

    /// <summary>
    /// Defence in depth for anything read back out of the database: only a single-slash-prefixed
    /// relative path is ever handed to the frontend.
    /// </summary>
    public static string? Sanitize(string? route) =>
        string.IsNullOrWhiteSpace(route) || !route.StartsWith('/') || route.StartsWith("//")
            ? null
            : route;

    /// <summary>The button text shown on the notification card.</summary>
    public static string ActionLabel(string notificationType) => notificationType switch
    {
        NotificationTypes.CandidateViolationCreated => "Review violation",
        NotificationTypes.ViolationConfirmed or NotificationTypes.ViolationDismissed => "View violation",
        NotificationTypes.ReportGenerated => "Open report",
        NotificationTypes.ImageProcessingFailed => "View flight images",
        NotificationTypes.ComplianceEvaluationFailed => "View detection",
        _ => "Open"
    };
}
