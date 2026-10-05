namespace AeroCompliance.ApiService.Services;

/// <summary>
/// Request-scoped authenticated identity used by application services. Future tenant-aware queries
/// must take municipality identity from this context, never from an untrusted request value.
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    Guid? MunicipalityId { get; }
    bool IsPlatformAdmin { get; }
}
