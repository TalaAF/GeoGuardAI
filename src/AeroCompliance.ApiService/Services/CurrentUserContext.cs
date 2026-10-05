using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeroCompliance.ApiService.Authentication;
using AeroCompliance.ApiService.Authorization;

namespace AeroCompliance.ApiService.Services;

/// <summary>Safely reads the current request's validated claims.</summary>
public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuidClaim(JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier);

    public string? Email => FindClaimValue(JwtRegisteredClaimNames.Email, ClaimTypes.Email);

    public string? Role => FindClaimValue(ClaimTypes.Role, "role");

    public Guid? MunicipalityId => ParseGuidClaim(AeroComplianceClaimTypes.MunicipalityId);

    public bool IsPlatformAdmin => string.Equals(Role, Roles.Admin, StringComparison.Ordinal);

    private Guid? ParseGuidClaim(params string[] claimTypes)
        => Guid.TryParse(FindClaimValue(claimTypes), out var value) ? value : null;

    private string? FindClaimValue(params string[] claimTypes)
    {
        var principal = Principal;
        if (principal is null)
        {
            return null;
        }

        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
