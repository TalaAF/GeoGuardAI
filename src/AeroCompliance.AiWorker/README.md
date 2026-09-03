# AeroCompliance AI Worker

Runs in `AI_MODE=mock` by default and consumes RabbitMQ processing requests. The default image has
no ML framework or model dependency. Health endpoints are `/health` and `/ready` on port 8080.

## Metadata extraction (Phase 5)

Independent of `AI_MODE`, whenever the source object is a `.tif`/`.tiff` the worker attempts to read
its **raster georeferencing** with `rasterio` (`aerocompliance_ai/geospatial.py`):

- CRS, affine transform, width, height, bounds, and pixel resolution.
- A raster with no CRS, no transform, or only a default identity transform raises
  `MISSING_GEOREFERENCING`; a transform/bounds containing non-finite values raises
  `INVALID_GEOREFERENCING`. Neither is invented or silently ignored.

In **`AI_MODE=mock`** this is best-effort only: a missing/invalid raster is logged and simply means no
raster metadata is attached to the `image.analysis.completed.v1` event — it never fails mock
processing (mock detections don't need it). In **`AI_MODE=yolo`**, `YoloModelRunner.analyze()`
requires it and fails the image (`AnalysisFailed`, retryable) if it's missing or invalid.

`rasterio` (and `shapely`, used only to validate/repair a polygon's geometry) are **optional**
dependencies — see `requirements-yolo.txt` — so the default lightweight image keeps starting and
processing mock requests without either installed.

## Building the raster/YOLO-capable image

The default `docker build .` installs only `requirements.txt` (lightweight, mock-only, no GDAL). To
build an image that can also read GeoTIFF georeferencing and (once a model is supplied) run real
YOLO inference, pass the `AI_REQUIREMENTS` build arg:

```powershell
docker build --build-arg AI_REQUIREMENTS=requirements-yolo.txt -t aerocompliance-ai-worker .
```

Or, from `AppHost.cs`, on the `aiworker` container resource:

```csharp
.WithBuildArg("AI_REQUIREMENTS", "requirements-yolo.txt")
```

This does **not** change `AI_MODE` — that is a separate, runtime environment variable and stays
`mock` by default either way (see the root README's "AI modes" and "Real model handoff" sections).

## Real YOLO — what is and is not done here

`YoloModelRunner` (`aerocompliance_ai/model.py`) lazily imports `ultralytics` and loads
`AI_MODEL_PATH` only when `AI_MODE=yolo` actually calls `analyze()`. When it runs, it requires a
genuinely georeferenced GeoTIFF, runs the model, and converts each detection's pixel-space mask (or
bounding box, if the model has no segmentation head) into an EPSG:4326 `MultiPolygon` via
`geospatial.polygon_pixels_to_wgs84` / `build_multipolygon_geojson`, then returns detections in the
exact shape `MockModelRunner` already produces — so no .NET contract changes are needed to switch.

**This has not been run against a real model or a real georeferenced image in this workspace** — there
is no `.pt` file and no representative test GeoTIFF here. Treat `YoloModelRunner` as structurally
complete but functionally unverified until a teammate exercises it end to end with both.
