using AeroCompliance.Domain.Entities;
using AeroCompliance.Domain.Enums;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Services;

/// <summary>
/// Seeds one dev test user per role (under the pilot municipality) after migrations. Idempotent:
/// checks by email and only inserts missing users, so it is safe to run on every startup.
///
/// DEV-ONLY: every seeded user shares the password below. Never enable this seeding in production,
/// and never ship these credentials.
/// </summary>
public sealed class DevUserSeeder(
    AeroComplianceDbContext db,
    IPasswordHasherService passwordHasher,
    ILogger<DevUserSeeder> logger)
{
    /// <summary>DEV-ONLY shared password for all seeded users.</summary>
    private const string DevPassword = "ChangeMe123!";

    private const string EmailDomain = "aerocompliance.test";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var created = 0;

        foreach (var role in Enum.GetValues<RoleName>())
        {
            // Email pattern {role}@aerocompliance.test, lowercased (e.g. admin@..., municipalitymanager@...).
            var email = $"{role.ToString().ToLowerInvariant()}@{EmailDomain}";

            if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = $"{role} (seed)",
                Email = email,
                RoleId = (int)role,
                // Admin is platform-wide (no municipality); everyone else sits under the pilot municipality.
                MunicipalityId = role == RoleName.Admin ? null : SeedData.PilotMunicipalityId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // Hash live (PasswordHasher salts per call, so this can't be precomputed in a migration).
            user.PasswordHash = passwordHasher.Hash(user, DevPassword);

            db.Users.Add(user);
            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Seeded {Count} DEV test user(s) with the shared dev password. Do not use in production.",
                created);
        }
    }
}
