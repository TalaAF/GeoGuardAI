FROM python:3.12-slim
WORKDIR /app
# Default build installs the lightweight, mock-only dependency set (no rasterio/ultralytics, so the
# image stays small and starts without a model or GDAL). Pass
# --build-arg AI_REQUIREMENTS=requirements-yolo.txt to build the raster+YOLO-capable image instead
# (see the AI Worker README's "Real model handoff" section) — nothing else about this Dockerfile,
# or AppHost.cs's default AI_MODE=mock, changes.
ARG AI_REQUIREMENTS=requirements.txt
# rasterio's manylinux wheel dynamically loads libexpat at import time, which python:3.12-slim does
# not ship; without it `import rasterio` fails (caught as ImportError -> HAS_RASTERIO=False, so the
# lightweight build still runs fine either way, but the raster/YOLO-capable build needs it to
# actually work). Installed unconditionally since it's ~200KB and used by Python's stdlib anyway.
RUN apt-get update && apt-get install -y --no-install-recommends libexpat1 \
    && rm -rf /var/lib/apt/lists/*
COPY requirements.txt requirements-yolo.txt ./
RUN pip install --no-cache-dir -r ${AI_REQUIREMENTS}
COPY aerocompliance_ai ./aerocompliance_ai
ENV PYTHONUNBUFFERED=1 AI_MODE=mock HEALTH_PORT=8080
EXPOSE 8080
CMD ["python", "-m", "aerocompliance_ai.main"]
