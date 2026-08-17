using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>A land parcel within a block. Maps to <c>parcels</c>.</summary>
public class Parcel
{
    public Guid Id { get; set; }

    public Guid BlockId { get; set; }
    public Block Block { get; set; } = null!;

    public string ParcelNumber { get; set; } = null!;
    public string? OwnerName { get; set; }

    /// <summary>Usually inherited from the zone, but may be overridden per parcel.</summary>
    public string? ZoneType { get; set; }

    /// <summary>Area in square metres (computed from geom or imported). NUMERIC(12,2).</summary>
    public decimal? AreaM2 { get; set; }

    /// <summary>MultiPolygon geometry, SRID 4326.</summary>
    public MultiPolygon Geom { get; set; } = null!;

    /// <summary>Provenance of the parcel record; defaults to <c>municipality_gis</c> (never AI).</summary>
    public string Source { get; set; } = "municipality_gis";
    public DateTimeOffset CreatedAt { get; set; }
}
