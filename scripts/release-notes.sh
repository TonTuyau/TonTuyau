#!/usr/bin/env bash
# A version's notes for its GitHub Release: docs/releases/<version>.md without its "# TonTuyau X.Y.Z" title (the
# release has its own) and with each paragraph and list item on one line. The files are wrapped at 120 columns like
# the other docs, but a release page shows every line break as one.
#   scripts/release-notes.sh 2.0.0 > notes.md
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
file="$root/docs/releases/$1.md"
[[ -f "$file" ]] || { printf 'release-notes: no %s\n' "$file" >&2; exit 1; }

awk '
  # A line that starts a block of its own: a heading, a list item, a table row, a quote, a fence; or a blank line.
  function starts_block(text) { return text == "" || text ~ /^(#|[-*+] |[0-9]+\. |\||>|```)/ }

  NR == 1 && /^# / { skip_blank = 1; next }
  skip_blank && /^$/ { skip_blank = 0; next }
  { skip_blank = 0 }

  /^```/ { code = !code; flush(); print; next }
  code { print; next }

  {
    text = $0
    sub(/^[ \t]+/, "", text)
    # Joined to the line before when it goes on with it: that line is text, and this one starts nothing.
    if (held != "" && !starts_block(text) && held !~ /^(#|\|)/) {
      held = held " " text
    } else {
      flush()
      held = $0
      if ($0 == "") { print ""; held = "" }
    }
  }

  function flush() { if (held != "") print held; held = "" }
  END { flush() }
' "$file"
