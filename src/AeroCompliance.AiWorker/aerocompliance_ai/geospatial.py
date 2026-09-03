"""Raster georeferencing: GeoTIFF metadata extraction and pixel -> raster -> EPSG:4326 conversion.

`rasterio` (and `shapely`, used only for geometry validation/repair) are optional dependencies —
see requirements-yolo.txt — so the default lightweight/mock-only worker image keeps starting and
processing images without them. Every public function here degrades safely when they are missing:
`extract_raster_metadata` returns ``None`` rather than raising, and the conversion helpers raise a
clearly-coded `GeoreferencingError` that the caller turns into a retryable/non-retryable
`ImageAnalysisFailed` event instead of ever inventing geographic coordinates from pixels.
"""

from dataclasses import dataclass
from typing import Optional

try:
    import rasterio
    from rasterio.warp import transform as _warp_transform

    HAS_RASTERIO = True
except ImportError:  # pragma: no cover - exercised only on the lightweight/mock-only build
    HAS_RASTERIO = False

try:
    from shapely.geometry import MultiPolygon, Polygon, mapping
    from shapely.validation import make_valid

    HAS_SHAPELY = True
except ImportError:  # pragma: no cover - exercised only on the lightweight/mock-only build
    HAS_SHAPELY = False


class GeoreferencingError(Exception):
    """Raised for a raster that cannot be safely treated as geographic data. ``code`` is a stable,
    machine-readable failure code surfaced on the `ImageAnalysisFailed` event."""

    def __init__(self, code: str, message: str):
        super().__init__(message)
        self.code = code


@dataclass(frozen=True)
class RasterMetadata:
    crs: str
    width: int
    height: int
    affine_transform: list  # [a, b, c, d, e, f] — rasterio's Affine coefficients, row-major.
    bounds: tuple  # (left, bottom, right, top), in the raster's own CRS.
    resolution: tuple  # (pixel width, pixel height), in the raster's own CRS units.


def require_georeferencing(crs, affine) -> None:
    """Guard used before treating raster/model output as geographic: fail loudly with a clear code
    rather than silently comparing pixel coordinates to PostGIS/road/parcel geometry."""
    if not crs or not affine:
        raise GeoreferencingError(
            "MISSING_GEOREFERENCING",
            "Raster CRS and affine transform are required; pixel coordinates cannot be used as map coordinates.",
        )


def extract_raster_metadata(path: str) -> Optional[RasterMetadata]:
    """Open ``path`` with rasterio and return its georeferencing.

    Returns ``None`` (does not raise) only when rasterio itself is not installed, so mock-mode
    processing of a GeoTIFF on the lightweight build can still complete without raster metadata.
    Once rasterio *is* available, a raster that fails to open, has no CRS, has only an identity
    transform, or has non-finite transform/bounds values raises `GeoreferencingError` with a
    specific code — it is never silently treated as "no metadata".
    """
    if not HAS_RASTERIO:
        return None

    try:
        with rasterio.open(path) as dataset:
            transform = dataset.transform
            crs = dataset.crs
            if crs is None or transform is None or transform.is_identity:
                raise GeoreferencingError(
                    "MISSING_GEOREFERENCING",
                    "The raster has no CRS/affine transform (or only a default identity transform) "
                    "— it is not genuinely georeferenced.",
                )

            coeffs = [transform.a, transform.b, transform.c, transform.d, transform.e, transform.f]
            bounds = tuple(dataset.bounds)  # (left, bottom, right, top)
            if any(not _finite(v) for v in (*coeffs, *bounds)):
                raise GeoreferencingError(
                    "INVALID_GEOREFERENCING",
                    "The raster's affine transform or bounds contain non-finite values.",
                )

            return RasterMetadata(
                crs=crs.to_string(),
                width=dataset.width,
                height=dataset.height,
                affine_transform=coeffs,
                bounds=bounds,
                resolution=(abs(transform.a), abs(transform.e)),
            )
    except GeoreferencingError:
        raise
    except Exception as exc:  # rasterio raising on a corrupt/unsupported file, missing driver, etc.
        raise GeoreferencingError(
            "INVALID_GEOREFERENCING", f"Could not read raster georeferencing: {exc}"
        ) from exc


def _finite(value: float) -> bool:
    return value == value and value not in (float("inf"), float("-inf"))  # NaN != NaN


def pixel_to_raster(affine_transform: list, col: float, row: float) -> tuple:
    """Convert a pixel (column, row) coordinate to the raster's own CRS via its affine transform."""
    a, b, c, d, e, f = affine_transform
    return a * col + b * row + c, d * col + e * row + f


def raster_to_wgs84(crs: str, xs: list, ys: list) -> list:
    """Reproject points from ``crs`` into EPSG:4326, returning a list of (lon, lat) pairs."""
    if not HAS_RASTERIO:
        raise GeoreferencingError("RASTERIO_UNAVAILABLE", "rasterio is not installed in this worker build.")
    lons, lats = _warp_transform(crs, "EPSG:4326", xs, ys)
    return list(zip(lons, lats))


def polygon_pixels_to_wgs84(affine_transform: list, crs: str, rings: list) -> list:
    """Convert one polygon's pixel-space rings — a list of rings, each a list of (col, row) pairs,
    exterior ring first — into WGS84 (lon, lat) rings. Every ring is preserved (exterior + holes)."""
    wgs84_rings = []
    for ring in rings:
        raster_xy = [pixel_to_raster(affine_transform, col, row) for col, row in ring]
        xs, ys = (list(v) for v in zip(*raster_xy)) if raster_xy else ([], [])
        wgs84_rings.append(raster_to_wgs84(crs, xs, ys) if xs else [])
    return wgs84_rings


def build_multipolygon_geojson(polygons_wgs84: list) -> Optional[dict]:
    """Combine one or more polygons (each already converted to WGS84 rings, exterior first) into a
    single GeoJSON MultiPolygon.

    When shapely is available (see requirements-yolo.txt), each part is validated and repaired
    in isolation (`make_valid`) — a self-intersecting or otherwise invalid part is fixed rather than
    dropping the whole detection, and only a genuinely degenerate part (too few points, empty after
    repair) is skipped. Without shapely, rings are passed through as-is after a basic point-count
    check, since no repair is possible.
    """
    if not polygons_wgs84:
        return None

    if not HAS_SHAPELY:
        coordinates = [
            [[list(point) for point in ring] for ring in rings]
            for rings in polygons_wgs84
            if rings and len(rings[0]) >= 4
        ]
        return {"type": "MultiPolygon", "coordinates": coordinates} if coordinates else None

    parts = []
    for rings in polygons_wgs84:
        if not rings or len(rings[0]) < 4:
            continue
        try:
            polygon = Polygon(rings[0], rings[1:])
            if not polygon.is_valid:
                polygon = make_valid(polygon)
        except Exception:
            continue  # Skip only this malformed part — preserve every other valid part.
        if polygon.is_empty:
            continue
        if polygon.geom_type == "Polygon":
            parts.append(polygon)
        elif polygon.geom_type == "MultiPolygon":
            parts.extend(list(polygon.geoms))

    return mapping(MultiPolygon(parts)) if parts else None
