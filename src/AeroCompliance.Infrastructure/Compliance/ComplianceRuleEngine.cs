using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using Npgsql;
using NpgsqlTypes;

namespace AeroCompliance.Infrastructure.Compliance;

/// <summary>What one rule evaluation produced, for reporting back to a caller.</summary>
public sealed record RuleEvaluationResult(
    string CanonicalKey,
    string RuleLabel,
    Guid? ZoningRuleId,
    string Outcome,
    decimal? MeasuredValue,
    decimal? RequiredValue,
    string Unit,
    string Explanation,
    bool CreatedViolation,
    Guid? ViolationId);

/// <summary>The spatial context resolved once per detection and shared by every rule.</summary>
public sealed record DetectionContext(
    Guid? ParcelId, string? ParcelNumber, decimal? ParcelAreaM2,
    Guid? BlockId, string? BlockNumber,
    Guid? ZoneId, string? ZoneNumber, string? ZoneType,
    Guid? NearestRoadId, string? NearestRoadCode, string? NearestRoadName, double? NearestRoadDistanceM);

/// <summary>
/// The deterministic zoning-rule engine. Given a detection it resolves the surrounding parcel,
/// block and zone, picks the applicable rules, measures each one in EPSG:32636, and records a
/// <see cref="ComplianceEvaluation"/> — creating a candidate violation only where a rule fails.
/// </summary>
/// <remarks>
/// Shared by the processing pipeline and the manual re-evaluation endpoint so both paths produce
/// identical results. Every measurement runs in PostGIS against the authoritative geometry; the
/// caller never supplies a measured value.
/// </remarks>
public static class ComplianceRuleEngine
{
    /// <summary>Only these detection classes represent built structures worth evaluating.</summary>
    private static readonly string[] EvaluableClasses = ["building", "violation"];

    public static bool IsEvaluableClass(string? classLabel) =>
        classLabel is not null && EvaluableClasses.Contains(classLabel.Trim().ToLowerInvariant());

