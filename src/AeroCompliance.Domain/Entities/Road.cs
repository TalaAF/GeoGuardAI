using NetTopologySuite.Geometries;

namespace AeroCompliance.Domain.Entities;

/// <summary>A road (stored as its right-of-way polygon). Maps to <c>roads</c>.</summary>
public class Road
{
    public Guid Id { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;

    public string RoadCode { get; set; } = null!;
    public string? Name { get; set; }

    /// <summary>Main Road, Local, Sensitive, ...</summary>
    public string? RoadType { get; set; }

    public decimal? WidthM { get; set; }
    public int? SpeedLimitKmh { get; set; }

    /// <summary>Sensitive roads trigger the fast police-alert workflow.</summary>
    public bool IsSensitive { get; set; }

    /// <summary>Road right-of-way as a MultiPolygon, SRID 4326.</summary>
    public MultiPolygon Geom { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
