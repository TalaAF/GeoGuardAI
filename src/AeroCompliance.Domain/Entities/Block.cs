using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>A block within a zone. Maps to <c>blocks</c>.</summary>
public class Block
{
    public Guid Id { get; set; }

    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;

    public string BlockNumber { get; set; } = null!;

    /// <summary>MultiPolygon geometry, SRID 4326.</summary>
    public MultiPolygon Geom { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Parcel> Parcels { get; set; } = new List<Parcel>();
}
