using System.Net;

namespace AeroCompliance.Domain.Entities;

/// <summary>
/// An immutable audit-trail entry. Maps to <c>audit_logs</c> (BIGSERIAL PK).
/// Logically append-only: UPDATE/DELETE are revoked at the database-permission level.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>NULL when the actor is the system itself (e.g. AI).</summary>
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    /// <summary>e.g. <c>violation.confirm</c>, <c>rule.validate</c>.</summary>
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public Guid? EntityId { get; set; }

    /// <summary>Prior state as JSONB.</summary>
    public string? BeforeState { get; set; }

    /// <summary>New state as JSONB.</summary>
    public string? AfterState { get; set; }

    /// <summary>Originating IP address (PostgreSQL INET).</summary>
    public IPAddress? IpAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
