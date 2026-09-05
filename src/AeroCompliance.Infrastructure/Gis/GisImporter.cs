using System.Text.Json;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;

namespace AeroCompliance.Infrastructure.Gis;

/// <summary>Per-layer outcome, printed verbatim by the import command.</summary>
public sealed class LayerImportResult
{
    public string SourceFile { get; set; } = "";
    public string Classification { get; set; } = "";
    public string SourceChecksum { get; set; } = "";
    public string SourceCrs { get; set; } = "";
    public string GeometryType { get; set; } = "";
    public double[]? SourceBounds { get; set; }
    public double[]? Wgs84Bounds { get; set; }
    public int FeatureCount { get; set; }
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Duplicates { get; set; }
    public int Repaired { get; set; }
    public int SkippedNull { get; set; }
    public int Rejected { get; set; }
    public string TargetMunicipality { get; set; } = "";
    public string Target { get; set; } = "";
    public List<string> Notes { get; } = [];
}

/// <summary>
/// Development-only, idempotent importer for the supplied municipal GIS layers.
/// </summary>
/// <remarks>
/// Each source layer is classified explicitly rather than by filename convention, and each
/// classification writes to the table that actually represents it — operational parcels into
/// <see cref="Parcel"/>, everything else into <see cref="GisReferenceFeature"/>. Re-running is safe:
/// parcels key on (block, parcel number) and reference features on (source layer, source feature id),
/// so a second run updates in place and reports zero new rows.
/// </remarks>
public sealed class GisImporter(AeroComplianceDbContext db)
{
    private const int Wgs84 = 4326;
    private const int MetricEpsg = 32636;
    private const int BatchSize = 500;

    private static readonly GeometryFactory Wgs84Factory =
        new(new PrecisionModel(), Wgs84);
    private static readonly GeometryFactory MetricFactory =
        new(new PrecisionModel(), MetricEpsg);

    /// <summary>Attributes worth keeping from the source DBF. The rest of the row is discarded.</summary>
    private static readonly string[] KeptAttributes =
    [
        "OBJECTID", "PARCEL_NO", "BLOCK_NO_S", "QUARTER_NO", "BUILD_NO", "ROAD_NM",
        "BLOCK_NA", "QUARTER_NA", "LANDAREA", "PARCEL_ARE",
    ];

    public async Task<List<LayerImportResult>> ImportAsync(
        string sourceDirectory, Guid municipalityId, bool dryRun, TextWriter log, CancellationToken ct)
    {
        var results = new List<LayerImportResult>();
        var municipality = await db.Municipalities.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == municipalityId, ct)
            ?? throw new InvalidOperationException($"Municipality {municipalityId} does not exist.");

        var candidates = Directory.GetFiles(sourceDirectory, "*.shp").OrderBy(x => x).ToList();
        log.WriteLine($"Scanning {sourceDirectory}: {candidates.Count} candidate .shp files");

        // Validate everything first so rejections are reported even when nothing is imported.
        var sources = new List<ShapefileSource>();
        foreach (var path in candidates)
        {
            var (source, rejection) = ShapefileReader.Validate(path);
            if (rejection is not null)
            {
                results.Add(new LayerImportResult
                {
                    SourceFile = Path.GetFileName(path),
                    Classification = "REJECTED",
                    Target = "none",
                    TargetMunicipality = municipality.Name,
                    Rejected = 1,
                    Notes = { rejection.Reason },
                });
                log.WriteLine($"  REJECT {Path.GetFileName(path)}: {rejection.Reason}");
                continue;
            }
            sources.Add(source!);
        }

