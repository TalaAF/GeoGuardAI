using System.Text.Json;
using NetTopologySuite.Geometries;

namespace AeroCompliance.ProcessingService;

internal static class GeometryReader
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);
    public static MultiPolygon Read(JsonElement json)
    {
        var type = json.GetProperty("type").GetString(); var coordinates = json.GetProperty("coordinates");
        var polygons = type switch { "Polygon" => new[] { Polygon(coordinates) }, "MultiPolygon" => coordinates.EnumerateArray().Select(Polygon).ToArray(), _ => throw new InvalidDataException("Unsupported geometry type.") };
        var result = Factory.CreateMultiPolygon(polygons); result.SRID = 4326;
        if (result.IsEmpty || !result.IsValid) throw new InvalidDataException("Invalid detection geometry.");
        return result;
    }
    private static Polygon Polygon(JsonElement node) { var rings = node.EnumerateArray().Select(Ring).ToArray(); return Factory.CreatePolygon(rings[0], rings.Skip(1).ToArray()); }
    private static LinearRing Ring(JsonElement node) => Factory.CreateLinearRing(node.EnumerateArray().Select(p => { var a = p.EnumerateArray().ToArray(); return new Coordinate(a[0].GetDouble(), a[1].GetDouble()); }).ToArray());
}
