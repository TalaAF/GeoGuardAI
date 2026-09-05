using AeroCompliance.Infrastructure.Gis;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Services;

/// <summary>
/// Development-only CLI entry point for the GIS importer.
/// </summary>
/// <remarks>
/// Invoked as <c>dotnet run --project src/AeroCompliance.ApiService -- import-gis &lt;dir&gt;
/// [--municipality &lt;guid&gt;] [--dry-run]</c>. It runs instead of the web host, so it never starts
/// listening and never runs as part of normal service startup. Refuses to run outside Development.
/// </remarks>
public static class GisImportCommand
{
    public const string Verb = "import-gis";

    public static bool Matches(string[] args) =>
        args.Length > 0 && string.Equals(args[0], Verb, StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            Console.Error.WriteLine("import-gis is a development-only tool and will not run in this environment.");
            return 2;
        }

        var directory = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : null;
        if (directory is null || !Directory.Exists(directory))
        {
            Console.Error.WriteLine($"Usage: {Verb} <source-gis-directory> [--municipality <guid>] [--dry-run]");
            Console.Error.WriteLine($"The source directory was missing or does not exist: {directory ?? "(none)"}");
            return 2;
        }

        var dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var municipalityId = SeedData.PilotMunicipalityId;
        var flagIndex = Array.FindIndex(args, a => string.Equals(a, "--municipality", StringComparison.OrdinalIgnoreCase));
        if (flagIndex >= 0)
        {
            if (flagIndex + 1 >= args.Length || !Guid.TryParse(args[flagIndex + 1], out municipalityId))
            {
                Console.Error.WriteLine("--municipality requires a GUID.");
                return 2;
            }
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        await db.Database.MigrateAsync();

        var log = Console.Out;
        log.WriteLine($"GIS import{(dryRun ? " (DRY RUN — nothing will be written)" : "")}");
        log.WriteLine($"  source       : {directory}");
        log.WriteLine($"  municipality : {municipalityId}");
        log.WriteLine();

        var importer = new GisImporter(db);
        var results = await importer.ImportAsync(directory, municipalityId, dryRun, log, CancellationToken.None);

        log.WriteLine();
        log.WriteLine(new string('=', 100));
        log.WriteLine("PER-LAYER IMPORT REPORT");
        log.WriteLine(new string('=', 100));
        foreach (var r in results)
        {
            log.WriteLine();
            log.WriteLine($"source filename    : {r.SourceFile}");
            log.WriteLine($"classification     : {r.Classification}");
            log.WriteLine($"source checksum    : {r.SourceChecksum}");
            log.WriteLine($"source CRS         : {r.SourceCrs}");
            log.WriteLine($"geometry type      : {r.GeometryType}");
            log.WriteLine($"source bounds      : {Fmt(r.SourceBounds)}");
            log.WriteLine($"EPSG:4326 bounds   : {Fmt(r.Wgs84Bounds)}");
            log.WriteLine($"feature count      : {r.FeatureCount:N0}");
            log.WriteLine($"imported           : {r.Imported:N0}");
            log.WriteLine($"updated            : {r.Updated:N0}");
            log.WriteLine($"duplicates         : {r.Duplicates:N0}");
            log.WriteLine($"repaired geometry  : {r.Repaired:N0}");
            log.WriteLine($"skipped/null       : {r.SkippedNull:N0}");
            log.WriteLine($"rejected           : {r.Rejected:N0}");
            log.WriteLine($"target municipality: {r.TargetMunicipality}");
            log.WriteLine($"target             : {r.Target}");
            foreach (var note in r.Notes) log.WriteLine($"note               : {note}");
        }

        log.WriteLine();
        log.WriteLine("Totals now in the database:");
        log.WriteLine($"  parcels                      : {await db.Parcels.CountAsync():N0}");
        foreach (var type in AeroCompliance.Domain.Entities.GisReferenceTypes.All)
        {
            var count = await db.GisReferenceFeatures.CountAsync(x => x.ReferenceType == type);
            log.WriteLine($"  {type,-29}: {count:N0}");
        }
        return 0;
    }

    private static string Fmt(double[]? bounds) =>
        bounds is null ? "(none)" : $"[{bounds[0]:F4}, {bounds[1]:F4}, {bounds[2]:F4}, {bounds[3]:F4}]";
}
