#!/usr/bin/env bash
# A stand-in for the image's yt-dlp_linux (alpha.6): a PyInstaller program unpacks itself into a new _MEI<pid> folder
# of its TMPDIR at each start, and removes it only when it ends by itself (a kill skips the trap). Where it unpacked is
# kept in unpacked.path for the tests. A channel's tab lists three entries, then goes on (a long channel) until it's
# stopped.
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
unpacked="${TMPDIR:-/tmp}/_MEI$$"
mkdir -p "$unpacked"
echo "$unpacked" > "$here/unpacked.path"
trap 'rm -rf "$unpacked"' EXIT
if [[ "${@: -1}" =~ /channel/[^/]+/videos$ ]]; then
  echo '{"id": "dQw4w9WgXcQ", "live_status": "not_live", "title": "One"}'
  echo '{"id": "k3Fq8xZ0aQw", "live_status": "not_live", "title": "Two"}'
  echo '{"id": "alpha-01___", "live_status": "not_live", "title": "Three"}'
  sleep 60
fi
exit 0
