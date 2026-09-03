import os
import uuid

from . import geospatial


class ModelFailure(Exception):
    def __init__(self, code: str, message: str): super().__init__(message); self.code = code


class MockModelRunner:
    def analyze(self, request: dict, image_path: str | None = None) -> list[dict]:
        # image_path is accepted (and ignored) for interface parity with YoloModelRunner — the mock
        # never inspects file bytes, only the request's own captured coordinates/scenario.
        scenario = (request.get("mockScenario") or "none").lower()
        if scenario == "failure": raise ModelFailure("MOCK_FAILURE", "The requested deterministic mock failure occurred.")
        if scenario == "none": return []
        lon, lat = request.get("longitude"), request.get("latitude")
        if lon is None or lat is None: raise ModelFailure("MISSING_CAPTURE_COORDINATES", "Geographic mock scenarios require capture longitude and latitude.")
        delta = 0.00008
        ring = [[lon-delta,lat-delta],[lon+delta,lat-delta],[lon+delta,lat+delta],[lon-delta,lat+delta],[lon-delta,lat-delta]]
        detection_id = str(uuid.uuid5(uuid.UUID(request["requestId"]), "detection:0"))
        return [{"detectionId": detection_id, "classId": 0, "className": "building", "confidence": 0.93,
                 "geometry": {"type": "MultiPolygon", "coordinates": [[[p for p in ring]]]}}]


class YoloModelRunner:
    """Loads a YOLO/Ultralytics model lazily and runs segmentation inference on a georeferenced
    GeoTIFF, converting pixel-space masks into canonical EPSG:4326 detections via `geospatial`.

    Division of responsibility (see the AI Worker README):
      * The teammate supplies and validates the trained `.pt` model, confirms its class list, and
        provides representative georeferenced GeoTIFF test images.
      * This class loads the model, reads the raster's georeferencing, converts its pixel-space
        output into EPSG:4326, and returns detections in the same shape `MockModelRunner` already
        produces — no downstream (.NET) contract changes are needed to swap mock for real inference.

    NOT VERIFIED: no trained `.pt` model or representative georeferenced GeoTIFF has been available
    in this workspace. This is a structurally-complete implementation against Ultralytics' documented
    segmentation API, exercised only by code review — not by an actual inference run. Treat every
    code path here as unverified until a teammate runs it end to end.
    """

    def __init__(self, settings):
        self.settings = settings
        self._model = None  # Lazy: only import/load ultralytics the first time analyze() needs it.

    def _load(self):
        if self._model is not None:
            return self._model
        try:
            from ultralytics import YOLO
        except ImportError as exc:
            raise RuntimeError(
                "AI_MODE=yolo requires the ultralytics package. Rebuild the worker image with "
                "requirements-yolo.txt (see the AI Worker README) before selecting this mode."
            ) from exc
        if not os.path.isfile(self.settings.model_path):
            raise RuntimeError(
                f"AI_MODEL_PATH does not point to an existing file: {self.settings.model_path}. "
                "Mount/copy the teammate's .pt weights there before selecting AI_MODE=yolo."
            )
        self._model = YOLO(self.settings.model_path)
        return self._model

    def analyze(self, request: dict, image_path: str | None = None) -> list[dict]:
        if image_path is None:
            raise ModelFailure("MISSING_IMAGE", "No local image file was provided to the YOLO runner.")
        if not geospatial.HAS_RASTERIO:
            raise ModelFailure(
                "RASTERIO_UNAVAILABLE",
                "AI_MODE=yolo requires rasterio for georeferencing. Rebuild the worker image with "
                "requirements-yolo.txt (see the AI Worker README) before selecting this mode.",
            )
        try:
            raster = geospatial.extract_raster_metadata(image_path)
        except geospatial.GeoreferencingError as exc:
            raise ModelFailure(exc.code, str(exc)) from exc

        model = self._load()
        results = model.predict(image_path, conf=float(self.settings.threshold), verbose=False)
        if not results:
            return []

        result = results[0]
        boxes = result.boxes
        masks = result.masks
        if boxes is None or len(boxes) == 0:
            return []
        names = getattr(result, "names", {}) or {}

        detections: list[dict] = []
        for index in range(len(boxes)):
            class_id = int(boxes.cls[index].item())
            confidence = float(boxes.conf[index].item())
            class_name = str(names.get(class_id, str(class_id)))

            if masks is not None and index < len(masks.xy):
                pixel_ring = [(float(x), float(y)) for x, y in masks.xy[index]]
            else:
                # No segmentation mask for this detection (a detect-only model/head) — fall back to
                # its bounding box rather than dropping the detection.
                x1, y1, x2, y2 = (float(v) for v in boxes.xyxy[index])
                pixel_ring = [(x1, y1), (x2, y1), (x2, y2), (x1, y2)]

            if len(pixel_ring) < 3:
                continue  # Degenerate mask/box for this one detection — skip only it.
            if pixel_ring[0] != pixel_ring[-1]:
                pixel_ring = [*pixel_ring, pixel_ring[0]]

            wgs84_rings = geospatial.polygon_pixels_to_wgs84(raster.affine_transform, raster.crs, [pixel_ring])
            geometry = geospatial.build_multipolygon_geojson([wgs84_rings])
            if geometry is None:
                continue

            detections.append({
                "detectionId": str(uuid.uuid5(uuid.UUID(request["requestId"]), f"detection:{index}")),
                "classId": class_id,
                "className": class_name,
                "confidence": round(confidence, 4),
                "geometry": geometry,
            })
        return detections
