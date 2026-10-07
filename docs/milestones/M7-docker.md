# M7: Docker images, the compose stack, GHCR

Branch: `milestone/m7-docker` (from `main` at `e0f396b`) · Features: `feature/m7-version`, `feature/m7-front-server`, `feature/m7-images`, `feature/m7-ci`, `feature/m7-docs`, `feature/m7-ci-scheduler-test`

Spec: [QA_SESSION_13.md](../QA_SESSION_13.md) (Actions + tests, public images, one minor version per step, amd64, `aspnet:10.0` + `node:24-slim` non-root, media as a volume, port 80, plain-Node front server, manual updates), [QA_SESSION_01.md](../QA_SESSION_01.md) Q13–Q16, R2-14, R2-21, [QA_SESSION_02.md](../QA_SESSION_02.md) R2-21, R2-23, R2-24.

Open questions for you: [QA_SESSION_14.md](../QA_SESSION_14.md). Choices I made without asking are marked **(to confirm)** below; each one is a question there.

## Scope

1. Version **0.7.0**, in one checked place, shown in harmless spots.
2. The front container's production server: plain `node:http`, no dependency, tested.
3. Both images, the three-service compose stack, `.env.example`.
4. GitHub Actions: `check.sh`, then the images; GHCR only from `main` and version tags.
5. The README's "Install with Docker" guide; a local end-to-end run of the stack against the real TubeArchivist (read-only).

## What was built

**Version (`feature/m7-version`)**
- `<Version>0.7.0</Version>` in `backend/Directory.Build.props` and `"version": "0.7.0"` in `frontend/package.json`. `scripts/check-version.sh` fails when they differ (first step of `check.sh`), and with a tag argument (`v0.7.0`) when the tag doesn't name that version (used by CI on tags). A backend unit test checks the assembly's version is the props' one, without a `+commit` suffix.
- Shown in: `tontuyau --version` ("TonTuyau 0.7.0", answered without configuration), an `X-TonTuyau-Version` header on every backend answer **(to confirm)**, the image labels, and a plain-text line "TonTuyau 0.7.0" at the bottom of the settings menu (not a menu item: the keyboard skips it).

