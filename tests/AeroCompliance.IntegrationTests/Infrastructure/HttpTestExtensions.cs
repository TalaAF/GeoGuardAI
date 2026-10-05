using System.Net.Http.Headers;
using System.Net.Http.Json;
using AeroCompliance.Contracts.Dtos.Auth;
using AeroCompliance.Domain.Entities;

namespace AeroCompliance.IntegrationTests.Infrastructure;

public static class HttpTestExtensions
{
    public static async Task<TokenResponse> LoginAsync(this HttpClient client, User user)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(user.Email, ApiTestFixture.TestPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public static void UseBearer(this HttpClient client, string accessToken)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
}
