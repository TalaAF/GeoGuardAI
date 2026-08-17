using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AeroCompliance.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> so migrations can be created/applied without booting
/// the Aspire host. The connection string here is a local placeholder — it is only used to build the
/// model and is never used at runtime (the API resolves the real connection from Aspire).
/// </summary>
public class AeroComplianceDbContextFactory : IDesignTimeDbContextFactory<AeroComplianceDbContext>
{
    public AeroComplianceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AEROCOMPLIANCE_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=aerocompliancedb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AeroComplianceDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(AeroComplianceDbContext).Assembly.FullName);
            })
            .Options;

        return new AeroComplianceDbContext(options);
    }
}