**Front server (`feature/m7-front-server`)**: `frontend/server/server.ts` (+ `main.ts`), run as `node server/main.ts` (Node 24 strips the types).
- Serves `dist/`: files under `/assets/` (Vite's hashed names) `Cache-Control: public, max-age=31536000, immutable`; `index.html` and the rest `no-cache` with a weak ETag (304 on `If-None-Match`). Any other path that isn't a file gets `index.html` (the router's pages); a missing `/assets/…` file is a 404, never HTML (an old page after an upgrade).
- No way out of `dist/`: the path is decoded once, then refused on `..`, a hidden segment, a backslash or a NUL, and the resolved file must sit under `dist/`; targets that aren't a plain path (`//host/x`, `http://…`) are a 400. Only GET and HEAD (405 otherwise).
- Forwards `/api`, `/media`, `/art` (and only those prefixes: `/apiary` is a page) to `TONTUYAU_BACKEND_URL`, streaming both ways: the request body piped up, the raw path and query as sent, `Range` / `If-Range` / `If-None-Match` and the other end-to-end headers forwarded (hop-by-hop ones and those named in `Connection` dropped), `X-Forwarded-For/Proto/Host` added, the backend's status (200, 206, 304, 405, 416…) and headers passed back, the body piped. A browser that goes away (seeking, closing the tab) destroys the upstream request, before or during the answer. A backend that can't be reached gives a **bare 502** in plain text: the app then says "Can't reach the TonTuyau server", as with the dev proxy (M5).
- `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`, `X-Frame-Options: SAMEORIGIN` on everything (added to the backend's answers when it didn't set them). No CSP **(to confirm)**, no compression **(to confirm)**.
- `main.ts`: `PORT` (8080), `TONTUYAU_BACKEND_URL` (`http://tontuyau-backend:8080`), `TONTUYAU_DIST`; on SIGTERM it stops accepting, lets answers end, and cuts long video streams after 5 s.

**Images and stack (`feature/m7-images`)**
- `docker/backend.Dockerfile`: SDK 10.0 build (a restore layer from the project files alone, then a `linux-x64` framework-dependent publish, so only SkiaSharp's `libSkiaSharp.so` for that platform; `appsettings.Development.json` dropped) → `mcr.microsoft.com/dotnet/aspnet:10.0`, which is **Ubuntu 24.04**, not Debian **(to confirm)**. Runs as the image's `app` user (1654); `/data/art` and `/data/logs/sync` are created owned by it, so fresh named volumes are writable; `/app` on the PATH, so `docker exec tontuyau-backend tontuyau …` works; the entrypoint is `/app/tontuyau`.
- `docker/frontend.Dockerfile`: `node:24-slim` + corepack's pnpm (the version `packageManager` pins), `pnpm install --frozen-lockfile`, `pnpm run build` → `node:24-slim` with only `dist/` and the two server files (212 KB: no `node_modules`, no pnpm store), as `node`, port 8080.
- Both: OCI labels `title`, `description`, `source` (the GitHub repo), `licenses=MIT`, `version`, `revision` (build args; CI and `scripts/build-images.sh` pass the version and the commit).
- `.dockerignore` is an **allow-list** (the backend's `src/` and build settings, `frontend/`, `.editorconfig`), with `bin/`, `obj/`, `node_modules/`, `dist/` and any `.env*` excluded inside those: `.env`, `dev-data/`, `docs/` and `.git` can't reach a build.
- `docker/docker-compose.yml`:
  - `tontuyau-es` (9.5.4, 4 GB heap, 8 GB / 3 CPU), `tontuyau-backend` (1 GB / 2 CPU), `tontuyau-frontend` (256 MB / 1 CPU); `restart: unless-stopped`; healthchecks (ES: cluster yellow; backend: `/health/ready` through bash's `/dev/tcp`, the image has no curl **(to confirm)**; front: `node -e fetch('/')`), and `depends_on: service_healthy` front → backend → ES.
  - Only the front publishes a port: `${TONTUYAU_HTTP_PORT:-80}:8080`. The M0 localhost port of Elasticsearch moved to `docker/compose.dev.yml` for development **(to confirm)**.
  - Volumes: `tontuyau-es-data`, `tontuyau-art`, `tontuyau-sync-logs` (local), and the media: `tontuyau-media` (or `TONTUYAU_MEDIA_VOLUME`), declared `external: true`, mounted **read-only** at `/youtube`. The README shows how to create it as NFS or as a bind to a local folder. **Replaced in 0.7.2** by a folder path, `TONTUYAU_MEDIA_PATH`, bind-mounted read-only (see [releases/0.7.2](../releases/1.0.0.md)).
  - Image tags `${TONTUYAU_VERSION:-latest}`. Required settings fail early (`${…:?}`); every optional one is passed through as `${VAR:-}`: empty means the backend's own default, so defaults live in one place (the backend). Elasticsearch's URL and the three paths are fixed by the compose file **(to confirm)**.
- `.env.example` rewritten for the stack: every variable it reads, with its default and meaning; the development-only ones (ES URL, paths) commented at the end.
- `scripts/build-images.sh [tag]` builds both images like CI (default tag `dev`).
- Found by the end-to-end run, fixed: in the image the CLI's working folder is `/app`, where `appsettings.json`'s `"Default": "Information"` beat `SetMinimumLevel(Warning)`, so `tontuyau sync full` printed every TubeArchivist request (450 KB) above its report. A code filter now keeps the CLI at warnings and errors.

**CI (`feature/m7-ci`)**: `.github/workflows/ci.yml`
- On every push (any branch, and `v*.*.*` tags) and pull request: job **Checks** installs .NET from `backend/global.json`, pnpm from `frontend/package.json`'s `packageManager`, Node 24 (NuGet and pnpm stores cached), and runs `./scripts/check.sh` (Testcontainers uses the runner's Docker). On a tag, it first checks the tag matches the code's version.
- Then job **Image backend / frontend** (matrix, after Checks): buildx, `linux/amd64`, GHA cache per image; `docker/metadata-action@v5`, `login-action@v3`, `build-push-action@v6`.
- **Pushes only** on a push to `main` (tags `main`, `sha-<short>`) or a `vX.Y.Z` tag (`X.Y.Z`, `X.Y`, `latest`), with `GITHUB_TOKEN` and `packages: write`. Other branches and pull requests build without pushing (their branch tag is never pushed). The `version` label is the code's version, `licenses` MIT.

**Scheduler test (`feature/m7-ci-scheduler-test`)**: a race in `SyncSchedulerTests.Runs_each_sync_when_it_is_due`, seen on a CI runner (see the end of this file); the test now waits until the scheduler's timer exists before moving the fake clock.

**Docs (`feature/m7-docs`)**: README "Install with Docker" (prerequisites, the media volume, `.env`, `docker compose pull && docker compose up -d`, the first full sync and its timings, updating by hand, logs, backups per volume, the CLI, GHCR and the one-time visibility step), configuration tables for the stack's variables, development setup updated (ES through `compose.dev.yml`, the front server, the version check); this file; [QA_SESSION_14.md](../QA_SESSION_14.md); the plan's M7 row.

## Image sizes

| Image | On disk | Compressed (pull) | Base |
|---|---|---|---|
| `ghcr.io/eclegv/tontuyau-backend` | 386 MB | 109 MB | `aspnet:10.0` 340 MB / 96 MB |
| `ghcr.io/eclegv/tontuyau-frontend` | 330 MB | 81 MB | `node:24-slim` (our layers: 230 KB) |

## Local end-to-end check

2026-10-06 on the laptop, both images built locally (`scripts/build-images.sh`, tag `dev`), the compose file of this branch plus a laptop-only override (not committed): Elasticsearch heap 1 GB / limit 2 GB; as art, a **hard-linked copy** of `dev-data/trial/art` (no extra disk, the trial folder untouched; the backend ran as uid 1000 there because those files are the user's); a bind folder for the sync logs. Media: a volume made exactly like the README's local-folder recipe (`--opt type=none --opt o=bind,ro`) over `dev-data/youtube` (11 real files). `TONTUYAU_SYNC_SCHEDULER_ENABLED=false`, port 8088. The stack's own fresh Elasticsearch (not the trial one). In the art copy I deleted the resized versions of 30 video thumbnails and 12 channel images first, so the sync had to remake them.

| Check | Result |
|---|---|
| `docker compose up -d` | all three healthy in 42 s (ES 41 s, backend 5 s after it, front 1 s) |
| `docker exec tontuyau-backend tontuyau sync full` (real TubeArchivist, read-only) | **2 min 36 s**: 45,339 videos / 116 channels, 0 rejected, 5 corrected, 20 images copied (new since the trial), **108 resized versions made, 0 failed** (SkiaSharp works in the image; the 72 deleted versions are back, valid WebP), 45,328 files "missing" (only 11 are on the laptop), 0 deletions |
| `tontuyau sync status`, `tontuyau --version` | the run report; "TonTuyau 0.7.0"; `tontuyau sync full` prints only its report since the CLI fix |
| Media through the front container | `Range: bytes=1000-1999` → **206** with `Content-Range: bytes 1000-1999/393895`; past the end → **416**; `/media/<id>/download` → 200 with `Content-Disposition`, byte-identical to the file |
| Art | `/art/videos/<id>/360` → `image/webp`; `If-None-Match` → **304** |
| Read-only media | `touch /youtube/x` in the backend: "Read-only file system" |
| Backend stopped | `/api/home` → bare **502** (plain text), the app's pages still load; backend started again → healthy, 200 |
| Browser (Chromium, my own tab) | Home (sized thumbnails `/art/videos/<id>/720`, all loading), a channel (banner, avatar, tabs), watch: H.264/Opus played and seeked (7 s → 8.7 s, 206 answers), shorts: AV1 played (1080×1920), search "creme brulee" (7 results), settings menu "TonTuyau 0.7.0"; no console errors |
| Memory | ES 1.5 GB (1 GB heap), backend 60–180 MB (sync running), front 50–100 MB |
| Secrets | the build context holds no `.env`, `dev-data/`, `bin/`, `obj/` or `node_modules/` (listed by copying it into a throwaway image); a filesystem grep of both images for the TubeArchivist token: 0 files; image env holds only paths and ports |

The full sync here is much shorter than the README's "about 13 minutes" because the art was already there (the first install copies ~4 GB from TubeArchivist and resizes it all: the README's 13 + 7 minutes come from the M5 trial).

Cleaned up: `docker compose down -v` (the stack's Elasticsearch volume removed; the art and logs were binds), the test media volume and the hard-linked art copy removed, my browser tab closed. The local images `ghcr.io/eclegv/tontuyau-backend:dev` (386 MB) and `tontuyau-frontend:dev` (330 MB) are kept.

## Tests

| Kind | What | Count |
|---|---|---|
| Vitest (node environment, real sockets) | front server: immutable assets, revalidated files + 304, HEAD, SPA fallback (5 routes, incl. `/apiary`), missing asset 404, traversal and hidden files (10 paths), non-path targets, 405, security headers; proxy: 4 prefixes forwarded as is (path, query, Host, X-Forwarded-*), Range / If-Range / If-None-Match with 206 / 304 / 416, streaming (first bytes before the backend ends), request body + method, abort before and during the answer, hop-by-hop headers, bare 502 | 33 |
| Vitest | settings menu shows the version as plain text | 1 |
| xUnit | `--version` (2 spellings), `IsCommand` / `NeedsStore` rows, the assembly version = Directory.Build.props | 5 |
| Reqnroll | `/health` carries `X-TonTuyau-Version` (a step added to the existing scenario) | 0 new |

Totals: **3,934 backend** (3,929 before), **368 front** (334 before), all passing in `./scripts/check.sh` before each merge into the milestone branch. Mutation checks: removing the upstream cancel on close fails the "goes away before the backend answers" test; removing the traversal guards fails 5 traversal cases.

Two flaky backend scenarios, each failing once in eight `check.sh` runs and passing on the rerun (`Sync.feature` "A channel with more than the window of one type…", `Cli.feature` "Status shows the watermark…"): both as if the scenario's indices were briefly invisible, with the laptop swapping; outside M7: [QA14-10](../QA_SESSION_14.md).

## GitHub Actions and GHCR

- Pushed: `milestone/m7-docker` only. On this branch the workflow runs the checks and builds both images **without pushing** them.
- **Your one-time step**, after the first push to GHCR (the merge into `main`, or the `v0.7.0` tag): GHCR creates each new package as **private**. For `tontuyau-backend` and `tontuyau-frontend`: github.com → your profile → **Packages** → the package → **Package settings** → **Danger Zone → Change visibility → Public**; check it's linked to `Eclegv/TonTuyau` (it should be, from the `source` label). Never done by a workflow (the token can't).
- After the merge into `main`, tag `v0.7.0` and push it to get `0.7.0`, `0.7` and `latest` ([QA14-8](../QA_SESSION_14.md)).

## Decisions taken during the milestone (to confirm)

All asked in [QA_SESSION_14.md](../QA_SESSION_14.md): the Ubuntu base image (Microsoft has no Debian image for .NET 10), the version header on every answer, no CSP, no compression, the backend healthcheck on `/health/ready`, the fixed paths and limits in the compose file, Elasticsearch's dev port in an override file.

Also:
- The front server runs TypeScript directly (`node server/main.ts`, type stripping in Node 24), like the repo's other Node scripts: no build step for it, the same file is tested.
- Proxied answers keep the backend's status line and headers as they are; the front server only adds its three security headers when missing.
- Optional settings go through compose as empty strings, which the backend already reads as "unset".

## Deviations from the plan

- QA13-6 said "Debian slim" for `aspnet:10.0`: that tag is Ubuntu 24.04 since .NET 10, and no Debian variant exists.
- The compose file no longer publishes Elasticsearch's port (M0 did, for development); `docker/compose.dev.yml` does it on demand.

## Known limitations / follow-ups

- The first full sync's art copy and full resize weren't re-measured in the container (the laptop reused the trial art); M8 measures the real first install on the VM.
- No ARM image (QA13-5).
- CI on `milestone/m7-docker`: the first run (47008c1) passed in 11 min 31 s (checks 8 min 4 s, then the images in parallel: backend 3 min 20 s, front 58 s; built, not pushed). The second (6fa131d, the docs) failed on a timing race in a scheduler unit test (`Runs_each_sync_when_it_is_due`: the test moved the fake clock before the scheduler's timer existed). Fixed on `feature/m7-ci-scheduler-test` (the test waits for the timer; checked by widening the window: the old test fails, the new one passes).

## After M7: follow-ups (in 1.0.0)

Three follow-ups from this review ([notes](../releases/1.0.0.md), questions in [QA_SESSION_15.md](../QA_SESSION_15.md)):
- **CSP done** (QA14-3 b): the front server sends a strict Content-Security-Policy on every answer; the theme script moved from inline to `public/theme-init.js`. The "No CSP **(to confirm)**" above no longer holds.
- **Healthcheck on `/health`** (QA14-5 b): the backend's compose healthcheck asks the generic `/health` instead of `/health/ready` (still an endpoint). The front can now start before Elasticsearch is usable; the API's errors then show in the app with a Retry button.
- **The two flaky scenarios** (QA14-10) were a product bug, not slowness: `GET _alias/<name>` fails with `404 index_not_found_exception` when *any* index is deleted at the same moment (Elasticsearch with security resolves the index list twice), and TonTuyau took that 404 for "alias missing". Fixed with `ignore_unavailable=true` and a strict reading of the 404.

## After M7: CI triggers (in 1.0.0)

Since 1.0.0, CI runs only on pull requests to `main` and on version tags, to save Actions minutes: what "Pushes only" above describes for `main` (tags `main`, `sha-<short>`) no longer happens. Images are published only from `vX.Y.Z` tags (`X.Y.Z`, `X.Y`, `latest`); pull requests build them without pushing. See [releases/1.0.0.md](../releases/1.0.0.md).
