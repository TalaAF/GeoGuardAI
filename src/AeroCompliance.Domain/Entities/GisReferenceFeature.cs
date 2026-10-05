namespace AeroCompliance.Domain.Entities;

/// <summary>
/// Reference GIS data imported from a municipal source layer. Maps to <c>gis_reference_features</c>.
/// </summary>
/// <remarks>
/// Deliberately separate from the operational entities. These rows are <b>evidence and comparison
/// baselines</b>, never live compliance records:
/// <list type="bullet">
/// <item>Building ground truth is not an AI <see cref="Detection"/> — it was never produced by a model.</item>
/// <item>A historical encroachment polygon is not a current <see cref="Violation"/> — nobody reviewed it here.</item>
/// <item>A road-encroachment reference polygon is not a <see cref="Road"/> — it is the *result* of a
/// past analysis, not a street network, and the source data contains no complete road layer.</item>
/// </list>
/// Storing them in their own table keeps those distinctions structural rather than conventional.
/// </remarks>
public class GisReferenceFeature
{
    public long Id { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;

    /// <summary>What this feature is for; see <see cref="GisReferenceTypes"/>.</summary>
    public string ReferenceType { get; set; } = null!;

    /// <summary>Originating layer basename, e.g. <c>building_32636</c>.</summary>
    public string SourceLayer { get; set; } = null!;

    /// <summary>
    /// Stable identifier from the source layer (its OBJECTID where present, otherwise the record
    /// ordinal). Combined with <see cref="SourceLayer"/> this is the natural key that makes
    /// re-running the importer update rather than duplicate.
    /// </summary>
    public string SourceFeatureId { get; set; } = null!;

    /// <summary>CRS the source layer was read in, e.g. <c>EPSG:28191</c>, recorded for provenance.</summary>
    public string SourceCrs { get; set; } = null!;

    /// <summary>Canonical geometry, MultiPolygon SRID 4326 — as with every other geometry here.</summary>
    public NetTopologySuite.Geometries.MultiPolygon Geom { get; set; } = null!;

    /// <summary>Area in square metres, measured in EPSG:32636 at import time.</summary>
    public double? AreaM2 { get; set; }

    /// <summary>
    /// A small, explicitly selected subset of source attributes (block/parcel/quarter numbers),
    /// stored as JSON for display and matching. Never the whole DBF row.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>SHA-256 of the source .shp, so an import can be traced back to exact bytes.</summary>
    public string SourceChecksum { get; set; } = null!;

    /// <summary>True when the source geometry was invalid and had to be repaired on import.</summary>
    public bool GeometryRepaired { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>Canonical <see cref="GisReferenceFeature.ReferenceType"/> values.</summary>
public static class GisReferenceTypes
{
    /// <summary>Municipal building footprints, used to score AI detections — not detections themselves.</summary>
    public const string BuildingGroundTruth = "BuildingGroundTruth";

    /// <summary>Previously-derived parcel-encroachment result polygons. Historical, not current findings.</summary>
    public const string ParcelEncroachmentReference = "ParcelEncroachmentReference";

    /// <summary>
    /// Previously-derived road-encroachment result polygons. These are past *results*, not a road
    /// network: the source data contains no complete Roads layer, so a match against these can only
    /// ever mean "overlaps a known encroachment area", never "encroaches on any road".
    /// </summary>
    public const string RoadEncroachmentReference = "RoadEncroachmentReference";

    public static readonly string[] All =
    [
        BuildingGroundTruth, ParcelEncroachmentReference, RoadEncroachmentReference
    ];
}
