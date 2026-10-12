#!/usr/bin/env bash
# The compose file must render (valid YAML, every required variable named): checked with .env.example plus dummy
# values for the secrets and a temporary media folder, so a broken docker/docker-compose.yml fails the build.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir "$work/media"
sed -e "s|^TONTUYAU_MEDIA_PATH=.*|TONTUYAU_MEDIA_PATH=$work/media|" \
    -e 's|^TONTUYAU_ES_PASSWORD=.*|TONTUYAU_ES_PASSWORD=check-compose|' \
    -e 's|^TONTUYAU_TA_TOKEN=.*|TONTUYAU_TA_TOKEN=check-compose|' \
    -e 's|^TONTUYAU_DB_PASSWORD=.*|TONTUYAU_DB_PASSWORD=check-compose|' \
    "$root/.env.example" > "$work/env"

docker compose --env-file "$work/env" -f "$root/docker/docker-compose.yml" config --quiet
printf 'docker/docker-compose.yml renders with .env.example\n'
