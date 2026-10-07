#!/usr/bin/env bash
# Builds both images locally, as CI does (linux/amd64), tagged ghcr.io/eclegv/tontuyau-{backend,frontend}:<tag>.
#   scripts/build-images.sh          → tag "dev"
#   scripts/build-images.sh 1.0.0    → tag "1.0.0"
# Run the stack on them with TONTUYAU_VERSION=<tag>. CI publishes the real ones (.github/workflows/ci.yml).
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tag="${1:-dev}"
version="$("$root/scripts/check-version.sh")"
revision="$(git -C "$root" rev-parse HEAD)"

for image in backend frontend; do
  printf '\n\033[1;34m==> ghcr.io/eclegv/tontuyau-%s:%s\033[0m\n' "$image" "$tag"
  docker build \
    --platform linux/amd64 \
    --file "$root/docker/$image.Dockerfile" \
    --build-arg "VERSION=$version" \
    --build-arg "REVISION=$revision" \
    --tag "ghcr.io/eclegv/tontuyau-$image:$tag" \
    "$root"
done
