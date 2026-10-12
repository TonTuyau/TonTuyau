#!/usr/bin/env bash
# TonTuyau's version lives in two places that must agree (QA13-4): <Version> in backend/Directory.Build.props and
# "version" in frontend/package.json. It is X.Y.Z, or a pre-release X.Y.Z-alpha.N, -beta.N or -rc.N (a release
# branch's state, tried on the VM as X.Y.Z-alpha.N images before the release). With an argument (a git tag such as
# v1.0.0 or v1.3.0-alpha.1), the tag must name that version too.
#   scripts/check-version.sh                  → prints the version, or fails
#   scripts/check-version.sh v1.3.0-alpha.1   → also checks the tag
#   scripts/check-version.sh --prerelease     → prints "true" for a pre-release version, "false" otherwise
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

backend="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/backend/Directory.Build.props")"
frontend="$(sed -n 's/^  "version": "\(.*\)",$/\1/p' "$root/frontend/package.json")"

fail() { printf 'check-version: %s\n' "$*" >&2; exit 1; }

release='[0-9]+\.[0-9]+\.[0-9]+'
prerelease='-(alpha|beta|rc)\.[0-9]+'
[[ "$backend" =~ ^$release($prerelease)?$ ]] \
  || fail "backend/Directory.Build.props: no X.Y.Z or X.Y.Z-alpha.N/-beta.N/-rc.N <Version> (found '$backend')"
[[ "$frontend" == "$backend" ]] || fail "frontend/package.json says '$frontend', backend/Directory.Build.props says '$backend'"

if [[ $# -gt 0 && "$1" == "--prerelease" ]]; then
  if [[ "$backend" =~ $prerelease$ ]]; then printf 'true\n'; else printf 'false\n'; fi
  exit 0
fi
if [[ $# -gt 0 && "$1" != "v$backend" ]]; then
  fail "tag '$1' doesn't match the version $backend (expected v$backend)"
fi

printf '%s\n' "$backend"
