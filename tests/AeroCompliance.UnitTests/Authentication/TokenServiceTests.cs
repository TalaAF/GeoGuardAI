using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeroCompliance.ApiService.Authentication;
using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Domain.Entities;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AeroCompliance.UnitTests.Authentication;

public class TokenServiceTests
{
    private const string TestSigningKey = "isolated-test-signing-key-with-at-least-32-bytes";

    [Fact]
    public void Municipality_user_token_contains_expected_claims_and_metadata()
    {
        var userId = Guid.NewGuid();
        var municipalityId = Guid.NewGuid();
        var options = CreateOptions();
        var service = new TokenService(Options.Create(options), new TestHostEnvironment());
        var user = new User
        {
            Id = userId,
            Email = "inspector@example.test",
            MunicipalityId = municipalityId,
            Role = new Role { Id = 3, Name = Roles.Inspector }
        };

        var before = DateTimeOffset.UtcNow;
        var issued = service.IssueTokens(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Equal("inspector@example.test", token.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(Roles.Inspector, token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(municipalityId.ToString(), token.Claims.Single(c => c.Type == AeroComplianceClaimTypes.MunicipalityId).Value);
        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Contains(options.Audience, token.Audiences);
        Assert.InRange(token.ValidTo, before.UtcDateTime.AddMinutes(14), before.UtcDateTime.AddMinutes(16));
    }

    [Fact]
    public void Platform_admin_token_omits_municipality_claim()
    {
        var service = new TokenService(Options.Create(CreateOptions()), new TestHostEnvironment());
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.test",
            MunicipalityId = null,
            Role = new Role { Id = 1, Name = Roles.Admin }
        };

        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.IssueTokens(user).AccessToken);

        Assert.DoesNotContain(token.Claims, c => c.Type == AeroComplianceClaimTypes.MunicipalityId);
    }

    [Fact]
    public void Production_like_environment_rejects_missing_or_development_key()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtOptions().ResolveSigningKey(false));
        Assert.Throws<InvalidOperationException>(() => new JwtOptions
        {
            SigningKey = JwtOptions.DevFallbackSigningKey
        }.ResolveSigningKey(false));
    }

    [Fact]
    public void Development_environment_allows_documented_fallback()
        => Assert.Equal(JwtOptions.DevFallbackSigningKey, new JwtOptions().ResolveSigningKey(true));

    private static JwtOptions CreateOptions() => new()
    {
        Issuer = "AeroCompliance.Tests",
        Audience = "AeroCompliance.Tests.Client",
        SigningKey = TestSigningKey,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "AeroCompliance.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
