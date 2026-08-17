using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>A stitched orthomosaic produced from many images. Maps to <c>orthomosaics</c>.</summary>
public class Orthomosaic
{
    public Guid Id { get; set; }

    public Guid FlightId { get; set; }
    public Flight Flight { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    /// <summary>Geographic envelope of the full image, Polygon SRID 4326.</summary>
    public Polygon Bounds { get; set; } = null!;

    public decimal? ResolutionCmPerPx { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}
