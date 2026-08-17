using AeroCompliance.Domain.Enums;

namespace AeroCompliance.Infrastructure.Persistence;

/// <summary>
/// Shared, deterministic seed constants used by both the migration <c>HasData</c> seeding
/// (roles + pilot municipality) and the runtime user seeder. Keeping the fixed IDs here means the
/// two mechanisms agree on primary keys / foreign keys.
/// </summary>
public static class SeedData
{
    /// <summary>Fixed ID of the seeded "Pilot Municipality" (stable across environments).</summary>
    public static readonly Guid PilotMunicipalityId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string PilotMunicipalityName = "Pilot Municipality";

    /// <summary>Fixed timestamp used for seeded rows so migrations stay deterministic (no <c>now()</c>).</summary>
    public static readonly DateTimeOffset SeedTimestamp =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The six roles, keyed by the stable <see cref="RoleName"/> integer values.</summary>
    public static readonly IReadOnlyList<(int Id, string Name, string Description)> Roles =
    [
        ((int)RoleName.Admin, RoleName.Admin.ToString(), "Platform-wide administrator."),
        ((int)RoleName.MunicipalityManager, RoleName.MunicipalityManager.ToString(), "Manages a municipality's users and data."),
        ((int)RoleName.Inspector, RoleName.Inspector.ToString(), "Field inspector reviewing violations."),
        ((int)RoleName.GISAnalyst, RoleName.GISAnalyst.ToString(), "Validates zoning rules and GIS layers."),
        ((int)RoleName.Reviewer, RoleName.Reviewer.ToString(), "Reviews and signs off violation reports."),
        ((int)RoleName.DroneOperator, RoleName.DroneOperator.ToString(), "Plans and flies drone missions.")
    ];
}
