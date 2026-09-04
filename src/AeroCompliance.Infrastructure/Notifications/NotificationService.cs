using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.Infrastructure.Notifications;

/// <summary>One notification to stage, already resolved to human-readable text and a safe route.</summary>
public sealed record NotificationRequest(
    Guid MunicipalityId,
    string NotificationType,
    string Severity,
    string Title,
    string Message,
    string SourceEventKey,
    string? RelatedEntityType = null,
    Guid? RelatedEntityId = null,
    string? Route = null);

public interface INotificationService
{
    /// <summary>
    /// Stages notifications for every eligible recipient of <paramref name="request"/>. Rows are
    /// added to the context but not saved, so they commit in the caller's own transaction.
    /// Returns how many were staged.
    /// </summary>
    Task<int> NotifyRolesAsync(NotificationRequest request, IReadOnlyCollection<string> roleNames,
        CancellationToken ct, Guid? alsoNotifyUserId = null, Guid? excludeUserId = null);

    /// <summary>Stages a notification for one specific user, subject to the same eligibility rules.</summary>
    Task<int> NotifyUserAsync(NotificationRequest request, Guid userId, CancellationToken ct);
}

/// <summary>
/// Resolves notification recipients and stages durable rows.
/// </summary>
/// <remarks>
/// Recipients are always looked up fresh from the database and filtered to active users in the
/// notification's own municipality — a user in another tenant can never be selected. Nothing is
/// saved here: the caller owns the transaction, so a notification cannot outlive a business
/// operation that was rolled back.
/// </remarks>
public sealed class NotificationService(AeroComplianceDbContext db) : INotificationService
{
    /// <summary>
    /// Categories an Admin or MunicipalityManager cannot switch off, because doing so would hide
    /// operational failures from the only people able to act on them.
    /// </summary>
    private static readonly string[] NonSuppressibleForOversight = [NotificationCategories.ProcessingFailures];
    private static readonly string[] OversightRoles = ["Admin", "MunicipalityManager"];
    private const string PlatformAdminRole = "Admin";

    /// <summary>
    /// Who may receive a notification raised for <paramref name="municipalityId"/>: a user assigned
    /// to that municipality, or a platform-wide Admin (one with no municipality of their own), who
    /// oversees every tenant and would otherwise be silently excluded from all notifications. A
    /// user assigned to a *different* municipality never matches.
    /// </summary>
    /// <remarks>
    /// Returned as an expression rather than a method so EF can translate it into the SQL WHERE
    /// clause; a plain helper method here would throw at query time.
    /// </remarks>
    private static System.Linq.Expressions.Expression<Func<User, bool>> InScope(Guid municipalityId) =>
        u => u.MunicipalityId == municipalityId ||
             (u.MunicipalityId == null && u.Role.Name == PlatformAdminRole);

    public async Task<int> NotifyRolesAsync(NotificationRequest request, IReadOnlyCollection<string> roleNames,
        CancellationToken ct, Guid? alsoNotifyUserId = null, Guid? excludeUserId = null)
    {
        var candidates = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && roleNames.Contains(u.Role.Name))
            .Where(InScope(request.MunicipalityId))
            .Select(u => new { u.Id, RoleName = u.Role.Name })
            .ToListAsync(ct);

        // A specifically named recipient (e.g. the flight's operator) joins the role-derived set,
        // but only if they are active and in the same municipality — otherwise they are skipped
        // rather than guessed at.
        if (alsoNotifyUserId is Guid extra && candidates.All(c => c.Id != extra))
        {
            var named = await db.Users.AsNoTracking()
                .Where(u => u.Id == extra && u.IsActive)
                .Where(InScope(request.MunicipalityId))
                .Select(u => new { u.Id, RoleName = u.Role.Name })
                .SingleOrDefaultAsync(ct);
            if (named is not null) candidates.Add(named);
        }

        var staged = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.Id == excludeUserId) continue;
            if (await StageAsync(request, candidate.Id, candidate.RoleName, ct)) staged++;
        }
        return staged;
    }

    public async Task<int> NotifyUserAsync(NotificationRequest request, Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Where(InScope(request.MunicipalityId))
            .Select(u => new { u.Id, RoleName = u.Role.Name })
            .SingleOrDefaultAsync(ct);
        if (user is null) return 0;
        return await StageAsync(request, user.Id, user.RoleName, ct) ? 1 : 0;
    }

    private async Task<bool> StageAsync(NotificationRequest request, Guid userId, string roleName, CancellationToken ct)
    {
        if (!await IsEnabledAsync(request.NotificationType, userId, roleName, ct)) return false;

        // Idempotency is enforced by a unique index; this check keeps a redelivery from turning
        // into a failed transaction, and also covers rows staged earlier in this same unit of work.
        var alreadyPersisted = await db.UserNotifications.AsNoTracking().AnyAsync(x =>
            x.RecipientUserId == userId &&
            x.NotificationType == request.NotificationType &&
            x.SourceEventKey == request.SourceEventKey, ct);
        if (alreadyPersisted) return false;

        var alreadyStaged = db.ChangeTracker.Entries<UserNotification>().Any(e =>
            e.State == EntityState.Added &&
            e.Entity.RecipientUserId == userId &&
            e.Entity.NotificationType == request.NotificationType &&
            e.Entity.SourceEventKey == request.SourceEventKey);
        if (alreadyStaged) return false;

        db.UserNotifications.Add(new UserNotification
        {
            Id = Guid.NewGuid(),
            MunicipalityId = request.MunicipalityId,
            RecipientUserId = userId,
            NotificationType = request.NotificationType,
            Severity = request.Severity,
            Title = request.Title,
            Message = request.Message,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            Route = NotificationRoutes.Sanitize(request.Route),
            SourceEventKey = request.SourceEventKey,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        return true;
    }

    /// <summary>
    /// Applies the user's category preferences. Absent preferences mean "everything on". Oversight
    /// roles cannot suppress operational failure notices.
    /// </summary>
    private async Task<bool> IsEnabledAsync(string notificationType, Guid userId, string roleName, CancellationToken ct)
    {
        var category = NotificationCategories.For(notificationType);
        if (OversightRoles.Contains(roleName) && NonSuppressibleForOversight.Contains(category)) return true;

        var preference = await db.NotificationPreferences.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (preference is null) return true;

        return category switch
        {
            NotificationCategories.CandidateViolations => preference.CandidateViolations,
            NotificationCategories.ProcessingFailures => preference.ProcessingFailures,
            NotificationCategories.ReviewOutcomes => preference.ReviewOutcomes,
            NotificationCategories.GeneratedReports => preference.GeneratedReports,
            _ => true
        };
    }
}
