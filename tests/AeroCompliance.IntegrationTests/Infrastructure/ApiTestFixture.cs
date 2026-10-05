using AeroCompliance.ApiService.Services;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace AeroCompliance.IntegrationTests.Infrastructure;

public sealed class ApiTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .WithDatabase("aerocompliance_tests")
        .WithUsername("postgres")
        .WithPassword("isolated-tests-only-password")
        .Build();

    private AeroComplianceApiFactory? _factory;

    public HttpClient CreateClient()
        => Factory.CreateClient(new() { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Integration tests require a working Docker engine for an isolated PostGIS container. " +
                "No developer database is used.", exception);
        }

        _factory = new AeroComplianceApiFactory(_postgres.GetConnectionString());

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    public async Task<User> CreateUserAsync(
        RoleName role,
        bool isActive = true,
        Guid? id = null,
        string? email = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            FullName = $"{role} Integration User",
            Email = email ?? $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@example.test",
            RoleId = (int)role,
            MunicipalityId = role == RoleName.Admin ? null : SeedData.PilotMunicipalityId,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = hasher.Hash(user, TestPassword);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task SetActiveAsync(Guid userId, bool active)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var user = await db.Users.SingleAsync(value => value.Id == userId);
        user.IsActive = active;
        await db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var user = await db.Users.SingleAsync(value => value.Id == userId);
        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    public async Task<RefreshToken?> FindRefreshTokenAsync(string plaintextToken)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var hash = tokenService.HashRefreshToken(plaintextToken);
        return await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(value => value.TokenHash == hash);
    }

    public async Task AddRefreshTokenAsync(User user, string plaintextToken, DateTimeOffset expiresAt, bool revoked)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(plaintextToken),
            ExpiresAt = expiresAt,
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public const string TestPassword = "ValidPassword123!";

    private AeroComplianceApiFactory Factory
        => _factory ?? throw new InvalidOperationException("The integration fixture has not started.");
}