        foreach (var source in sources)
        {
            var classification = Classify(source.LayerName);
            if (classification is null)
            {
                results.Add(new LayerImportResult
                {
                    SourceFile = Path.GetFileName(source.Path),
                    Classification = "SKIPPED (duplicate of a selected layer, or not a required layer)",
                    Target = "none",
                    TargetMunicipality = municipality.Name,
                    FeatureCount = source.FeatureCount,
                    SourceChecksum = source.Sha256,
                    SourceCrs = $"EPSG:{source.SourceEpsg}",
                    Notes = { SkipReason(source.LayerName) },
                });
                log.WriteLine($"  SKIP   {source.LayerName}: {SkipReason(source.LayerName)}");
                continue;
            }

            log.WriteLine($"  IMPORT {source.LayerName} as {classification} ({source.FeatureCount:N0} features, EPSG:{source.SourceEpsg})");
            var result = classification == "OperationalParcel"
                ? await ImportParcelsAsync(source, municipality, dryRun, log, ct)
                : await ImportReferenceAsync(source, municipality, classification, dryRun, log, ct);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Maps a layer basename to what it actually is. Returns null for a layer that is a duplicate of
    /// a selected one (same data in another CRS) or not part of the required import set.
    /// </summary>
    private static string? Classify(string layer) => layer.ToLowerInvariant() switch
    {
        "parcels_fixed" => "OperationalParcel",
        "building_32636" => GisReferenceTypes.BuildingGroundTruth,
        "parcels_encroachments" => GisReferenceTypes.ParcelEncroachmentReference,
        "road_violations" => GisReferenceTypes.RoadEncroachmentReference,
        _ => null,
    };

    private static string SkipReason(string layer) => layer.ToLowerInvariant() switch
    {
        "building_fixed" =>
            "duplicate of building_32636 (same 6,034 features in EPSG:28191); building_32636 selected",
        "parcels_32636" =>
            "duplicate of parcels_encroachments (verified identical OBJECTIDs and areas, EPSG:32636); parcels_encroachments selected",
        _ => "not part of the required import set",
    };

    // --- Operational parcels ------------------------------------------------------------------

    /// <summary>
    /// Imports parcels_fixed into <see cref="Parcel"/>. The domain requires every parcel to sit under
    /// a Block inside a Zone, so a single import Zone is created and Blocks are derived from the
    /// source's QUARTER_NO cadastral grouping (with one fallback block for parcels lacking it).
    /// </summary>
    private async Task<LayerImportResult> ImportParcelsAsync(
        ShapefileSource source, Municipality municipality, bool dryRun, TextWriter log, CancellationToken ct)
    {
        var result = NewResult(source, "OperationalParcel", $"parcels (via zone/blocks) in {municipality.Name}", municipality);
        var toWgs84 = ShapefileReader.Transform(source.SourceEpsg, Wgs84);
        var toMetric = ShapefileReader.Transform(source.SourceEpsg, MetricEpsg);

        var features = ReadFeatures(source, toWgs84, toMetric, result);
        if (features.Count == 0) return result;

        result.SourceBounds = Bounds(features.Select(f => f.SourceEnvelope));
        result.Wgs84Bounds = Bounds(features.Select(f => f.Geometry.EnvelopeInternal));

        if (dryRun)
        {
            result.Notes.Add($"dry run: {features.Count:N0} features validated, nothing written");
            return result;
        }

        var zone = await EnsureImportZoneAsync(municipality.Id, features, ct);
        var blocks = await EnsureBlocksAsync(zone, features, ct);

        // Existing parcels keyed by (blockId, parcelNumber) — the entity's own uniqueness rule.
        var blockIds = blocks.Values.Select(b => b.Id).ToHashSet();
        var existing = await db.Parcels
            .Where(p => blockIds.Contains(p.BlockId))
            .Select(p => new { p.Id, p.BlockId, p.ParcelNumber })
            .ToListAsync(ct);
        var index = existing.ToDictionary(p => (p.BlockId, p.ParcelNumber), p => p.Id);

        var seen = new HashSet<(Guid, string)>();
        var pending = 0;
        var qualified = 0;
        foreach (var feature in features)
        {
            var quarter = Attr(feature.Attributes, "QUARTER_NO") ?? "UNASSIGNED";
            var block = blocks[quarter];
            var parcelNumber = ParcelNumber(feature.Attributes, feature.SourceFeatureId);
            var key = (block.Id, parcelNumber);

            if (!seen.Add(key))
            {
                // BLOCK_NO_S is a 2-character field in the source DBF, so block+parcel is not
                // unique on its own. Rather than drop the feature, qualify it with the source
                // OBJECTID (which is unique across all 13,436 rows) and keep the readable number
                // for the majority of parcels that do not collide.
                parcelNumber = Trim($"{parcelNumber}#{feature.SourceFeatureId}");
                key = (block.Id, parcelNumber);
                if (!seen.Add(key))
                {
                    result.Duplicates++;
                    continue;
                }
                qualified++;
            }

            if (index.TryGetValue(key, out var parcelId))
            {
                var tracked = await db.Parcels.SingleAsync(p => p.Id == parcelId, ct);
                tracked.Geom = feature.Geometry;
                tracked.AreaM2 = (decimal)Math.Round(feature.AreaM2, 2);
                tracked.ZoneType = null;
                tracked.Source = SourceTag(source);
                result.Updated++;
            }
            else
            {
                db.Parcels.Add(new Parcel
                {
                    Id = Guid.NewGuid(),
                    BlockId = block.Id,
                    ParcelNumber = parcelNumber,
                    OwnerName = null, // The source DBF carries no owner name.
                    ZoneType = null,
                    AreaM2 = (decimal)Math.Round(feature.AreaM2, 2),
                    Geom = feature.Geometry,
                    Source = SourceTag(source),
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                result.Imported++;
            }

            if (++pending >= BatchSize)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                // Re-attach the blocks after clearing so the next batch keeps valid references.
                blocks = await ReloadBlocksAsync(zone.Id, ct);
                pending = 0;
                log.WriteLine($"    ... {result.Imported + result.Updated:N0} parcels processed");
            }
        }
        if (pending > 0) await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        result.Notes.Add($"zone '{zone.ZoneNumber}' + {blocks.Count} block(s) derived from QUARTER_NO");
        if (qualified > 0)
            result.Notes.Add(
                $"{qualified:N0} parcel number(s) qualified with the source OBJECTID because the " +
                "2-character BLOCK_NO_S field makes block+parcel non-unique");
        return result;
    }

    private async Task<Zone> EnsureImportZoneAsync(
        Guid municipalityId, List<ReadFeature> features, CancellationToken ct)
    {
        const string zoneNumber = "RML-GIS-IMPORT";
        var zone = await db.Zones.SingleOrDefaultAsync(
            z => z.MunicipalityId == municipalityId && z.ZoneNumber == zoneNumber, ct);
        var hull = EnvelopeMultiPolygon(features.Select(f => f.Geometry.EnvelopeInternal));

        if (zone is null)
        {
            zone = new Zone
            {
                Id = Guid.NewGuid(),
                MunicipalityId = municipalityId,
                ZoneNumber = zoneNumber,
                Name = "Imported municipal cadastre",
                ZoneType = "cadastral_import",
                Geom = hull,
                SourceDocument = "Imported from the supplied municipal Shapefile set (parcels_fixed).",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Zones.Add(zone);
        }
        else
        {
            zone.Geom = hull;
        }
        await db.SaveChangesAsync(ct);
        return zone;
    }

    private async Task<Dictionary<string, Block>> EnsureBlocksAsync(
        Zone zone, List<ReadFeature> features, CancellationToken ct)
    {
        var byQuarter = features
            .GroupBy(f => Attr(f.Attributes, "QUARTER_NO") ?? "UNASSIGNED")
            .ToDictionary(g => g.Key, g => EnvelopeMultiPolygon(g.Select(f => f.Geometry.EnvelopeInternal)));

        var existing = await db.Blocks.Where(b => b.ZoneId == zone.Id).ToListAsync(ct);
        var index = existing.ToDictionary(b => b.BlockNumber, b => b);

        foreach (var (quarter, geom) in byQuarter)
        {
            var blockNumber = $"Q{quarter}";
            if (index.TryGetValue(blockNumber, out var block))
            {
                block.Geom = geom;
                continue;
            }
            block = new Block
            {
                Id = Guid.NewGuid(),
                ZoneId = zone.Id,
                BlockNumber = blockNumber,
                Geom = geom,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Blocks.Add(block);
            index[blockNumber] = block;
        }
        await db.SaveChangesAsync(ct);

        return byQuarter.Keys.ToDictionary(q => q, q => index[$"Q{q}"]);
    }

    private async Task<Dictionary<string, Block>> ReloadBlocksAsync(Guid zoneId, CancellationToken ct)
    {
        var blocks = await db.Blocks.Where(b => b.ZoneId == zoneId).ToListAsync(ct);
        return blocks.ToDictionary(b => b.BlockNumber[1..], b => b);
    }

    // --- Reference features -------------------------------------------------------------------

    private async Task<LayerImportResult> ImportReferenceAsync(
        ShapefileSource source, Municipality municipality, string referenceType,
        bool dryRun, TextWriter log, CancellationToken ct)
    {
        var result = NewResult(source, referenceType, $"gis_reference_features ({referenceType})", municipality);
        var toWgs84 = ShapefileReader.Transform(source.SourceEpsg, Wgs84);
        var toMetric = ShapefileReader.Transform(source.SourceEpsg, MetricEpsg);

        var features = ReadFeatures(source, toWgs84, toMetric, result);
        if (features.Count == 0) return result;

        result.SourceBounds = Bounds(features.Select(f => f.SourceEnvelope));
        result.Wgs84Bounds = Bounds(features.Select(f => f.Geometry.EnvelopeInternal));

        if (dryRun)
        {
            result.Notes.Add($"dry run: {features.Count:N0} features validated, nothing written");
            return result;
        }

        var existing = await db.GisReferenceFeatures
            .Where(x => x.SourceLayer == source.LayerName)
            .Select(x => new { x.Id, x.SourceFeatureId })
            .ToListAsync(ct);
        var index = existing.ToDictionary(x => x.SourceFeatureId, x => x.Id);

        var seen = new HashSet<string>();
        var pending = 0;
        foreach (var feature in features)
        {
            if (!seen.Add(feature.SourceFeatureId)) { result.Duplicates++; continue; }

            if (index.TryGetValue(feature.SourceFeatureId, out var id))
            {
                var tracked = await db.GisReferenceFeatures.SingleAsync(x => x.Id == id, ct);
                tracked.Geom = feature.Geometry;
                tracked.AreaM2 = feature.AreaM2;
                tracked.MetadataJson = feature.MetadataJson;
                tracked.SourceChecksum = source.Sha256;
                tracked.GeometryRepaired = feature.Repaired;
                tracked.ImportedAt = DateTimeOffset.UtcNow;
                result.Updated++;
            }
            else
            {
                db.GisReferenceFeatures.Add(new GisReferenceFeature
                {
                    MunicipalityId = municipality.Id,
                    ReferenceType = referenceType,
                    SourceLayer = source.LayerName,
                    SourceFeatureId = feature.SourceFeatureId,
                    SourceCrs = $"EPSG:{source.SourceEpsg}",
                    Geom = feature.Geometry,
                    AreaM2 = feature.AreaM2,
                    MetadataJson = feature.MetadataJson,
                    SourceChecksum = source.Sha256,
                    GeometryRepaired = feature.Repaired,
                    ImportedAt = DateTimeOffset.UtcNow,
                });
                result.Imported++;
            }

            if (++pending >= BatchSize)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                pending = 0;
                log.WriteLine($"    ... {result.Imported + result.Updated:N0} features processed");
            }
        }
        if (pending > 0) await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return result;
    }

    // --- Shared reading -----------------------------------------------------------------------

    private sealed record ReadFeature(
        string SourceFeatureId,
        MultiPolygon Geometry,
        double AreaM2,
        string? MetadataJson,
        bool Repaired,
        Envelope SourceEnvelope,
        Dictionary<string, string?> Attributes);

    private static List<ReadFeature> ReadFeatures(
        ShapefileSource source,
        ProjNet.CoordinateSystems.Transformations.MathTransform toWgs84,
        ProjNet.CoordinateSystems.Transformations.MathTransform toMetric,
        LayerImportResult result)
    {
        // Geometry and attributes are read through separate paths on purpose. The .shp is read
        // geometry-only, and the .dbf through TolerantDbfReader, because several supplied DBFs
        // store binary inside numeric fields — a combined strict reader throws on the first such
        // record and would cost the entire layer.
        var geometries = Shapefile.ReadAllGeometries(source.Path);
        var dir = Path.GetDirectoryName(source.Path)!;
        var dbfPath = Path.Combine(dir, source.LayerName + ".dbf");
        var cpgPath = Path.Combine(dir, source.LayerName + ".cpg");

        TolerantDbfReader? dbf = null;
        try
        {
            dbf = new TolerantDbfReader(dbfPath, File.Exists(cpgPath) ? cpgPath : null);
            if (dbf.RecordCount != geometries.Length)
                result.Notes.Add(
                    $"attribute/geometry count mismatch ({dbf.RecordCount:N0} DBF records vs " +
                    $"{geometries.Length:N0} geometries); attributes matched by ordinal where present");
        }
        catch (Exception ex)
        {
            result.Notes.Add($"attributes unavailable ({ex.GetType().Name}); imported with geometry only");
        }

        var features = new List<ReadFeature>(geometries.Length);
        try
        {
            for (var i = 0; i < geometries.Length; i++)
            {
                var geometry = geometries[i];
                if (geometry is null || geometry.IsEmpty)
                {
                    result.SkippedNull++;
                    continue;
                }
                if (result.GeometryType.Length == 0) result.GeometryType = geometry.GeometryType;

                var sourceEnvelope = geometry.EnvelopeInternal.Copy();

                var (wgs84, repaired) = ShapefileReader.ToMultiPolygon(geometry, toWgs84, Wgs84Factory);
                if (wgs84 is null)
                {
                    result.Rejected++;
                    continue;
                }
                if (repaired) result.Repaired++;

                var (metric, _) = ShapefileReader.ToMultiPolygon(geometry, toMetric, MetricFactory);
                var area = metric?.Area ?? 0d;

                var attributes = dbf is not null && i < dbf.RecordCount
                    ? dbf.ReadRecord(i, KeptAttributes)
                    : new Dictionary<string, string?>();

                var featureId = attributes.TryGetValue("OBJECTID", out var oid) && !string.IsNullOrWhiteSpace(oid)
                    ? oid!.Trim()
                    : (i + 1).ToString();

                features.Add(new ReadFeature(
                    featureId, wgs84, area,
                    attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes),
                    repaired, sourceEnvelope, attributes));
            }
        }
        finally
        {
            dbf?.Dispose();
        }

        result.FeatureCount = source.FeatureCount;
        return features;
    }

    private static LayerImportResult NewResult(
        ShapefileSource source, string classification, string target, Municipality municipality) => new()
    {
        SourceFile = Path.GetFileName(source.Path),
        Classification = classification,
        SourceChecksum = source.Sha256,
        SourceCrs = $"EPSG:{source.SourceEpsg} ({source.SourceCrsName})",
        FeatureCount = source.FeatureCount,
        TargetMunicipality = municipality.Name,
        Target = target,
    };

    private static string SourceTag(ShapefileSource source) =>
        $"municipality_gis:{source.LayerName}"[..Math.Min(30, $"municipality_gis:{source.LayerName}".Length)];

    private static string? Attr(Dictionary<string, string?> attributes, string key) =>
        attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string ParcelNumber(Dictionary<string, string?> attributes, string fallback)
    {
        var parcel = Attr(attributes, "PARCEL_NO");
        var block = Attr(attributes, "BLOCK_NO_S");
        // PARCEL_NO alone repeats across blocks, so it is qualified with the block number to give a
        // stable, human-meaningful identifier; the source OBJECTID is the last resort.
        if (parcel is not null && block is not null) return Trim($"{block}-{parcel}");
        if (parcel is not null) return Trim(parcel);
        return Trim($"OID-{fallback}");
    }

    private static string Trim(string value) => value.Length <= 50 ? value : value[..50];

    private static double[]? Bounds(IEnumerable<Envelope> envelopes)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        var any = false;
        foreach (var e in envelopes)
        {
            if (e.IsNull) continue;
            any = true;
            minX = Math.Min(minX, e.MinX); minY = Math.Min(minY, e.MinY);
            maxX = Math.Max(maxX, e.MaxX); maxY = Math.Max(maxY, e.MaxY);
        }
        return any ? [minX, minY, maxX, maxY] : null;
    }

    /// <summary>Axis-aligned envelope of a set of features, as a MultiPolygon — used for the
    /// synthesized import Zone/Block geometry, which has no authoritative source boundary.</summary>
    private static MultiPolygon EnvelopeMultiPolygon(IEnumerable<Envelope> envelopes)
    {
        var b = Bounds(envelopes) ?? [0, 0, 0, 0];
        var ring = Wgs84Factory.CreateLinearRing(
        [
            new Coordinate(b[0], b[1]), new Coordinate(b[2], b[1]),
            new Coordinate(b[2], b[3]), new Coordinate(b[0], b[3]),
            new Coordinate(b[0], b[1]),
        ]);
        var multi = Wgs84Factory.CreateMultiPolygon([Wgs84Factory.CreatePolygon(ring)]);
        multi.SRID = Wgs84;
        return multi;
    }
}
