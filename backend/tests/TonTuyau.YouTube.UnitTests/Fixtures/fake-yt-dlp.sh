#!/usr/bin/env bash
# A stand-in for yt-dlp in the tests: records its arguments, then answers from the fixture files next to it,
# by the id at the end of the URL: <id>.json on stdout (exit 0), or <id>.err on stderr (exit 1); <id>.sleep
# holds seconds to wait first.
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
printf '%s\n' "$*" >> "$here/calls.log"
if [[ "$*" == *--version* ]]; then
  echo "2026.08.19"
  exit 0
fi
url="${@: -1}"
id="${url##*[=/]}"
if [[ -f "$here/$id.sleep" ]]; then
  sleep "$(cat "$here/$id.sleep")"
fi
if [[ -f "$here/$id.json" ]]; then
  cat "$here/$id.json"
  exit 0
fi
if [[ -f "$here/$id.err" ]]; then
  cat "$here/$id.err" >&2
fi
exit 1
