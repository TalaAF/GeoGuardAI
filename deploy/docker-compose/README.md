# AeroCompliance Docker Compose

Requirements: Docker Engine or Docker Desktop using Linux AMD64 containers, with Docker Compose v2.

1. Download `compose.yaml` and `.env.example` into the same directory.
2. Copy `.env.example` to `.env` and replace every `change-me` value.
3. Run:

   ```sh
   docker compose pull
   docker compose up -d
   ```

4. Open http://localhost:8080 and sign in as `admin@aerocompliance.test` with `DEMO_PASSWORD` from
   `.env`.

Data persists in named PostgreSQL, RabbitMQ and MinIO volumes. Stop without deleting data using
`docker compose down`.

The default `AEROCOMPLIANCE_AI_MODE=mock` is deterministic demonstration output, not model-accuracy
evidence. For real YOLO inference, create the directory configured by `AEROCOMPLIANCE_MODEL_DIR`, put
the approved weights at `best.pt`, set `AEROCOMPLIANCE_AI_MODE=yolo`, and run `docker compose up -d`
again. The model is mounted read-only and is not included in the image.

These images target `linux/amd64`. They were built and checked under AMD64 emulation on an ARM64
Docker host; the complete workflow has not been verified on a native AMD64 machine.