    /// <summary>
    /// Evaluates every applicable rule for one detection. Caller is responsible for saving; this
    /// method stages entities on the context and returns what it decided.
    /// </summary>
    public static async Task<IReadOnlyList<RuleEvaluationResult>> EvaluateAsync(
        AeroComplianceDbContext db, Guid municipalityId, Detection detection, Guid? imageAnalysisId,
        CancellationToken ct)
    {
        var results = new List<RuleEvaluationResult>();
        if (!IsEvaluableClass(detection.ClassLabel) || detection.Geom is null || detection.Geom.IsEmpty)
            return results;

        var context = await ResolveContextAsync(db, municipalityId, detection.Geom, ct);
        var rules = await ApplicableRulesAsync(db, municipalityId, context, ct);

        foreach (var (definition, rule) in rules)
        {
            var existing = await db.ComplianceEvaluations
                .Include(x => x.Violation)
                .FirstOrDefaultAsync(x => x.DetectionId == detection.Id && x.EvaluationType == definition.CanonicalKey, ct);

            var measurement = await MeasureAsync(db, definition, detection.Geom, context, ct);

            // The required value is copied onto the evaluation at measurement time and never
            // rewritten afterwards, so a later edit to the rule cannot silently restate what a
            // previously reviewed violation was judged against.
            var required = existing?.RequiredValue ?? rule.RuleValue;
            var outcome = measurement.Outcome ?? (definition.Passes(measurement.Value!.Value, required)
                ? ComplianceOutcomes.Pass
                : ComplianceOutcomes.Fail);

            var location = LocationLabel(definition, context);
            var explanation = measurement.Outcome switch
            {
                ComplianceOutcomes.NotApplicable => $"Not applicable: {measurement.Reason}",
                ComplianceOutcomes.InsufficientData => $"Cannot be assessed: {measurement.Reason}",
                _ => definition.Explain(measurement.Value!.Value, required, location)
            };

            var evaluation = existing;
            if (evaluation is null)
            {
                evaluation = new ComplianceEvaluation
                {
                    Id = Guid.NewGuid(),
                    ImageAnalysisId = imageAnalysisId ?? detection.ImageAnalysisId ?? Guid.Empty,
                    DetectionId = detection.Id,
                    ZoningRuleId = rule.Id,
                    EvaluationType = definition.CanonicalKey,
                    MeasuredValue = measurement.Value,
                    RequiredValue = rule.RuleValue,
                    Unit = definition.Unit,
                    Outcome = outcome,
                    MetricCrs = 32636,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.ComplianceEvaluations.Add(evaluation);
            }
            else
            {
                // Re-measure in place. RequiredValue is deliberately left as first recorded.
                evaluation.MeasuredValue = measurement.Value;
                evaluation.Outcome = outcome;
                evaluation.Unit = definition.Unit;
                evaluation.MetricCrs = 32636;
                if (evaluation.ZoningRuleId is null) evaluation.ZoningRuleId = rule.Id;
            }

            Guid? violationId = evaluation.Violation?.Id;
            var created = false;

            // A failing rule raises at most one candidate violation. An existing violation is never
            // reopened, re-decided, or removed here — human review outcomes are left untouched.
            if (outcome == ComplianceOutcomes.Fail && evaluation.Violation is null &&
                !await db.Violations.AnyAsync(v => v.ComplianceEvaluationId == evaluation.Id, ct))
            {
                var violation = new Violation
                {
                    Id = Guid.NewGuid(),
                    ComplianceEvaluationId = evaluation.Id,
                    DetectionId = detection.Id,
                    ParcelId = definition.RequiredContext.HasFlag(SpatialContext.Parcel) ? context.ParcelId : null,
                    RoadId = definition.RequiredContext.HasFlag(SpatialContext.Road) ? context.NearestRoadId : null,
                    ZoneId = context.ZoneId,
                    BlockId = context.BlockId,
                    RuleId = rule.Id,
                    ViolationType = definition.CanonicalKey,
                    MeasuredValue = measurement.Value,
                    RequiredValue = required,
                    ExceedanceValue = definition.Exceedance(measurement.Value!.Value, required),
                    AiConfidenceScore = detection.Confidence,
                    CurrentStatusId = 1, // under_review — never auto-confirmed.
                    Geom = detection.Geom,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.Violations.Add(violation);
                db.ViolationStatusHistory.Add(new ViolationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    ViolationId = violation.Id,
                    StatusId = 1,
                    Notes = $"Raised automatically by the zoning-rule engine: {explanation}",
                    ChangedAt = DateTimeOffset.UtcNow
                });
                violationId = violation.Id;
                created = true;
            }

            results.Add(new RuleEvaluationResult(definition.CanonicalKey, definition.Label, rule.Id,
                outcome, measurement.Value, required, definition.Unit, explanation, created, violationId));
        }

        return results;
    }

    // --- Applicability and precedence ----------------------------------------------------------

    /// <summary>
    /// Picks the rules that apply to this detection. Within one canonical rule type a rule bound
    /// to the exact zone wins over a zone-type fallback, so the specific and generic versions of
    /// the same rule are never both applied.
    /// </summary>
    private static async Task<List<(ZoningRuleDefinition Definition, ZoningRule Rule)>> ApplicableRulesAsync(
        AeroComplianceDbContext db, Guid municipalityId, DetectionContext context, CancellationToken ct)
    {
        var candidates = await db.ZoningRules.AsNoTracking()
            .Where(x => x.MunicipalityId == municipalityId && x.IsActive && x.IsValidated)
            .Where(x => (context.ZoneId != null && x.ZoneId == context.ZoneId) ||
                        (x.ZoneId == null && x.ZoneType != null && context.ZoneType != null &&
                         x.ZoneType.ToLower() == context.ZoneType.ToLower()))
            .ToListAsync(ct);

        return candidates
            .Select(rule => (Definition: ZoningRuleCatalog.Find(rule.RuleKey), Rule: rule))
            .Where(x => x.Definition is not null && ZoningRuleCatalog.IsConfigurationValid(x.Rule, x.Definition!))
            .GroupBy(x => x.Definition!.CanonicalKey)
            .Select(group => group
                // Zone-specific first; then the most recently created, so an updated rule wins
                // deterministically over an older duplicate of the same scope.
                .OrderByDescending(x => x.Rule.ZoneId.HasValue)
                .ThenByDescending(x => x.Rule.CreatedAt)
                .First())
            .Select(x => (x.Definition!, x.Rule))
            .OrderBy(x => x.Item1.CanonicalKey)
            .ToList();
    }

    // --- Spatial context ------------------------------------------------------------------------

    /// <summary>
    /// Resolves the primary parcel (largest intersection with the detection), its block and zone,
    /// and the nearest road. Everything is optional: a detection over unmapped ground simply
    /// yields nulls, and the rules that need that context record a safe non-failure outcome.
    /// </summary>
    public static async Task<DetectionContext> ResolveContextAsync(
        AeroComplianceDbContext db, Guid municipalityId, MultiPolygon geom, CancellationToken ct)
    {
        var conn = await OpenAsync(db, ct);

        // ST_MakeValid guards against a self-intersecting detection outline aborting the whole
        // query; it repairs the ring topology without inventing new geometry.
        const string parcelSql = """
            SELECT p.id, p.parcel_number, ST_Area(ST_Transform(p.geom, 32636)),
                   b.id, b.block_number, z.id, z.zone_number, z.zone_type
            FROM parcels p
            JOIN blocks b ON b.id = p.block_id
            JOIN zones  z ON z.id = b.zone_id
            WHERE z.municipality_id = @m
              AND ST_Intersects(p.geom, ST_MakeValid(@g))
            ORDER BY ST_Area(ST_Intersection(ST_Transform(ST_MakeValid(p.geom), 32636),
                                             ST_Transform(ST_MakeValid(@g), 32636))) DESC
            LIMIT 1
            """;

        Guid? parcelId = null, blockId = null, zoneId = null;
        string? parcelNumber = null, blockNumber = null, zoneNumber = null, zoneType = null;
        decimal? parcelArea = null;

        await using (var cmd = Command(db, conn, parcelSql, municipalityId, geom))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                parcelId = reader.GetGuid(0);
                parcelNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                parcelArea = reader.IsDBNull(2) ? null : (decimal)reader.GetDouble(2);
                blockId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
                blockNumber = reader.IsDBNull(4) ? null : reader.GetString(4);
                zoneId = reader.IsDBNull(5) ? null : reader.GetGuid(5);
                zoneNumber = reader.IsDBNull(6) ? null : reader.GetString(6);
                zoneType = reader.IsDBNull(7) ? null : reader.GetString(7);
            }
        }

