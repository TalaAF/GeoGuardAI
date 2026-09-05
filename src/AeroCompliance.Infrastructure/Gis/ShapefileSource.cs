using System.Security.Cryptography;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace AeroCompliance.Infrastructure.Gis;

/// <summary>A validated, ready-to-read Shapefile: complete component set plus a resolved source CRS.</summary>
public sealed record ShapefileSource(
    string Path,
    string LayerName,
    int SourceEpsg,
    string SourceCrsName,
    string Sha256,
    int FeatureCount);

/// <summary>Why a candidate layer could not be imported. Reported, never silently skipped.</summary>
public sealed record ShapefileRejection(string Path, string LayerName, string Reason);

/// <summary>
/// Discovers and validates Shapefile component sets, and reprojects their geometry into the
/// canonical EPSG:4326 (plus EPSG:32636 for metric measurement).
/// </summary>
/// <remarks>
/// A Shapefile is only accepted with all four required components (.shp/.shx/.dbf/.prj) present and
/// a CRS that resolves to a concrete EPSG code. A missing or ambiguous .prj is a hard rejection —
/// guessing a CRS would silently place thousands of polygons in the wrong part of the world.
/// </remarks>
public static class ShapefileReader
{
    private static readonly CoordinateSystemFactory CsFactory = new();
    private static readonly CoordinateTransformationFactory TransformFactory = new();

    // Definitions for the two source CRSs present in the supplied data, plus the canonical targets.
    // ProjNet has no EPSG registry of its own, so the WKT is supplied explicitly and matched to the
    // EPSG code found in the .prj rather than trusting the .prj text itself for the maths.
    private const string Wgs84Wkt =
        """GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563,AUTHORITY["EPSG","7030"]],AUTHORITY["EPSG","6326"]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],UNIT["degree",0.0174532925199433,AUTHORITY["EPSG","9122"]],AUTHORITY["EPSG","4326"]]""";

    private const string Utm36NWkt =
        """PROJCS["WGS 84 / UTM zone 36N",GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563,AUTHORITY["EPSG","7030"]],AUTHORITY["EPSG","6326"]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],UNIT["degree",0.0174532925199433,AUTHORITY["EPSG","9122"]],AUTHORITY["EPSG","4326"]],PROJECTION["Transverse_Mercator"],PARAMETER["latitude_of_origin",0],PARAMETER["central_meridian",33],PARAMETER["scale_factor",0.9996],PARAMETER["false_easting",500000],PARAMETER["false_northing",0],UNIT["metre",1,AUTHORITY["EPSG","9001"]],AUTHORITY["EPSG","32636"]]""";

    // Palestine 1923 / Palestine Grid. The Clarke 1880 (Benoit) ellipsoid and the
    // Palestine 1923 -> WGS 84 (EPSG:1074) 3-parameter shift are stated explicitly so the
    // transformation is reproducible and reviewable rather than implicit.
    private const string PalestineGridWkt =
        """PROJCS["Palestine 1923 / Palestine Grid",GEOGCS["Palestine 1923",DATUM["Palestine_1923",SPHEROID["Clarke 1880 (Benoit)",6378300.789,293.4663155389811,AUTHORITY["EPSG","7010"]],TOWGS84[-275.7224,94.7824,340.8944,-8.001,-4.42,-11.821,1],AUTHORITY["EPSG","6281"]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],UNIT["degree",0.0174532925199433,AUTHORITY["EPSG","9122"]],AUTHORITY["EPSG","4281"]],PROJECTION["Transverse_Mercator"],PARAMETER["latitude_of_origin",31.73409694444445],PARAMETER["central_meridian",35.21208055555556],PARAMETER["scale_factor",1],PARAMETER["false_easting",170251.555],PARAMETER["false_northing",126867.909],UNIT["metre",1,AUTHORITY["EPSG","9001"]],AUTHORITY["EPSG","28191"]]""";

    private static readonly Dictionary<int, string> KnownCrsWkt = new()
    {
        [4326] = Wgs84Wkt,
        [32636] = Utm36NWkt,
        [28191] = PalestineGridWkt,
    };

    /// <summary>Builds a transform between two supported EPSG codes.</summary>
    public static MathTransform Transform(int fromEpsg, int toEpsg)
    {
        if (!KnownCrsWkt.TryGetValue(fromEpsg, out var fromWkt))
            throw new NotSupportedException($"No CRS definition is registered for EPSG:{fromEpsg}.");
        if (!KnownCrsWkt.TryGetValue(toEpsg, out var toWkt))
            throw new NotSupportedException($"No CRS definition is registered for EPSG:{toEpsg}.");
        var from = CsFactory.CreateFromWkt(fromWkt);
        var to = CsFactory.CreateFromWkt(toWkt);
        return TransformFactory.CreateFromCoordinateSystems(from, to).MathTransform;
    }

