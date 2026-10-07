#!/usr/bin/env bash
# Builds and tests everything, front and back. Exit code != 0 on the first failure.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Toolchain installed in the home folder (see README), if not already on PATH.
export PATH="$HOME/.dotnet:$HOME/.local/node/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 COREPACK_ENABLE_DOWNLOAD_PROMPT=0

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }

step "version (backend = frontend)"
"$root/scripts/check-version.sh"

step "compose file (renders with .env.example)"
"$root/scripts/check-compose.sh"

# Run from backend/ so its global.json (SDK pin + Microsoft.Testing.Platform runner) applies.
cd "$root/backend"
step "backend: format"
dotnet format --verify-no-changes

step "backend: build"
dotnet build --nologo -v quiet

step "backend: tests (unit + Reqnroll)"
dotnet test --no-build

cd "$root/frontend"
step "frontend: install"
pnpm install --frozen-lockfile

step "frontend: lint"
pnpm run lint

step "frontend: typecheck"
pnpm run check

step "frontend: tests (the app, the mock, the production server in server/)"
pnpm run test

step "frontend: build"
pnpm run build

step "frontend: the built page has nothing the Content-Security-Policy would block"
pnpm run check:csp

printf '\n\033[1;32mAll checks passed.\033[0m\n'
