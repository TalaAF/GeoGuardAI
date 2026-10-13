#!/usr/bin/env bash
set -Eeuo pipefail

pg_isready -h 127.0.0.1 -p 5432 -U aerocompliance -d aerocompliance >/dev/null
bash -c 'exec 3<>/dev/tcp/127.0.0.1/5672'
curl --fail --silent http://127.0.0.1:9000/minio/health/live >/dev/null
curl --fail --silent http://127.0.0.1:8081/health >/dev/null
curl --fail --silent http://127.0.0.1:8082/ready >/dev/null
curl --fail --silent http://127.0.0.1:8080/health >/dev/null
