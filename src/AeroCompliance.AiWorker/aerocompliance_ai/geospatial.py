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
    tags: dict  # GeoTIFF metadata tags, carrying any experimental-georeferencing declaration.


# Provenance values for a detection's geometry, so a consumer never has to guess how a coordinate
# was obtained.
SOURCE_REAL_YOLO_ESTIMATED = "RealYoloEstimatedGeometry"
SOURCE_REAL_YOLO_MODEL_ONLY = "RealYoloModelOnly"
SOURCE_MOCK_GEOGRAPHIC = "MockGeographicScenario"
# Kept distinct from the estimated case: a genuinely surveyed raster must not be reported under a
# label that says "estimated".
SOURCE_REAL_YOLO_GEOREFERENCED = "RealYoloGeoreferencedGeometry"


@dataclass(frozen=True)
class ExperimentalPolicy:
    """An approximate georeference that the raster itself declares, plus the region it is trusted in.

    Read from the derivative's own GeoTIFF tags rather than from worker configuration, so the
    restriction travels with the file: any raster without these tags is treated as a normal,
    fully-trusted georeference and behaves exactly as before.
    """

    method: str
    confidence: str
    eligibility: str
    anchor_x: Optional[float]
    anchor_y: Optional[float]
    roi_radius_m: Optional[float]
    requires_manual_review: bool

    @property
    def has_roi(self) -> bool:
        return (self.anchor_x is not None and self.anchor_y is not None
                and self.roi_radius_m is not None and self.roi_radius_m > 0)

    def contains(self, x: float, y: float) -> bool:
        """True when a point in the raster's own CRS lies inside the trusted ROI."""
        if not self.has_roi:
            return True
        return self.distance_from_anchor(x, y) <= self.roi_radius_m

    def distance_from_anchor(self, x: float, y: float) -> Optional[float]:
        if self.anchor_x is None or self.anchor_y is None:
            return None
        return ((x - self.anchor_x) ** 2 + (y - self.anchor_y) ** 2) ** 0.5


def read_experimental_policy(tags: dict) -> Optional[ExperimentalPolicy]:
    """Return the raster's declared experimental-georeferencing policy, or None if it declares none.

    Only a raster explicitly tagged ``georeferencingStatus=Estimated`` is restricted. Anything else
    -- including a properly surveyed GeoTIFF -- returns None and is processed unrestricted.
    """
    if not tags:
        return None
    if (tags.get("georeferencingStatus") or "").strip().lower() != "estimated":
        return None

    def number(key):
        try:
            return float(tags[key])
        except (KeyError, TypeError, ValueError):
            return None

    return ExperimentalPolicy(
        method=tags.get("provenance") or tags.get("georeferencingMethod") or "Estimated",
        confidence=tags.get("georeferencingConfidence") or "Low",
        eligibility=tags.get("geographicEvaluationEligible") or "ExperimentalOnly",
        anchor_x=number("anchorEasting"),
        anchor_y=number("anchorNorthing"),
        roi_radius_m=number("trustedRoiRadiusMetres"),
        requires_manual_review=(tags.get("requiresManualReview") or "true").strip().lower() != "false",
    )


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
                # Column/row vector lengths, not |a| and |e|: those two coincide with pixel size
                # only for an axis-aligned transform, and understate it once the affine carries a
                # rotation in its b/d terms.
                resolution=((transform.a ** 2 + transform.d ** 2) ** 0.5,
                            (transform.b ** 2 + transform.e ** 2) ** 0.5),
                tags=dict(dataset.tags() or {}),
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
