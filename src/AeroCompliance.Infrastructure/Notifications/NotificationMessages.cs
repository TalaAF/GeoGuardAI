namespace AeroCompliance.Infrastructure.Notifications;

/// <summary>
/// Builds the human-readable title and message for each notification type.
/// </summary>
/// <remarks>
/// Every caller passes already-resolved display names (flight mission, file name, road code,
/// reference). Where a name is unavailable the wording degrades to a neutral phrase rather than
/// falling back to an identifier — a raw UUID must never reach a notification.
/// </remarks>
public static class NotificationMessages
{
    /// <summary>Short human reference for a violation, e.g. "ACV-2026-12AB34CD".</summary>
    public static string ViolationReference(Guid violationId, DateTimeOffset createdAt) =>
        $"ACV-{createdAt.ToUniversalTime():yyyy}-{violationId:N}"[..17].ToUpperInvariant();

    public static (string Title, string Message) CandidateViolation(
        string ruleLabel, string? measured, string? required, string? location, string? flightName)
    {
        var issue = ruleLabel.ToLowerInvariant();
        var title = $"New {issue} violation ready for review";
        var where = string.IsNullOrWhiteSpace(location) ? "" : $" at {location}";
        var comparison = measured is not null && required is not null
            ? $" Measured {measured} against a required {required}."
            : "";
        var flight = string.IsNullOrWhiteSpace(flightName) ? "" : $" Detected on flight “{flightName}”.";
        return (title, $"A new {issue} violation{where} is ready for review.{comparison}{flight}");
    }

    public static (string Title, string Message) ImageProcessingFailed(string? fileName, string? flightName)
    {
        var subject = !string.IsNullOrWhiteSpace(fileName) ? $"image “{fileName}”"
            : !string.IsNullOrWhiteSpace(flightName) ? $"an image on flight “{flightName}”"
            : "an uploaded image";
        var onFlight = !string.IsNullOrWhiteSpace(flightName) && !string.IsNullOrWhiteSpace(fileName)
            ? $" on flight “{flightName}”" : "";
        return ("Image processing failed",
            $"Processing failed for {subject}{onFlight}. The image can be retried from the flight's images page.");
    }

    public static (string Title, string Message) ComplianceEvaluationFailed(string? fileName, string? flightName)
    {
        var subject = !string.IsNullOrWhiteSpace(fileName) ? $"image “{fileName}”"
            : !string.IsNullOrWhiteSpace(flightName) ? $"an image on flight “{flightName}”"
            : "a processed image";
        return ("Compliance evaluation could not be completed",
            $"Compliance evaluation could not be completed for {subject}. " +
            "This is a processing problem, not a zoning-rule result, and needs an operational check.");
    }

    public static (string Title, string Message) ViolationDecided(
        bool confirmed, string reference, string? reviewerName, string? location)
    {
        var verb = confirmed ? "confirmed" : "dismissed";
        var by = string.IsNullOrWhiteSpace(reviewerName) ? "" : $" by {reviewerName}";
        var where = string.IsNullOrWhiteSpace(location) ? "" : $" at {location}";
        return ($"Violation {verb}",
            $"Violation {reference}{where} was {verb}{by}.");
    }

    public static (string Title, string Message) ReportGenerated(string reference, string? location) =>
        ("Report ready to download",
         $"Report {reference}{(string.IsNullOrWhiteSpace(location) ? "" : $" for {location}")} is ready to download.");
}
