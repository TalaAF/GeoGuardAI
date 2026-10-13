# AeroCompliance single-container demo

This image contains the complete local demo stack and needs only Docker:

```sh
docker pull tala211/aerocompliance:2026.10.09-demo-amd64
docker run -d --name aerocompliance --restart unless-stopped \
  -p 8080:8080 \
  -v aerocompliance-data:/var/lib/aerocompliance \
  tala211/aerocompliance:2026.10.09-demo-amd64
docker logs aerocompliance
```

Open http://localhost:8080. The first startup generates and persists settings under
`/var/lib/aerocompliance`; `docker logs aerocompliance` prints the generated demo login. Stop and
restart without losing data using `docker stop aerocompliance` and `docker start aerocompliance`.

The default AI mode is clearly logged as **MOCK** and is not model-accuracy evidence. The image has
the real YOLO runtime but not the project-specific weights. To enable it, copy `best.pt` to
`/var/lib/aerocompliance/models/best.pt` in the named volume and recreate the container with
`-e AEROCOMPLIANCE_AI_MODE=yolo`. The image targets Linux AMD64.
