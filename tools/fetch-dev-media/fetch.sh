#!/usr/bin/env bash
# Copies a few real videos (+ metadata + images) from TubeArchivist into dev-data/, laid out like TA's volumes:
#   dev-data/youtube/<channel_id>/<video_id>.mp4      (simulated NFS share)
#   dev-data/cache/videos/<c>/<video_id>.jpg           (TA cache: thumbnails)
#   dev-data/cache/channels/<channel_id>_<kind>.jpg    (TA cache: channel art)
#   dev-data/ta/videos/<video_id>.json, dev-data/ta/channels/<channel_id>.json
#
# Read-only: GET requests only (curl without -X/-d). Re-running skips files already complete.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out="$root/dev-data"
list="${1:-$root/tools/fetch-dev-media/videos.txt}"

set -a; . "$root/.env"; set +a
: "${TONTUYAU_TA_URL:?missing in .env}" "${TONTUYAU_TA_TOKEN:?missing in .env}"

get() { curl -fsS --retry 3 -H "Authorization: Token $TONTUYAU_TA_TOKEN" "$TONTUYAU_TA_URL$1"; }

download() { # <ta path> <destination> [expected size]
  local src="$1" dst="$2" size="${3:-}"
  if [[ -f "$dst" && ( -z "$size" || "$(stat -c %s "$dst")" == "$size" ) ]]; then return; fi
  mkdir -p "$(dirname "$dst")"
  # Paths come from TA's own *_url fields, but never write outside dev-data/.
  [[ "$(realpath -m "$dst")" == "$out"/* ]] || { echo "refusing to write outside dev-data: $dst" >&2; exit 1; }
  get "$src" > "$dst.part" && mv "$dst.part" "$dst"
  if [[ -n "$size" && "$(stat -c %s "$dst")" != "$size" ]]; then echo "size mismatch for $dst" >&2; exit 1; fi
}

mkdir -p "$out/ta/videos" "$out/ta/channels"
grep -vE '^\s*(#|$)' "$list" | while read -r id; do
  json="$out/ta/videos/$id.json"
  get "/api/video/$id/" > "$json"
  media=$(jq -r .media_url "$json"); thumb=$(jq -r .vid_thumb_url "$json"); size=$(jq -r .media_size "$json")
  channel=$(jq -r .channel.channel_id "$json")
  echo "$id  $(jq -r '.vid_type' "$json")  $((size / 1048576)) MB  $media"
  download "$media" "$out$media" "$size"
  download "$thumb" "$out$thumb"
  if [[ ! -f "$out/ta/channels/$channel.json" ]]; then
    get "/api/channel/$channel/" > "$out/ta/channels/$channel.json"
    for kind in thumb banner tvart; do
      art=$(jq -r ".channel_${kind}_url // empty" "$out/ta/channels/$channel.json")
      [[ -n "$art" ]] && { download "$art" "$out$art" || echo "  (no $kind for $channel)"; }
    done
  fi
done
echo "done: $(du -sh "$out" | cut -f1) in $out"
