using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AeroCompliance.Infrastructure;

/// <summary>
/// Registration helpers for the Infrastructure layer. In Epic 1 the Aspire client integration
/// (<c>AddNpgsqlDbContext</c>) configures the connection in the API host; this helper only enables
/// the NetTopologySuite geometry mapping on that DbContext, keeping the PostGIS wiring in one place.
/// </summary>
public static class DependencyInjection
{
    /// <summary>The connection-string / resource name shared with the Aspire AppHost.</summary>
    public const string DatabaseResourceName = "aerocompliancedb";

    /// <summary>
    /// Applies the Npgsql options the <see cref="AeroComplianceDbContext"/> needs (PostGIS via
    /// NetTopologySuite and the migrations assembly). Call this from the host's
    /// <c>AddNpgsqlDbContext&lt;AeroComplianceDbContext&gt;(configureDbContextOptions: ...)</c>.
    /// </summary>
    public static void UseAeroComplianceNpgsql(this DbContextOptionsBuilder options)
    {
        options.UseNpgsql(npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsAssembly(typeof(AeroComplianceDbContext).Assembly.FullName);
        });
    }
}
