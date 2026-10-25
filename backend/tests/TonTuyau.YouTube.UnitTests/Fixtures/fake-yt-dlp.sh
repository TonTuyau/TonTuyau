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
# The cookie file it was given: its content and mode kept as last-cookie.txt / last-cookie.mode, for the tests.
cookies=""
args=("$@")
for i in "${!args[@]}"; do
  if [[ "${args[$i]}" == "--cookies" ]]; then cookies="${args[$((i + 1))]}"; fi
done
if [[ -n "$cookies" ]]; then
  cp "$cookies" "$here/last-cookie.txt"
  stat -c '%a' "$cookies" > "$here/last-cookie.mode"
  echo "$cookies" > "$here/last-cookie.path"
fi
# The Liked videos ("LL") answer like YouTube: only to a signed-in cookie (SIGNED_IN), a bot check to BOT_CHECK.
if [[ "$id" == "LL" ]]; then
  if [[ -n "$cookies" ]] && grep -q SIGNED_IN "$cookies"; then
    echo '{"id": "LL", "title": "Liked videos", "_type": "playlist", "entries": [{"id": "k3Fq8xZ0aQw"}]}'
    exit 0
  fi
  if [[ -n "$cookies" ]] && grep -q BOT_CHECK "$cookies"; then
    echo "ERROR: [youtube:tab] LL: Sign in to confirm you’re not a bot." >&2
    exit 1
  fi
  echo "ERROR: [youtube:tab] LL: The playlist does not exist." >&2
  exit 1
fi
if [[ -f "$here/$id.sleep" ]]; then
  sleep "$(cat "$here/$id.sleep")"
fi
# Asked for the formats (--list-formats): <id>.formats on stdout; given the cookie, <id>.signed-in.formats if there's
# one (2.2.1: signed in, YouTube can keep its formats from yt-dlp).
if [[ "$*" == *--list-formats* && -n "$cookies" && -f "$here/$id.signed-in.formats" ]]; then
  cat "$here/$id.signed-in.formats"
  exit 0
fi
if [[ "$*" == *--list-formats* && -f "$here/$id.formats" ]]; then
  cat "$here/$id.formats"
  exit 0
fi
# Given the cookie, <id>.signed-in.err on stderr (2.2.1: signed in, YouTube can keep its formats from yt-dlp).
if [[ -n "$cookies" && -f "$here/$id.signed-in.err" ]]; then
  cat "$here/$id.signed-in.err" >&2
  exit 1
fi
if [[ -f "$here/$id.json" ]]; then
  cat "$here/$id.json"
  exit 0
fi
if [[ -f "$here/$id.err" ]]; then
  cat "$here/$id.err" >&2
fi
exit 1
