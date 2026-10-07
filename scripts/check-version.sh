#!/usr/bin/env bash
# TonTuyau's version lives in two places that must agree (QA13-4): <Version> in backend/Directory.Build.props and
# "version" in frontend/package.json. With an argument (a git tag such as v1.0.0), the tag must name that version too.
#   scripts/check-version.sh            → prints the version, or fails
#   scripts/check-version.sh v1.0.0     → also checks the tag
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

backend="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/backend/Directory.Build.props")"
frontend="$(sed -n 's/^  "version": "\(.*\)",$/\1/p' "$root/frontend/package.json")"

fail() { printf 'check-version: %s\n' "$*" >&2; exit 1; }

[[ "$backend" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "backend/Directory.Build.props: no X.Y.Z <Version> (found '$backend')"
[[ "$frontend" == "$backend" ]] || fail "frontend/package.json says '$frontend', backend/Directory.Build.props says '$backend'"
if [[ $# -gt 0 && "$1" != "v$backend" ]]; then
  fail "tag '$1' doesn't match the version $backend (expected v$backend)"
fi

printf '%s\n' "$backend"
