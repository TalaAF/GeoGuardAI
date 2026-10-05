using System.Net;
using System.Net.Http.Json;
using AeroCompliance.Contracts.Dtos.Users;
using AeroCompliance.Domain.Enums;
using AeroCompliance.IntegrationTests.Infrastructure;

namespace AeroCompliance.IntegrationTests.Users;

[Collection(ApiTestCollection.Name)]
public class CurrentUserEndpointTests(ApiTestFixture fixture)
{
    [Fact]
    public async Task Municipality_user_receives_safe_profile()
    {
        var user = await fixture.CreateUserAsync(RoleName.Inspector);
        using var client = fixture.CreateClient();
        var tokens = await client.LoginAsync(user);
        client.UseBearer(tokens.AccessToken);

        var response = await client.GetAsync("/api/users/me");
        var json = await response.Content.ReadAsStringAsync();
        var profile = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(user.Id, profile?.Id);
        Assert.Equal(user.Email, profile?.Email);
        Assert.Equal(RoleName.Inspector.ToString(), profile?.Role.Name);
        Assert.NotNull(profile?.Municipality);
        Assert.True(profile?.IsActive);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_receives_null_municipality()
    {
        var user = await fixture.CreateUserAsync(RoleName.Admin);
        using var client = fixture.CreateClient();
        var tokens = await client.LoginAsync(user);
        client.UseBearer(tokens.AccessToken);

        var profile = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/me");

        Assert.Equal(RoleName.Admin.ToString(), profile?.Role.Name);
        Assert.Null(profile?.Municipality);
    }

    [Fact]
    public async Task Token_for_deleted_user_returns_not_found()
    {
        var user = await fixture.CreateUserAsync(RoleName.Inspector);
        using var client = fixture.CreateClient();
        var tokens = await client.LoginAsync(user);
        await fixture.DeleteUserAsync(user.Id);
        client.UseBearer(tokens.AccessToken);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task User_deactivated_after_token_issuance_returns_forbidden()
    {
        var user = await fixture.CreateUserAsync(RoleName.Inspector);
        using var client = fixture.CreateClient();
        var tokens = await client.LoginAsync(user);
        await fixture.SetActiveAsync(user.Id, false);
        client.UseBearer(tokens.AccessToken);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_returns_unauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
