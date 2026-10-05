FROM python:3.12-slim
WORKDIR /app

# Three explicit dependency profiles (see the AI Worker README's "Dependency profiles" section):
#
#   requirements.txt              core/mock only — no rasterio/ultralytics/PyTorch/CUDA/.pt file.
#   requirements-geospatial.txt   core + rasterio/shapely/pyproj — GeoTIFF metadata extraction and
#                                  pixel->EPSG:4326 conversion. Still no ML framework or model.
#   requirements-yolo.txt         geospatial + ultralytics (pulls PyTorch) — real YOLO inference.
#
# Select one with --build-arg AI_REQUIREMENTS=<file>. The profile only controls which *libraries*
# are installed, never which inference path actually runs (that's AI_MODE, a separate variable).
ARG AI_REQUIREMENTS=requirements.txt

# System libraries:
#  * libexpat1 — rasterio's manylinux wheel dlopen's it at import time.
#  * libxcb1/libgl1/libglib2.0-0 — OpenCV, pulled in by ultralytics, links against these at import
#    even for headless use; without libxcb.so.1 `import ultralytics` fails outright.
# Installed unconditionally (a few hundred KB) so one base layer serves every profile.
RUN apt-get update && apt-get install -y --no-install-recommends \
        libexpat1 libxcb1 libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt requirements-geospatial.txt requirements-yolo.txt ./

# CPU-only PyTorch. The default PyPI wheel bundles the CUDA runtime (multiple GB) which this
# service never uses — development and CI run inference on CPU. Installing torch from PyTorch's
# CPU index first means the later ultralytics install finds its torch requirement already
# satisfied and does not pull the CUDA build. Skipped entirely for the non-YOLO profiles.
RUN if [ "${AI_REQUIREMENTS}" = "requirements-yolo.txt" ]; then \
        pip install --no-cache-dir --index-url https://download.pytorch.org/whl/cpu \
            "torch>=2.4,<3" "torchvision>=0.19,<1" ; \
    fi

RUN pip install --no-cache-dir -r ${AI_REQUIREMENTS}
COPY aerocompliance_ai ./aerocompliance_ai
ENV PYTHONUNBUFFERED=1 AI_MODE=mock HEALTH_PORT=8080
EXPOSE 8080
CMD ["python", "-m", "aerocompliance_ai.main"]