        // No parcel underneath: fall back to the zone the detection sits in, so a zone-scoped rule
        // still applies to a building on unparcelled land.
        if (zoneId is null)
        {
            const string zoneSql = """
                SELECT z.id, z.zone_number, z.zone_type
                FROM zones z
                WHERE z.municipality_id = @m AND ST_Intersects(z.geom, ST_MakeValid(@g))
                ORDER BY ST_Area(ST_Intersection(ST_Transform(ST_MakeValid(z.geom), 32636),
                                                 ST_Transform(ST_MakeValid(@g), 32636))) DESC
                LIMIT 1
                """;
            await using var cmd = Command(db, conn, zoneSql, municipalityId, geom);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                zoneId = reader.GetGuid(0);
                zoneNumber = reader.IsDBNull(1) ? null : reader.GetString(1);
                zoneType = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
        }

        const string roadSql = """
            SELECT r.id, r.road_code, r.name,
                   ST_Distance(ST_Transform(ST_MakeValid(r.geom), 32636), ST_Transform(ST_MakeValid(@g), 32636))
            FROM roads r
            WHERE r.municipality_id = @m
            ORDER BY 4 ASC
            LIMIT 1
            """;

        Guid? roadId = null; string? roadCode = null, roadName = null; double? roadDistance = null;
        await using (var cmd = Command(db, conn, roadSql, municipalityId, geom))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                roadId = reader.GetGuid(0);
                roadCode = reader.IsDBNull(1) ? null : reader.GetString(1);
                roadName = reader.IsDBNull(2) ? null : reader.GetString(2);
                roadDistance = reader.IsDBNull(3) ? null : reader.GetDouble(3);
            }
        }

        return new DetectionContext(parcelId, parcelNumber, parcelArea, blockId, blockNumber,
            zoneId, zoneNumber, zoneType, roadId, roadCode, roadName, roadDistance);
    }

    // --- Measurement ------------------------------------------------------------------------------

    private sealed record Measurement(decimal? Value, string? Outcome, string? Reason);

    /// <summary>
    /// Measures one rule. Returns a non-null <c>Outcome</c> only when the rule could not be
    /// measured at all — in that case the value is null and no violation can result.
    /// </summary>
    private static async Task<Measurement> MeasureAsync(
        AeroComplianceDbContext db, ZoningRuleDefinition definition, MultiPolygon geom,
        DetectionContext context, CancellationToken ct)
    {
        if (definition.RequiredContext.HasFlag(SpatialContext.Road) && context.NearestRoadId is null)
            return new(null, ComplianceOutcomes.NotApplicable, "no road is mapped in this municipality to measure against.");
        if (definition.RequiredContext.HasFlag(SpatialContext.Parcel) && context.ParcelId is null)
            return new(null, ComplianceOutcomes.NotApplicable, "the detection does not fall within a mapped parcel.");

        var conn = await OpenAsync(db, ct);
        switch (definition.CanonicalKey)
        {
            case ZoningRuleCatalog.MinimumRoadSetback:
                return context.NearestRoadDistanceM is double d
                    ? new((decimal)Math.Round(d, 3), null, null)
                    : new(null, ComplianceOutcomes.InsufficientData, "the distance to the nearest road could not be measured.");

            case ZoningRuleCatalog.MaximumBuildingArea:
            {
                var area = await ScalarAsync(db, conn,
                    "SELECT ST_Area(ST_Transform(ST_MakeValid(@g), 32636))", geom, null, ct);
                return area is null
                    ? new(null, ComplianceOutcomes.InsufficientData, "the detected outline has no measurable area.")
                    : new((decimal)Math.Round(area.Value, 3), null, null);
            }

            case ZoningRuleCatalog.MaximumParcelCoverage:
            {
                // Guard against a zero-area or unusable parcel polygon producing a divide-by-zero.
                var coverage = await ScalarAsync(db, conn, """
                    SELECT CASE
                             WHEN ST_Area(ST_Transform(ST_MakeValid(p.geom), 32636)) <= 0 THEN NULL
                             ELSE ST_Area(ST_Intersection(ST_Transform(ST_MakeValid(@g), 32636),
                                                          ST_Transform(ST_MakeValid(p.geom), 32636)))
                                  / ST_Area(ST_Transform(ST_MakeValid(p.geom), 32636)) * 100.0
                           END
                    FROM parcels p WHERE p.id = @p
                    """, geom, context.ParcelId, ct);
                return coverage is null
                    ? new(null, ComplianceOutcomes.InsufficientData, "the parcel boundary has no usable area.")
                    : new((decimal)Math.Round(coverage.Value, 3), null, null);
            }

            case ZoningRuleCatalog.MaximumOutsideParcelArea:
            {
                var outside = await ScalarAsync(db, conn, """
                    SELECT ST_Area(ST_Difference(ST_Transform(ST_MakeValid(@g), 32636),
                                                 ST_Transform(ST_MakeValid(p.geom), 32636)))
                    FROM parcels p WHERE p.id = @p
                    """, geom, context.ParcelId, ct);
                return outside is null
                    ? new(null, ComplianceOutcomes.InsufficientData, "the overlap with the parcel boundary could not be measured.")
                    : new((decimal)Math.Round(Math.Max(0, outside.Value), 3), null, null);
            }

            default:
                return new(null, ComplianceOutcomes.NotApplicable, "this rule type has no measurement defined.");
        }
    }

    private static string? LocationLabel(ZoningRuleDefinition definition, DetectionContext context)
    {
        if (definition.RequiredContext.HasFlag(SpatialContext.Road) && context.NearestRoadCode is not null)
            return context.NearestRoadName is null
                ? $"road {context.NearestRoadCode}"
                : $"road {context.NearestRoadCode} ({context.NearestRoadName})";
        if (definition.RequiredContext.HasFlag(SpatialContext.Parcel) && context.ParcelNumber is not null)
            return $"parcel {context.ParcelNumber}";
        return context.ParcelNumber is not null ? $"parcel {context.ParcelNumber}"
            : context.ZoneNumber is not null ? $"zone {context.ZoneNumber}" : null;
    }

    // --- Npgsql plumbing --------------------------------------------------------------------------

    /// <summary>
    /// The measurements run as raw PostGIS SQL on the context's own connection so they join the
    /// caller's transaction (the processing pipeline evaluates inside one) and therefore see the
    /// detection row it has just written.
    /// </summary>
    private static async Task<NpgsqlConnection> OpenAsync(AeroComplianceDbContext db, CancellationToken ct)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        return conn;
    }

    private static NpgsqlTransaction? CurrentTransaction(AeroComplianceDbContext db) =>
        db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

    private static NpgsqlCommand Command(
        AeroComplianceDbContext db, NpgsqlConnection conn, string sql, Guid municipalityId, MultiPolygon geom)
    {
        var cmd = new NpgsqlCommand(sql, conn, CurrentTransaction(db));
        cmd.Parameters.AddWithValue("m", municipalityId);
        cmd.Parameters.AddWithValue("g", NpgsqlDbType.Geometry, geom);
        return cmd;
    }

    private static async Task<double?> ScalarAsync(AeroComplianceDbContext db,
        NpgsqlConnection conn, string sql, MultiPolygon geom, Guid? parcelId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, CurrentTransaction(db));
        cmd.Parameters.AddWithValue("g", NpgsqlDbType.Geometry, geom);
        if (parcelId.HasValue) cmd.Parameters.AddWithValue("p", parcelId.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToDouble(result);
    }
}
