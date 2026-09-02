using System.Security.Claims;
using AeroCompliance.ApiService.Authentication;
using AeroCompliance.ApiService.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AeroCompliance.UnitTests.Authorization;

public class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(Policies.RequireAdmin, Roles.Admin, true)]
    [InlineData(Policies.RequireAdmin, Roles.Inspector, false)]
    [InlineData(Policies.ManageUsers, Roles.Admin, true)]
    [InlineData(Policies.ManageUsers, Roles.MunicipalityManager, true)]
    [InlineData(Policies.ManageUsers, Roles.DroneOperator, false)]
    [InlineData(Policies.ReviewViolations, Roles.Inspector, true)]
    [InlineData(Policies.ReviewViolations, Roles.Reviewer, true)]
    [InlineData(Policies.OperateDrone, Roles.DroneOperator, true)]
    [InlineData(Policies.AnalyzeGis, Roles.GISAnalyst, true)]
    public async Task Role_policies_keep_expected_behavior(string policy, string role, bool expected)
    {
        var authorization = CreateAuthorizationService();
        var principal = Principal(new Claim(ClaimTypes.Role, role));

        var result = await authorization.AuthorizeAsync(principal, null, policy);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task Municipality_claim_does_not_grant_missing_role()
    {
        var authorization = CreateAuthorizationService();
        var principal = Principal(new Claim(AeroComplianceClaimTypes.MunicipalityId, Guid.NewGuid().ToString()));

        var result = await authorization.AuthorizeAsync(principal, null, Policies.ReviewViolations);

        Assert.False(result.Succeeded);
    }

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAeroComplianceAuthorization();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));
}
