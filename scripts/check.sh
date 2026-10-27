#!/usr/bin/env bash
# Builds and tests everything, front and back. Exit code != 0 on the first failure.
#   check.sh                 every stage, in order
#   check.sh --backend       the version, the compose file, then the backend's stages
#   check.sh --frontend      the version, the compose file, then the front's stages
#   check.sh STAGE...        those stages only, in the order given (the CI runs each as a step of its own)
# The stages (`check.sh --list` lists them):
#   version, compose
#   backend:format, backend:build, backend:unit, backend:integration
#   frontend:install, frontend:lint, frontend:typecheck, frontend:test, frontend:build, frontend:csp
# A stage expects the ones before it on its side: backend:unit and backend:integration test what backend:build built,
# the front's stages need frontend:install's packages, frontend:csp reads frontend:build's page.
set -euo pipefail

backend_stages=(backend:format backend:build backend:unit backend:integration)
frontend_stages=(frontend:install frontend:lint frontend:typecheck frontend:test frontend:build frontend:csp)
all_stages=(version compose "${backend_stages[@]}" "${frontend_stages[@]}")

usage() {
  echo "usage: $0 [--backend | --frontend | --list | STAGE...]" >&2
  echo "stages: ${all_stages[*]}" >&2
  exit 2
}

done_message="All checks passed."
case "${1:-}" in
  "") stages=("${all_stages[@]}") ;;
  --backend)
    stages=(version compose "${backend_stages[@]}")
    done_message="All backend checks passed."
    ;;
  --frontend)
    stages=(version compose "${frontend_stages[@]}")
    done_message="All front checks passed."
    ;;
  --list)
    printf '%s\n' "${all_stages[@]}"
    exit 0
    ;;
  -*) usage ;;
  *)
    stages=("$@")
    done_message="Passed: $*."
    ;;
esac

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Toolchain installed in the home folder (see README), if not already on PATH.
export PATH="$HOME/.dotnet:$HOME/.local/node/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 COREPACK_ENABLE_DOWNLOAD_PROMPT=0

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }

# The backend's commands run from backend/, so its global.json (SDK pin + Microsoft.Testing.Platform runner) applies.
run_stage() {
  case "$1" in
    version)
      step "version (backend = frontend)"
      "$root/scripts/check-version.sh"
      ;;
    compose)
      step "compose file (renders with .env.example)"
      "$root/scripts/check-compose.sh"
      ;;
    backend:format)
      step "backend: format"
      (cd "$root/backend" && dotnet format --verify-no-changes)
      ;;
    backend:build)
      step "backend: build"
      (cd "$root/backend" && dotnet build --nologo -v quiet)
      ;;
    backend:unit)
      step "backend: unit tests"
      (cd "$root/backend" \
        && dotnet test --no-build --root-directory "$root/backend" \
          --test-modules "tests/*.UnitTests/bin/Debug/net10.0/*.UnitTests.dll")
      ;;
    backend:integration)
      step "backend: integration tests (Reqnroll; Testcontainers starts Elasticsearch, PostgreSQL, LLDAP)"
      (cd "$root/backend" && dotnet test --no-build --project tests/TonTuyau.IntegrationTests)
      ;;
    frontend:install)
      step "frontend: install"
      (cd "$root/frontend" && pnpm install --frozen-lockfile)
      ;;
    frontend:lint)
      step "frontend: lint"
      (cd "$root/frontend" && pnpm run lint)
      ;;
    frontend:typecheck)
      step "frontend: typecheck"
      (cd "$root/frontend" && pnpm run check)
      ;;
    frontend:test)
      step "frontend: tests (the app, the mock, the production server in server/)"
      (cd "$root/frontend" && pnpm run test)
      ;;
    frontend:build)
      step "frontend: build"
      (cd "$root/frontend" && pnpm run build)
      ;;
    frontend:csp)
      step "frontend: the built page has nothing the Content-Security-Policy would block"
      (cd "$root/frontend" && pnpm run check:csp)
      ;;
    *)
      echo "unknown stage: $1" >&2
      usage
      ;;
  esac
}

for stage in "${stages[@]}"; do
  run_stage "$stage"
done

printf '\n\033[1;32m%s\033[0m\n' "$done_message"
