using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeroCompliance.ApiService.Authentication;
using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using Microsoft.AspNetCore.Http;

namespace AeroCompliance.UnitTests.Authentication;

public class CurrentUserContextTests
{
    [Fact]
    public void Municipality_user_exposes_valid_identity_and_tenant()
    {
        var userId = Guid.NewGuid();
        var municipalityId = Guid.NewGuid();
        var context = CreateContext(
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, "inspector@example.test"),
            new(ClaimTypes.Role, Roles.Inspector),
            new(AeroComplianceClaimTypes.MunicipalityId, municipalityId.ToString()));

        Assert.True(context.IsAuthenticated);
        Assert.Equal(userId, context.UserId);
        Assert.Equal("inspector@example.test", context.Email);
        Assert.Equal(Roles.Inspector, context.Role);
        Assert.Equal(municipalityId, context.MunicipalityId);
        Assert.False(context.IsPlatformAdmin);
    }

    [Fact]
    public void Platform_admin_does_not_require_municipality()
    {
        var context = CreateContext(
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, Roles.Admin));

        Assert.True(context.IsPlatformAdmin);
        Assert.Null(context.MunicipalityId);
    }

    [Fact]
    public void Missing_claims_are_returned_as_null()
    {
        var context = CreateContext();

        Assert.True(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.Email);
        Assert.Null(context.Role);
        Assert.Null(context.MunicipalityId);
    }

    [Fact]
    public void Malformed_guid_claims_do_not_throw()
    {
        var context = CreateContext(
            new(JwtRegisteredClaimNames.Sub, "not-a-guid"),
            new(AeroComplianceClaimTypes.MunicipalityId, "also-not-a-guid"));

        Assert.Null(context.UserId);
        Assert.Null(context.MunicipalityId);
    }

    [Fact]
    public void Unauthenticated_request_has_no_authenticated_identity()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var context = new CurrentUserContext(accessor);

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
        Assert.Null(context.MunicipalityId);
        Assert.False(context.IsPlatformAdmin);
    }

    private static CurrentUserContext CreateContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new CurrentUserContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
