using System.Text.Json;
using NetTopologySuite.Geometries;

namespace AeroCompliance.ApiService.Services;

public static class GeoJsonService
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static bool TryReadMultiPolygon(JsonElement json, out MultiPolygon geometry, out string error)
    {
        geometry = Factory.CreateMultiPolygon();
        error = string.Empty;
        try
        {
            if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("type", out var typeNode) ||
                !json.TryGetProperty("coordinates", out var coordinates))
            {
                error = "Geometry must be a GeoJSON object with type and coordinates.";
                return false;
            }

            var type = typeNode.GetString();
            Polygon[] polygons = type switch
            {
                "Polygon" => [ReadPolygon(coordinates)],
                "MultiPolygon" => coordinates.EnumerateArray().Select(ReadPolygon).ToArray(),
                _ => throw new FormatException("Geometry type must be Polygon or MultiPolygon.")
            };

            geometry = Factory.CreateMultiPolygon(polygons);
            geometry.SRID = 4326;
            if (geometry.IsEmpty || !geometry.IsValid)
            {
                error = "Geometry must be non-empty and valid.";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or JsonException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static JsonElement Write(MultiPolygon geometry)
        => JsonSerializer.SerializeToElement(new
        {
            type = "MultiPolygon",
            coordinates = geometry.Geometries.Cast<Polygon>().Select(WritePolygon).ToArray()
        });

    private static double[][][] WritePolygon(Polygon polygon)
        => new[] { polygon.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray() }
            .Concat(Enumerable.Range(0, polygon.NumInteriorRings)
                .Select(i => polygon.GetInteriorRingN(i).Coordinates.Select(c => new[] { c.X, c.Y }).ToArray()))
            .ToArray();

    private static Polygon ReadPolygon(JsonElement polygonNode)
    {
        var rings = polygonNode.EnumerateArray().Select(ReadRing).ToArray();
        if (rings.Length == 0) throw new FormatException("A polygon requires an exterior ring.");
        return Factory.CreatePolygon(rings[0], rings.Skip(1).ToArray());
    }

    private static LinearRing ReadRing(JsonElement ringNode)
    {
        var coordinates = ringNode.EnumerateArray().Select(point =>
        {
            var values = point.EnumerateArray().ToArray();
            if (values.Length < 2) throw new FormatException("Each coordinate needs longitude and latitude.");
            var longitude = values[0].GetDouble();
            var latitude = values[1].GetDouble();
            if (!double.IsFinite(longitude) || !double.IsFinite(latitude) || longitude is < -180 or > 180 || latitude is < -90 or > 90)
                throw new FormatException("Coordinates must be finite WGS84 longitude/latitude values.");
            return new Coordinate(longitude, latitude);
        }).ToArray();
        if (coordinates.Length < 4 || !coordinates[0].Equals2D(coordinates[^1]))
            throw new FormatException("Polygon rings need at least four coordinates and must be closed.");
        return Factory.CreateLinearRing(coordinates);
    }
}
