#!/usr/bin/env bash
# Runs what a built image carries (1.5.1: no shell in TonTuyau's images, so a program missing a library would only
# show when it runs). CI runs it on each image it builds, before publishing it, and so does the monthly rebuild.
#   scripts/smoke-image.sh backend tontuyau-backend:ci
#   scripts/smoke-image.sh frontend tontuyau-frontend:ci
set -euo pipefail

kind="${1:?backend or frontend}"
image="${2:?the image to run}"

case "$kind" in
  backend)
    docker run --rm --entrypoint /usr/local/bin/yt-dlp "$image" --version
    docker run --rm --entrypoint /usr/local/bin/deno "$image" --version
    docker run --rm --entrypoint /usr/local/bin/ffmpeg "$image" -hide_banner -version
    docker run --rm --entrypoint /usr/local/bin/ffprobe "$image" -hide_banner -version
    docker run --rm "$image" --version
    ;;
  frontend)
    docker run --rm "$image" --version
    docker run --rm "$image" -e "require('node:fs').accessSync('/app/server/main.ts')"
    ;;
  *)
    printf 'smoke-image: backend or frontend, not %s\n' "$kind" >&2
    exit 2
    ;;
esac
