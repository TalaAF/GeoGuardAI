FROM python:3.12-slim
WORKDIR /app

# Three explicit dependency profiles (see the AI Worker README's "Dependency profiles" section):
#
#   requirements.txt              core/mock only — no rasterio/ultralytics/PyTorch/CUDA/.pt file.
#   requirements-geospatial.txt   core + rasterio/shapely/pyproj — GeoTIFF metadata extraction and
#                                  pixel->EPSG:4326 conversion. Still no ML framework or model.
#   requirements-yolo.txt         geospatial + ultralytics (pulls PyTorch) — real YOLO inference.
#
# Select one with --build-arg AI_REQUIREMENTS=<file>. AppHost.cs builds the "geospatial" profile by
# default so a normal local run can read GeoTIFF georeferencing even though AI_MODE stays "mock" at
# runtime — the profile only controls which *libraries* are installed, never which inference path
# actually runs (that's AI_MODE, a separate environment variable).
ARG AI_REQUIREMENTS=requirements.txt

# rasterio's manylinux wheel dynamically loads libexpat at import time, which python:3.12-slim does
# not ship; a no-op for the core profile, required for geospatial/yolo. Installed unconditionally
# (~200KB) so the same base layer works for every profile.
RUN apt-get update && apt-get install -y --no-install-recommends libexpat1 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt requirements-geospatial.txt requirements-yolo.txt ./
RUN pip install --no-cache-dir -r ${AI_REQUIREMENTS}
COPY aerocompliance_ai ./aerocompliance_ai
ENV PYTHONUNBUFFERED=1 AI_MODE=mock HEALTH_PORT=8080
EXPOSE 8080
CMD ["python", "-m", "aerocompliance_ai.main"]
