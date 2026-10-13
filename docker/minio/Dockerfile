FROM --platform=$BUILDPLATFORM golang:1.24-bookworm AS build

ARG TARGETARCH
ARG MINIO_RELEASE=RELEASE.2025-09-07T16-13-09Z

ENV CGO_ENABLED=0 \
    GOOS=linux

RUN GOARCH="${TARGETARCH}" go install -tags kqueue -trimpath \
    --ldflags "-s -w \
      -X github.com/minio/minio/cmd.Version=2025-09-07T16:13:09Z \
      -X github.com/minio/minio/cmd.CopyrightYear=2025 \
      -X github.com/minio/minio/cmd.ReleaseTag=${MINIO_RELEASE} \
      -X github.com/minio/minio/cmd.CommitID=07c3a429bfed \
      -X github.com/minio/minio/cmd.ShortCommitID=07c3a429bfed" \
    "github.com/minio/minio@${MINIO_RELEASE}"

FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /go/bin/linux_amd64/minio /usr/local/bin/minio

EXPOSE 9000 9001
ENTRYPOINT ["/usr/local/bin/minio"]