    /// <summary>
    /// Validates one candidate .shp. Returns the source on success, or a rejection explaining
    /// exactly which requirement failed.
    /// </summary>
    public static (ShapefileSource? Source, ShapefileRejection? Rejection) Validate(string shpPath)
    {
        var layer = System.IO.Path.GetFileNameWithoutExtension(shpPath);
        var dir = System.IO.Path.GetDirectoryName(shpPath)!;

        foreach (var ext in new[] { ".shx", ".dbf", ".prj" })
        {
            var companion = System.IO.Path.Combine(dir, layer + ext);
            if (!File.Exists(companion))
                return (null, new ShapefileRejection(shpPath, layer,
                    $"incomplete component set: missing {ext} (a Shapefile needs .shp/.shx/.dbf/.prj)"));
        }

        var prjText = File.ReadAllText(System.IO.Path.Combine(dir, layer + ".prj")).Trim();
        var (epsg, crsName) = ResolveEpsg(prjText);
        if (epsg is null)
            return (null, new ShapefileRejection(shpPath, layer,
                $"CRS could not be resolved to a supported EPSG code from the .prj ({crsName})"));

        int count;
        try
        {
            // Geometry-only: never parse the DBF here. Attributes are read separately by
            // TolerantDbfReader, because several supplied DBFs store binary in numeric fields and
            // a strict reader would reject the entire layer over a handful of dirty records.
            count = Shapefile.ReadAllGeometries(shpPath).Length;
            if (count == 0)
                return (null, new ShapefileRejection(shpPath, layer, "the layer contains zero features"));
        }
        catch (Exception ex)
        {
            return (null, new ShapefileRejection(shpPath, layer,
                $"not a readable ESRI Shapefile: {ex.GetType().Name}: {ex.Message}"));
        }

        return (new ShapefileSource(shpPath, layer, epsg.Value, crsName, Sha256File(shpPath), count), null);
    }

    /// <summary>
    /// Resolves the EPSG code from .prj WKT. Matched on the projection/datum name rather than an
    /// AUTHORITY node, because the supplied .prj files carry no AUTHORITY entries.
    /// </summary>
    private static (int? Epsg, string Name) ResolveEpsg(string wkt)
    {
        var name = ExtractFirstQuoted(wkt) ?? "unknown";
        // ESRI writes these names with underscores and omits the AUTHORITY node, so normalise
        // separators before matching rather than relying on an EPSG code being present.
        var probe = name.Replace('_', ' ');
        if (wkt.Contains("32636", StringComparison.Ordinal) ||
            probe.Contains("UTM Zone 36N", StringComparison.OrdinalIgnoreCase) ||
            probe.Contains("UTM zone 36N", StringComparison.OrdinalIgnoreCase))
            return (32636, name);
        if (wkt.Contains("28191", StringComparison.Ordinal) ||
            probe.Contains("Palestine", StringComparison.OrdinalIgnoreCase))
            return (28191, name);
        if (probe.Contains("WGS 1984", StringComparison.OrdinalIgnoreCase) ||
            probe.Contains("WGS 84", StringComparison.OrdinalIgnoreCase))
        {
            if (!wkt.Contains("PROJCS", StringComparison.Ordinal)) return (4326, name);
        }
        return (null, name);
    }

    private static string? ExtractFirstQuoted(string wkt)
    {
        var start = wkt.IndexOf('"');
        if (start < 0) return null;
        var end = wkt.IndexOf('"', start + 1);
        return end < 0 ? null : wkt[(start + 1)..end];
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Reprojects a geometry between EPSG codes, returning a normalized valid MultiPolygon, or null
    /// when nothing usable survives. Invalid rings are repaired via a zero-width buffer, which
    /// resolves self-intersections without inventing area.
    /// </summary>
    public static (MultiPolygon? Geometry, bool Repaired) ToMultiPolygon(
        Geometry source, MathTransform transform, GeometryFactory factory)
    {
        if (source is null || source.IsEmpty) return (null, false);

        var projected = Reproject(source, transform, factory);
        var repaired = false;
        if (!projected.IsValid)
        {
            projected = projected.Buffer(0);
            repaired = true;
            if (projected.IsEmpty) return (null, true);
        }

        var polygons = new List<Polygon>();
        Collect(projected, polygons);
        if (polygons.Count == 0) return (null, repaired);

        var multi = factory.CreateMultiPolygon(polygons.ToArray());
        multi.SRID = factory.SRID;
        return (multi, repaired);
    }

    private static void Collect(Geometry geometry, List<Polygon> into)
    {
        switch (geometry)
        {
            case Polygon p when !p.IsEmpty:
                into.Add(p);
                break;
            case GeometryCollection collection:
                foreach (var g in collection.Geometries) Collect(g, into);
                break;
        }
    }

    private static Geometry Reproject(Geometry source, MathTransform transform, GeometryFactory factory)
    {
        var clone = source.Copy();
        clone.Apply(new MathTransformFilter(transform));
        clone.GeometryChanged();
        return factory.CreateGeometry(clone);
    }

    private sealed class MathTransformFilter(MathTransform transform) : ICoordinateSequenceFilter
    {
        public bool Done => false;
        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var (x, y) = transform.Transform(seq.GetX(i), seq.GetY(i));
            seq.SetX(i, x);
            seq.SetY(i, y);
        }
    }
}
