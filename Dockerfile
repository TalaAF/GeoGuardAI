# syntax=docker/dockerfile:1.7

FROM --platform=$TARGETPLATFORM tala211/aerocompliance-api:2026.10.08-amd64 AS api
FROM --platform=$TARGETPLATFORM tala211/aerocompliance-processing:2026.10.08-amd64 AS processing
FROM --platform=$TARGETPLATFORM tala211/aerocompliance-ai-worker:2026.10.08-amd64 AS ai-worker
FROM --platform=$TARGETPLATFORM tala211/aerocompliance-frontend:2026.10.08-amd64 AS frontend
FROM --platform=$TARGETPLATFORM tala211/aerocompliance-minio:2025.09.07-amd64 AS minio

FROM --platform=$TARGETPLATFORM debian:trixie-slim

ARG TARGETARCH

LABEL org.opencontainers.image.title="AeroCompliance self-contained demo" \
      org.opencontainers.image.description="Single-container local demo with API, processing, AI worker, frontend, PostGIS, RabbitMQ and MinIO" \
      org.opencontainers.image.source="https://hub.docker.com/r/tala211/aerocompliance"

ENV DEBIAN_FRONTEND=noninteractive \
    AEROCOMPLIANCE_DATA_ROOT=/var/lib/aerocompliance \
    DOTNET_EnableDiagnostics=0 \
    PYTHONUNBUFFERED=1

RUN printf '#!/bin/sh\nexit 101\n' > /usr/sbin/policy-rc.d \
    && chmod +x /usr/sbin/policy-rc.d \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        bash ca-certificates curl libexpat1 libfontconfig1 libgcc-s1 libgl1 \
        libglib2.0-0 libgssapi-krb5-2 libicu76 libssl3t64 libstdc++6 libxcb1 \
        nginx openssl postgresql-17 postgresql-17-postgis-3 rabbitmq-server supervisor tzdata zlib1g \
    && rm -f /usr/sbin/policy-rc.d \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --home-dir /var/lib/aerocompliance/app --shell /usr/sbin/nologin aerocompliance \
    && rm -rf /var/lib/postgresql/* /var/lib/rabbitmq /var/lib/nginx /var/log/nginx /var/log/postgresql

COPY --from=api /usr/share/dotnet /usr/share/dotnet
RUN ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet

COPY --from=api /app /opt/aerocompliance/api
COPY --from=processing /app /opt/aerocompliance/processing
COPY --from=ai-worker /usr/local /usr/local
COPY --from=ai-worker /app/aerocompliance_ai /opt/aerocompliance/ai/aerocompliance_ai
COPY --from=frontend /usr/share/nginx/html /opt/aerocompliance/frontend
COPY --from=minio /usr/local/bin/minio /usr/local/bin/minio

COPY deploy/single-container/supervisord.conf /etc/supervisor/supervisord.conf
COPY deploy/single-container/nginx.conf /etc/nginx/nginx.conf
COPY deploy/single-container/entrypoint.sh /usr/local/bin/aerocompliance-entrypoint
COPY deploy/single-container/run-api.sh /opt/aerocompliance/bin/run-api
COPY deploy/single-container/run-processing.sh /opt/aerocompliance/bin/run-processing
COPY deploy/single-container/run-ai-worker.sh /opt/aerocompliance/bin/run-ai-worker
COPY deploy/single-container/run-postgres.sh /opt/aerocompliance/bin/run-postgres
COPY deploy/single-container/run-rabbitmq.sh /opt/aerocompliance/bin/run-rabbitmq
COPY deploy/single-container/wait-for-stack.sh /opt/aerocompliance/bin/wait-for-stack
COPY deploy/single-container/healthcheck.sh /opt/aerocompliance/bin/healthcheck

RUN chmod 0755 \
        /usr/local/bin/aerocompliance-entrypoint \
        /opt/aerocompliance/bin/run-api \
        /opt/aerocompliance/bin/run-processing \
        /opt/aerocompliance/bin/run-ai-worker \
        /opt/aerocompliance/bin/run-postgres \
        /opt/aerocompliance/bin/run-rabbitmq \
        /opt/aerocompliance/bin/wait-for-stack \
        /opt/aerocompliance/bin/healthcheck \
    && install -d -o aerocompliance -g aerocompliance /var/lib/aerocompliance

EXPOSE 8080
VOLUME ["/var/lib/aerocompliance"]
STOPSIGNAL SIGTERM
HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=5 \
    CMD ["/opt/aerocompliance/bin/healthcheck"]
ENTRYPOINT ["/usr/local/bin/aerocompliance-entrypoint"]
