# AeroCompliance single-container demo

After the multi-architecture image is published, the complete local demo
stack needs only Docker:

```sh
docker pull tala211/aerocompliance:latest
docker run -d --name aerocompliance --restart unless-stopped \
  -p 8080:8080 \
  -v aerocompliance-data:/var/lib/aerocompliance \
  tala211/aerocompliance:latest
docker logs aerocompliance
```

Open http://localhost:8080. The first startup generates and persists settings under
`/var/lib/aerocompliance`; `docker logs aerocompliance` prints the generated demo login. Stop and
restart without losing data using `docker stop aerocompliance` and `docker start aerocompliance`.

On a **new empty volume**, wait for Docker to report `healthy`. First startup then contains
four fictional aerial images, three completed sample flights, a planned follow-up flight,
mock building and road detections, two candidates awaiting review, and a labelled sample
PDF report. The images and coordinates are synthetic examples, not municipal source data
or evidence of AI accuracy. Review the pending candidates in the frontend to practice
confirmation or dismissal; the sample report demonstrates the normal report workflow.
The first-start marker is stored in the same data volume, so restarting or updating the
image preserves credentials, reviews, reports, and other user data. Existing volumes are
not backfilled with this scenario.

The default AI mode is clearly logged as **MOCK** and is not model-accuracy evidence. The image has
the real YOLO runtime but not the project-specific weights. To enable it, copy `best.pt` to
`/var/lib/aerocompliance/models/best.pt` in the named volume and recreate the container with
`-e AEROCOMPLIANCE_AI_MODE=yolo`. The build below publishes `latest` and
`2026.10.14-demo-scenario-multiarch` for Linux AMD64 and ARM64. It does not modify the
existing `2026.10.09-demo-amd64` tag.

## Build and publish the multi-architecture image

Run from the repository root with Docker Buildx and Docker Hub login configured:

```sh
docker buildx create --name aerocompliance-multiarch --driver docker-container --use
docker buildx inspect --bootstrap
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t tala211/aerocompliance:2026.10.14-demo-scenario-multiarch \
  --push .
docker buildx imagetools inspect tala211/aerocompliance:2026.10.14-demo-scenario-multiarch
```

The inspection must list `linux/amd64` and `linux/arm64`. Buildx may add
`unknown/unknown` attestation entries; these are metadata, not extra runtime
architectures. Test the versioned image on native ARM64 with a fresh volume,
including review and PDF download, before promoting it to `latest`:

```sh
docker buildx imagetools create \
  -t tala211/aerocompliance:latest \
  tala211/aerocompliance:2026.10.14-demo-scenario-multiarch
docker buildx imagetools inspect tala211/aerocompliance:latest
```

Do not retag or push `2026.10.09-demo-amd64`.

Smoke test the published image on an AMD64 or ARM64 Docker host:

```sh
docker pull tala211/aerocompliance:latest
docker run -d --name aerocompliance-smoke -p 8080:8080 \
  -v aerocompliance-smoke-data:/var/lib/aerocompliance \
  tala211/aerocompliance:latest
docker inspect --format '{{.State.Health.Status}}' aerocompliance-smoke
curl --fail http://localhost:8080/health
curl --fail http://localhost:8080/
docker logs aerocompliance-smoke
```

Wait for Docker to report `healthy` before checking the application. On an
ARM64 host, this smoke test exercises the ARM64 image automatically, without
`--platform`. Stop and remove the smoke container when finished with
`docker rm -f aerocompliance-smoke`; the named data volume is retained.
