using System.Net;
using System.Text.Json;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;

namespace AeroCompliance.ApiService.Services;

public interface IAuditService
{
    void Add(string action, string entityType, Guid? entityId, object? before = null, object? after = null, Guid? municipalityId = null);
}

public sealed class AuditService(
    AeroComplianceDbContext db,
    ICurrentUserContext currentUser,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Add(string action, string entityType, Guid? entityId, object? before = null, object? after = null, Guid? municipalityId = null)
    {
        IPAddress? ip = null;
        var rawIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (rawIp is not null) ip = rawIp.IsIPv4MappedToIPv6 ? rawIp.MapToIPv4() : rawIp;

        db.AuditLogs.Add(new AuditLog
        {
            ActorId = currentUser.UserId,
            MunicipalityId = municipalityId ?? currentUser.MunicipalityId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            BeforeState = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterState = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            IpAddress = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
