# M0: Skeleton

Branch: `milestone/m0-skeleton` · Features: `feature/m0-backend-skeleton`, `feature/m0-frontend-skeleton`, `feature/m0-tooling`

## What was built

**Backend** (`backend/`)
- `TonTuyau.slnx` (the .NET 10 default solution format) with the planned layers: `TonTuyau.Api` → `TonTuyau.Application` ← `TonTuyau.Infrastructure` / `TonTuyau.TubeArchivist`. Infrastructure and TubeArchivist are empty projects for now.
- Shared build settings (`Directory.Build.props`): nullable on, **warnings as errors**, recommended analyzers, code style enforced at build. Package versions central (`Directory.Packages.props`).
- `global.json`: pins SDK 10.0.4xx and opts into **Microsoft.Testing.Platform** (required by .NET 10 for xUnit v3).
- **Configuration**: flat `TONTUYAU_*` environment variables, read into immutable records, defaults for optional values. `TonTuyauOptionsValidator` (first `IValidator<T>`) **throws `ValidationException`** with *all* broken rules at once. The backend checks this right after building the host, so broken config = no start. Error messages name the variable, never its value.
- `GET /health` (ASP.NET health checks). OpenAPI document in Development.
- The executable is named `tontuyau` (it'll also host the CLI verbs in M3).

**Frontend** (`frontend/`)
- Svelte 5 + Vite 8 + TypeScript (strict, `noUncheckedIndexedAccess`), pnpm.
- Vitest (jsdom) + Testing Library; ESLint (typescript-eslint strict + svelte) + Prettier; `svelte-check --fail-on-warnings`.
- Placeholder app shell. The Vite dev server proxies `/api`, `/media`, `/art` to the backend (`TONTUYAU_BACKEND_URL`, default `http://localhost:5080`).

**Tooling**
- `scripts/check.sh`: backend format check → build → all tests; frontend install → lint → typecheck → tests → build.
- `docker/docker-compose.yml`: `tontuyau-es` (Elasticsearch 9.5.5, security on, 4 GB heap, 8 GB / 3 CPU limit, port bound to `127.0.0.1` only).
- `.env.example`, `.editorconfig`, `README.md`, MIT `LICENSE`.

## How to check it

```bash
git checkout milestone/m0-skeleton
./scripts/check.sh                       # expect "All checks passed."
```

Startup refusal, by hand:

```bash
cd backend && TONTUYAU_TA_URL= dotnet run --project src/TonTuyau.Api
# → ValidationException: Invalid configuration: TONTUYAU_TA_URL: is required.; ...
```

## Tests added

| Kind | Where | Count |
|---|---|---|
| Unit (xUnit v3 + Combinatorial) | `TonTuyau.Application.UnitTests/Configuration` | 65 cases: every required field × blank kinds, URL fields × bad URLs, path fields × relative paths, cron fields × bad crons, time zones, all errors at once, token never in messages, reader defaults/overrides |
| Integration (Reqnroll) | `TonTuyau.IntegrationTests/Features/Health.feature` | 6 scenarios: healthy backend; refuses to start for each missing required setting (+ token not revealed); refuses an invalid cron |
| Front (Vitest) | `frontend/src/App.test.ts` | 2: brand shown; no backend call on startup |

Total: **71 backend + 2 frontend, all passing.**

## Decisions taken during the milestone

- **Flat env names** (`TONTUYAU_TA_URL`, not `TONTUYAU_TubeArchivist__BaseUrl`): matches the plan and `.env` style; mapped in one place (`TonTuyauOptionsReader`).
- **Our own `IValidator<T>`** instead of `IValidateOptions`: same mechanism as the validators coming for API requests and TA data (throw `ValidationException`), so there's one error model everywhere.
- **Default full-sync cron `0 1 * * 0`** (Sunday 01:00) follows the R3-1 default. It's one env var if the answer is Saturday.
- **xUnit v3** (required by Xunit.Combinatorial 2.x and Reqnroll.xunit.v3) running on **Microsoft.Testing.Platform**.
- Reqnroll's generated `*.feature.cs` files are git-ignored.
- Git author for the repo is set locally to `eclegv <eclegv11@gmail.com>`. Change it with `git config user.name/user.email` if you want something else.

## Deviations from the plan

- Unit-test projects exist only where there's code to test (`TonTuyau.Application.UnitTests`). The Api/Infrastructure/TubeArchivist test projects get created with their first code (M1/M2) instead of as empty placeholders.
- `docker-compose.yml` has only Elasticsearch. The backend/frontend services need their Dockerfiles (M7).

## Known limitations / follow-ups

- **Docker isn't installed on the laptop yet** (R2-1). The compose file hasn't been validated or started yet (`docker compose config` once Docker is there), and there are no Testcontainers tests yet (they start in M2).
- The frontend's production Node server (proxy) comes with M7.
