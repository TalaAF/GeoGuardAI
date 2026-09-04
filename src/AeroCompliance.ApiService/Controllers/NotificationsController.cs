using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Common;
using AeroCompliance.Contracts.Dtos.Notifications;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Notifications;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>
/// The signed-in user's own in-app notifications.
/// </summary>
/// <remarks>
/// Every query is scoped to the user id taken from the token — there is no recipient parameter, so
/// one user can never read or alter another's notifications regardless of role. Anything not owned
/// by the caller is indistinguishable from a non-existent id (404).
/// </remarks>
[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationsController(AeroComplianceDbContext db, ICurrentUserContext user) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationDto>>> List(
        int page = 1, int pageSize = 20, bool unreadOnly = false, string? type = null, string? severity = null,
        CancellationToken ct = default)
    {
        if (!ApiSupport.IsValidPagination(page, pageSize)) return Problem(statusCode: 400, title: "Invalid pagination");
        if (user.UserId is not Guid me) return Unauthorized();

        var q = Mine(me);
        if (unreadOnly) q = q.Where(x => x.ReadAtUtc == null);
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.NotificationType == type.Trim());
        if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(x => x.Severity == severity.Trim());

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new PagedResponse<NotificationDto>(rows.Select(Map).ToList(), page, pageSize, total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        return Ok(new UnreadCountDto(await Mine(me).CountAsync(x => x.ReadAtUtc == null, ct)));
    }

    /// <summary>Idempotent: marking an already-read notification succeeds and changes nothing.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<NotificationDto>> MarkRead(Guid id, CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        var row = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientUserId == me, ct);
        if (row is null) return NotFound();
        if (row.ReadAtUtc is null)
        {
            row.ReadAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Ok(Map(row));
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<MarkAllReadResultDto>> MarkAllRead(CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        var now = DateTimeOffset.UtcNow;
        var updated = await db.UserNotifications
            .Where(x => x.RecipientUserId == me && x.DismissedAtUtc == null && x.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAtUtc, now), ct);
        return Ok(new MarkAllReadResultDto(updated));
    }

    /// <summary>Soft dismissal — sets the dismissed timestamp; the row is never physically deleted.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        var row = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.RecipientUserId == me, ct);
        if (row is null) return NotFound();
        if (row.DismissedAtUtc is null)
        {
            var now = DateTimeOffset.UtcNow;
            row.DismissedAtUtc = now;
            row.ReadAtUtc ??= now;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    /// <summary>Dismissed rows are excluded everywhere; they remain in the table rather than being deleted.</summary>
    private IQueryable<UserNotification> Mine(Guid me) =>
        db.UserNotifications.AsNoTracking().Where(x => x.RecipientUserId == me && x.DismissedAtUtc == null);

    private static NotificationDto Map(UserNotification x) => new(
        x.Id, x.Title, x.Message, x.Severity, NotificationSeverities.Label(x.Severity),
        x.ReadAtUtc is not null, x.CreatedAtUtc,
        NotificationRoutes.Sanitize(x.Route),
        // The related record's kind, spelled out; never the raw identifier.
        x.RelatedEntityType is null ? null : Humanize(x.RelatedEntityType),
        NotificationRoutes.ActionLabel(x.NotificationType));

    private static string Humanize(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");
}

/// <summary>The signed-in user's own in-app notification category switches.</summary>
[ApiController, Route("api/notification-preferences"), Authorize]
public sealed class NotificationPreferencesController(
    AeroComplianceDbContext db, ICurrentUserContext user, IAuditService audit) : ControllerBase
{
    /// <summary>Oversight roles cannot mute operational failures; the UI locks the toggle to match.</summary>
    private static readonly string[] OversightRoles = [Roles.Admin, Roles.MunicipalityManager];

    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> Get(CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        var p = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == me, ct);
        return Ok(Map(p, Locked()));
    }

    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesDto>> Save(
        SaveNotificationPreferencesRequest r, CancellationToken ct)
    {
        if (user.UserId is not Guid me) return Unauthorized();
        var locked = Locked();
        var p = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == me, ct);
        var before = p is null
            ? null
            : new { p.CandidateViolations, p.ProcessingFailures, p.ReviewOutcomes, p.GeneratedReports };

        if (p is null)
        {
            p = new NotificationPreference { UserId = me };
            db.NotificationPreferences.Add(p);
        }
        p.CandidateViolations = r.CandidateViolations;
        // A locked category is forced back on rather than silently accepted as disabled.
        p.ProcessingFailures = locked.Contains(NotificationCategories.ProcessingFailures) || r.ProcessingFailures;
        p.ReviewOutcomes = r.ReviewOutcomes;
        p.GeneratedReports = r.GeneratedReports;
        p.UpdatedAtUtc = DateTimeOffset.UtcNow;

        audit.Add("notification_preferences.update", "NotificationPreference", me, before,
            new { p.CandidateViolations, p.ProcessingFailures, p.ReviewOutcomes, p.GeneratedReports });
        await db.SaveChangesAsync(ct);
        return Ok(Map(p, locked));
    }

    private IReadOnlyList<string> Locked() =>
        user.Role is not null && OversightRoles.Contains(user.Role)
            ? [NotificationCategories.ProcessingFailures]
            : [];

    private static NotificationPreferencesDto Map(NotificationPreference? p, IReadOnlyList<string> locked) =>
        p is null
            ? new NotificationPreferencesDto(true, true, true, true, locked)
            : new NotificationPreferencesDto(p.CandidateViolations, p.ProcessingFailures, p.ReviewOutcomes,
                p.GeneratedReports, locked);
}
