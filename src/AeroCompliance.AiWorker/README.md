# AeroCompliance AI Worker

Runs in `AI_MODE=mock` by default and consumes RabbitMQ processing requests. Health endpoints are
`/health` and `/ready` on port 8080.

## Dependency profiles

Three explicit profiles, selected by the `AI_REQUIREMENTS` Docker build arg (default
`requirements.txt`):

| Profile file | Layers on | Adds | Needs a `.pt` file? | Needs CUDA? |
|---|---|---|---|---|
| `requirements.txt` (**core/mock**) | — | pika, minio, flask | No | No |
| `requirements-geospatial.txt` | core | `rasterio`, `shapely`, `pyproj` | No | No |
| `requirements-yolo.txt` | geospatial | `ultralytics` (pulls PyTorch) | Only to actually run inference | Not required to build/import; used automatically if a GPU is present |

Each file only lists what it *adds* over the one before it (`-r <previous>.txt` at the top) — no
duplicated version pins across files.

**AppHost builds the `geospatial` profile by default**
(`.WithBuildArg("AI_REQUIREMENTS", "requirements-geospatial.txt")` in `AppHost.cs`), so a normal
local run gets real GeoTIFF metadata extraction without ever needing PyTorch/CUDA or a model file.
`AI_MODE` is a separate, runtime environment variable and stays `mock` regardless of which profile
was built — the profile only controls which *libraries exist in the image*.

To switch profiles, change that one `WithBuildArg` line in `AppHost.cs`:

- **`"requirements.txt"`** — smallest/fastest image; mock detection only, no GeoTIFF raster metadata
  (a `.tif` upload still completes in mock mode, just with `SourceRasterCrs`/`RasterWidth`/
  `RasterHeight`/`AffineTransform` left `null`).
- **`"requirements-geospatial.txt"`** — the default; adds real GeoTIFF metadata extraction.
- **`"requirements-yolo.txt"`** — geospatial + real YOLO inference once a `.pt` model is supplied and
  `AI_MODE=yolo` is set (see "Real model handoff" in the root README).

Building directly with plain `docker`:

```powershell
docker build --build-arg AI_REQUIREMENTS=requirements-geospatial.txt -t aerocompliance-ai-worker .
```

**Verified in this workspace**: the `core` and `geospatial` images both build cleanly (core ~16s,
~260 MB; geospatial ~35s, ~575 MB) and were run directly (`docker run --entrypoint python ...`) to
confirm `HAS_RASTERIO`/`HAS_SHAPELY` are `True` only where expected, and that `ultralytics`/`torch`
are absent from both. The `yolo` profile also builds successfully but is large (~10 GB) and slow
(~15 minutes, dominated by downloading PyTorch/CUDA packages) — this was built once to confirm it
completes and to verify the raster→`EPSG:4326` conversion utilities inside that exact image against a
synthetic georeferenced GeoTIFF, then deleted (a 10 GB image isn't worth keeping locally). It was not
kept running and no real model or real inference was run against it.

The Dockerfile also installs `libexpat1` unconditionally (a small `apt-get` layer) — rasterio's wheel
dynamically loads it at import time, and `python:3.12-slim` does not ship it. Without this,
`import rasterio` raises `ImportError: libexpat.so.1: cannot open shared object file`, which
`geospatial.py`'s `try/except ImportError` catches silently (`HAS_RASTERIO=False`) — the worker would
still start and mock-only processing would still work, it would just silently lose GeoTIFF metadata
extraction. This was found by actually building and running the geospatial image, not by inspection.

## Metadata extraction

Independent of `AI_MODE`, whenever the source object is a `.tif`/`.tiff` the worker attempts to read
its **raster georeferencing** with `rasterio` (`aerocompliance_ai/geospatial.py`) — present whenever
the image was built with the `geospatial` or `yolo` profile:

- CRS, affine transform, width, height, bounds, and pixel resolution.
- A raster with no CRS, no transform, or only a default identity transform raises
  `MISSING_GEOREFERENCING`; a transform/bounds containing non-finite values raises
  `INVALID_GEOREFERENCING`. Neither is invented or silently ignored.

In **`AI_MODE=mock`** this is best-effort only: a missing/invalid raster (or `rasterio` simply not
being installed, on the `core` profile) is logged and simply means no raster metadata is attached to
the `image.analysis.completed.v1` event — it never fails mock processing (mock detections don't need
it). In **`AI_MODE=yolo`**, `YoloModelRunner.analyze()` requires it and fails the image
(`AnalysisFailed`, retryable) if it's missing or invalid.

**A GeoTIFF's raster georeferencing is not the same fact as a JPEG's EXIF GPS tag.** EXIF GPS locates
the camera/drone at one approximate point; a GeoTIFF's CRS + affine transform georeferences every
pixel precisely enough to convert a detection mask into map coordinates. `ProcessingService` tracks
these with different provenance values (`"GeoTiff"` vs. `"Exif"`) on the `Image` row rather than
treating them as equivalent — see the root README's "Automatic metadata extraction" section.

**Verified through the full running system** (real HTTP upload → MinIO → Outbox → RabbitMQ → this
worker, built with the `geospatial` profile, running under AppHost → `ProcessingService` →
PostgreSQL): a genuinely georeferenced synthetic GeoTIFF's CRS (`EPSG:32636`), width, height, and
affine transform were correctly read and persisted onto the `Image` row; a non-georeferenced TIFF
completed processing safely with those four fields left `null` (not invented, not a crash); no image
with a missing location ever received a default/invented coordinate (checked directly in
PostgreSQL). Reprocessing the same image is still governed by the pre-existing Inbox/Outbox and
unique-constraint idempotency in `ProcessingService` and `ImagesController` — unchanged by this work.

## Real YOLO — what is and is not done here

`YoloModelRunner` (`aerocompliance_ai/model.py`) lazily imports `ultralytics` and loads
`AI_MODEL_PATH` (default `/models/model.pt`) only when `AI_MODE=yolo` actually calls `analyze()`.
When it runs, it requires the `yolo` profile (for `ultralytics`) and a genuinely georeferenced
GeoTIFF, runs the model, and converts each detection's pixel-space mask (or bounding box, if the
model has no segmentation head) into an `EPSG:4326` `MultiPolygon` via
`geospatial.polygon_pixels_to_wgs84` / `build_multipolygon_geojson`, then returns detections in the
exact shape `MockModelRunner` already produces — so no .NET contract changes are needed to switch.

**This has not been run against a real model or a real drone GeoTIFF in this workspace** — there is
no `.pt` file and no representative production GeoTIFF here. The georeferencing/conversion path it
depends on (`geospatial.py`) has been verified directly (see above); `YoloModelRunner`'s own model
loading and inference call have not. Treat it as structurally complete but functionally unverified
until a teammate exercises it end to end with a real model and a real image. Remaining steps for that
handoff:

1. Build the `yolo` profile: `--build-arg AI_REQUIREMENTS=requirements-yolo.txt`.
2. Mount/copy the trained `.pt` file to the path in `AI_MODEL_PATH` (default `/models/model.pt`).
3. Set `AI_MODE=yolo` on the `aiworker` resource (`AppHost.cs`), plus `AI_CONFIDENCE_THRESHOLD` /
   `AI_MODEL_NAME` / `AI_MODEL_VERSION` as needed.
4. Confirm class IDs/names match what `ProcessingService.Evaluate` expects (`"building"` or
   `"violation"` today — extend that check for more classes).
5. Upload a real, representative georeferenced GeoTIFF and confirm `GET /api/detections` /
   `GET /api/violations` show real output, not just that the pipeline didn't crash.
