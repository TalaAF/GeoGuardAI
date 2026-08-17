using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>Planning zone from the municipal structure plan. Maps to <c>zones</c>.</summary>
public class Zone
{
    public Guid Id { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;

    public string ZoneNumber { get; set; } = null!;
    public string? Name { get; set; }

    /// <summary>Residential, Commercial, Agricultural, ...</summary>
    public string? ZoneType { get; set; }

    /// <summary>MultiPolygon geometry, SRID 4326 (WGS84).</summary>
    public MultiPolygon Geom { get; set; } = null!;

    public string? SourceDocument { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Block> Blocks { get; set; } = new List<Block>();
    public ICollection<ZoningRule> ZoningRules { get; set; } = new List<ZoningRule>();
}
