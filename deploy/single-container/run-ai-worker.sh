#!/usr/bin/env bash
set -Eeuo pipefail
/opt/aerocompliance/bin/wait-for-stack api
cd /opt/aerocompliance/ai
exec python -m aerocompliance_ai.main
