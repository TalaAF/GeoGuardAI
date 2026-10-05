using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;

namespace AeroCompliance.Infrastructure.Gis;

/// <summary>One detection compared against one imported reference feature.</summary>
public sealed record ReferenceMatch(
    string ReferenceType,
    string SourceLayer,
    string SourceFeatureId,
    double IntersectionAreaM2,
    double UnionAreaM2,
    double Iou,
    double ReferenceAreaM2,
    string Interpretation);

/// <summary>Everything the comparison found for one detection.</summary>
public sealed record DetectionReferenceReport(
    Guid DetectionId,
    bool HasGeographicGeometry,
    double DetectionAreaM2,
    IReadOnlyList<ReferenceMatch> BuildingGroundTruthMatches,
    IReadOnlyList<ReferenceMatch> ParcelEncroachmentMatches,
    IReadOnlyList<ReferenceMatch> RoadEncroachmentMatches,
    string Summary);

/// <summary>
/// Compares AI detections against the imported reference layers.
/// </summary>
/// <remarks>
/// This measures agreement; it never creates a finding. In particular:
/// <list type="bullet">
/// <item>A building ground-truth match means the model found a building the municipality already
/// has on file — that is a quality signal, not a violation.</item>
/// <item>A road-encroachment match means the detection overlaps a polygon from a *previous*
/// encroachment analysis. The source data contains no road network, so this can never be read as
/// "encroaches on a road" in general — only "overlaps a known, previously-identified area".</item>
/// </list>
/// All measurement happens in EPSG:32636 inside PostGIS, never on EPSG:4326 degrees.
/// </remarks>
public sealed class GisReferenceMatcher(AeroComplianceDbContext db)
{
    /// <summary>Minimum IoU for a detection to count as matching a ground-truth building.</summary>
    public const double DefaultBuildingIouThreshold = 0.30;

    /// <summary>Minimum overlap (m²) before a reference overlap is worth reporting at all.</summary>
    public const double MinimumOverlapM2 = 1.0;

    public async Task<DetectionReferenceReport> CompareAsync(
        Guid detectionId, Guid municipalityId, double buildingIouThreshold, CancellationToken ct)
    {
        var detection = await db.Detections.AsNoTracking()
            .Where(d => d.Id == detectionId)
            .Select(d => new { d.Id, d.Geom })
            .SingleOrDefaultAsync(ct);

        if (detection?.Geom is null || detection.Geom.IsEmpty)
        {
            return new DetectionReferenceReport(detectionId, false, 0, [], [], [],
                "This detection has no geographic geometry, so it cannot be compared with the " +
                "imported municipal reference layers.");
        }

        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);

        var detectionArea = await ScalarAsync(conn,
            "SELECT ST_Area(ST_Transform(ST_MakeValid(@g), 32636))", detection.Geom, null, null, ct) ?? 0d;

        var buildings = await MatchAsync(conn, detection.Geom, municipalityId,
            GisReferenceTypes.BuildingGroundTruth, ct);
        var parcels = await MatchAsync(conn, detection.Geom, municipalityId,
            GisReferenceTypes.ParcelEncroachmentReference, ct);
        var roads = await MatchAsync(conn, detection.Geom, municipalityId,
            GisReferenceTypes.RoadEncroachmentReference, ct);

        var buildingMatches = buildings
            .Select(m => m with
            {
                Interpretation = m.Iou >= buildingIouThreshold
                    ? "MatchedGroundTruthBuilding"
                    : "OverlapsGroundTruthBuildingBelowThreshold",
            })
            .ToList();
        var parcelMatches = parcels
            .Select(m => m with { Interpretation = "MatchedKnownParcelEncroachment" })
            .ToList();
        var roadMatches = roads
            .Select(m => m with { Interpretation = "ReferenceRoadEncroachmentMatch" })
            .ToList();

        var confirmed = buildingMatches.Count(m => m.Interpretation == "MatchedGroundTruthBuilding");
        var summary = confirmed > 0
            ? $"Matches {confirmed} municipal building record(s) at IoU >= {buildingIouThreshold:0.00}."
            : buildingMatches.Count > 0
                ? "Overlaps municipal building records but below the IoU match threshold — " +
                  "an unmatched prediction for model-quality purposes."
                : "No municipal building record overlaps this detection (unmatched prediction).";

        if (parcelMatches.Count > 0)
            summary += $" Overlaps {parcelMatches.Count} previously-recorded parcel-encroachment area(s).";
        if (roadMatches.Count > 0)
            summary += $" Overlaps {roadMatches.Count} previously-recorded road-encroachment area(s) — " +
                       "a match against known reference areas, not a complete road-network check.";

        return new DetectionReferenceReport(
            detectionId, true, Math.Round(detectionArea, 2),
            buildingMatches, parcelMatches, roadMatches, summary);
    }

    private static async Task<List<ReferenceMatch>> MatchAsync(
        NpgsqlConnection conn, MultiPolygon geom, Guid municipalityId, string referenceType, CancellationToken ct)
    {
        // Intersection, union and IoU are all computed in EPSG:32636 so the areas are real metres.
        // ST_MakeValid guards against a self-intersecting source polygon aborting the whole query.
        const string sql = """
            SELECT r.reference_type, r.source_layer, r.source_feature_id,
                   ST_Area(ST_Intersection(ST_Transform(ST_MakeValid(r.geom), 32636),
                                           ST_Transform(ST_MakeValid(@g), 32636)))  AS intersection_m2,
                   ST_Area(ST_Union(ST_Transform(ST_MakeValid(r.geom), 32636),
                                    ST_Transform(ST_MakeValid(@g), 32636)))         AS union_m2,
                   ST_Area(ST_Transform(ST_MakeValid(r.geom), 32636))               AS reference_m2
            FROM gis_reference_features r
            WHERE r.municipality_id = @m
              AND r.reference_type  = @t
              AND ST_Intersects(r.geom, ST_MakeValid(@g))
            ORDER BY intersection_m2 DESC
            LIMIT 25
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("g", NpgsqlDbType.Geometry, geom);
        cmd.Parameters.AddWithValue("m", municipalityId);
        cmd.Parameters.AddWithValue("t", referenceType);

        var matches = new List<ReferenceMatch>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var intersection = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);
            if (intersection < MinimumOverlapM2) continue;
            var union = reader.IsDBNull(4) ? 0d : reader.GetDouble(4);
            var referenceArea = reader.IsDBNull(5) ? 0d : reader.GetDouble(5);
            matches.Add(new ReferenceMatch(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                Math.Round(intersection, 2), Math.Round(union, 2),
                union > 0 ? Math.Round(intersection / union, 4) : 0d,
                Math.Round(referenceArea, 2),
                Interpretation: ""));
        }
        return matches;
    }

    private static async Task<double?> ScalarAsync(
        NpgsqlConnection conn, string sql, MultiPolygon geom, Guid? municipalityId, string? type, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("g", NpgsqlDbType.Geometry, geom);
        if (municipalityId.HasValue) cmd.Parameters.AddWithValue("m", municipalityId.Value);
        if (type is not null) cmd.Parameters.AddWithValue("t", type);
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToDouble(value);
    }
}
