# TonTuyau

<img src="frontend/public/favicon.svg" alt="" width="64" height="64" align="right">

*Ton tuyau*: French for "your pipe" (or "your tube").

A self-hosted, YouTube-like web UI for the library of a [TubeArchivist](https://github.com/tubearchivist/tubearchivist) instance. Browsing needs no login: every channel and video is listed and searchable (an admin account can log in, for the administration to come). TonTuyau **only reads** from TubeArchivist and never modifies it.

- **Backend**: ASP.NET Core (.NET 10), REST API, its own Elasticsearch 9, a scheduled sync from TubeArchivist (hourly for the new downloads, a full pass once a week).
- **Frontend**: Svelte 5 + Vite + TypeScript, built with pnpm, tested with Vitest.
- **Playback**: the original files, straight from your videos folder (mounted read-only); no transcoding.

Status: **1.0.0**. Runs as three Docker containers: see [Install with Docker](#install-with-docker). Plan: [docs/DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md); milestone notes: [docs/milestones/](docs/milestones/).

## Repository layout

| Path | Content |
|---|---|
| `backend/` | `TonTuyau.slnx`: `src/` (Api, Application, Infrastructure, TubeArchivist) and `tests/` (unit tests + Reqnroll integration tests) |
| `frontend/` | Svelte + Vite app; `server/` is the production server of the front container (plain Node, no dependency) |
| `docker/` | `docker-compose.yml` (the stack), the two Dockerfiles, `compose.dev.yml` (Elasticsearch on localhost for development) |
| `.github/workflows/ci.yml` | GitHub Actions on pull requests and version tags: `check.sh`, then the images, pushed to GHCR from version tags only |
| `backend/tools/TonTuyau.FixtureRecorder` | records anonymised TA fixtures for the tests; `scan` reports what the validators reject across the whole library (read-only) |
| `tools/fetch-dev-media/` | copies a few real videos + art from TA into `dev-data/` for local dev (read-only) |
| `scripts/check.sh` | builds and tests everything |
| `scripts/check-version.sh`, `scripts/build-images.sh` | the version check (backend = front), local image builds |
| `docs/` | plan, Q&A sessions, milestone notes |

## Install with Docker

TonTuyau runs as four containers: `tontuyau-es` (its own Elasticsearch), `tontuyau-db` (its PostgreSQL, for the accounts), `tontuyau-backend` (the API, the sync and the `tontuyau` command line) and `tontuyau-frontend` (the web app, which also forwards `/api`, `/media` and `/art` to the backend). Only the front is reachable from the LAN, on port 80 by default. The images are `ghcr.io/eclegv/tontuyau-backend` and `ghcr.io/eclegv/tontuyau-frontend` (`linux/amd64`).

### Prerequisites

- A Linux VM (x86-64) with Docker Engine and the Compose plugin (`docker compose version`). The stack sets **no CPU or memory limits**: it uses what the VM has. Elasticsearch takes a fixed heap, `TONTUYAU_ES_HEAP` (default 4 GB), plus its file cache; about 8 GB of RAM in total and 4+ cores are comfortable, and more cores make the first sync's image resizing faster. To cap a container anyway, add `deploy.resources.limits` in a `docker-compose.override.yml` next to the compose file (example in [releases/1.0.0](docs/releases/1.0.0.md)).
- For Elasticsearch, on the VM: `sudo sysctl -w vm.max_map_count=262144` (and the same line, `vm.max_map_count=262144`, in `/etc/sysctl.d/99-tontuyau.conf` so it survives a reboot).
- About 10 GB of local disk for the volumes (the index, and about 6 GB of images with their resized versions), plus the Docker images (about 3.5 GB, Elasticsearch's being most of it).
- The TubeArchivist URL and an API token (TubeArchivist → Settings → API token). TonTuyau only ever reads from it.
- The folder of videos TubeArchivist downloads into (its `youtube` folder: `<channel id>/<video id>.<ext>`), readable on the VM.

### 1. The videos folder

TonTuyau reads the videos from **one folder** on the VM: TubeArchivist's `youtube` folder, with one folder per channel (`<channel id>/<video id>.<ext>`). Set its path in `.env` as `TONTUYAU_MEDIA_PATH` (e.g. `/mnt/youtube`); the stack mounts it **read-only**, so TonTuyau can't change the files. Where the folder comes from is up to you: TonTuyau only needs to read it.

Check it lists channel folders (`UC…`):

```bash
ls /mnt/youtube | head
```

- If the folder doesn't exist, `docker compose up` stops with an error rather than starting with no videos.
- If it's empty, the sync notes it and never marks your files as missing.

### 2. Get the compose file and `.env`

In an empty folder (for example `/opt/tontuyau`), put [`docker/docker-compose.yml`](docker/docker-compose.yml) and [`.env.example`](.env.example) from this repository, then:

```bash
cp .env.example .env
chmod 600 .env
```

Fill in `TONTUYAU_TA_URL`, `TONTUYAU_TA_TOKEN`, `TONTUYAU_ES_PASSWORD` and `TONTUYAU_DB_PASSWORD` (any long random strings: `openssl rand -base64 24`), and `TONTUYAU_ADMIN_PASSWORD`: the admin account's password, read at the first start only (see [Logging in](#logging-in)). Every other variable has a default; `.env.example` explains each one.

### 3. Start

```bash
docker compose pull && docker compose up -d
docker compose ps        # all four "healthy" after a minute or two
```

The backend's "healthy" means its process answers (`/health`); `/health/ready` also says whether Elasticsearch and TonTuyau's indices are usable (see [REST API](#rest-api)).

Then open `http://<vm-ip>/` (or the port in `TONTUYAU_HTTP_PORT`). The library is empty until the first sync.

### 4. The first full sync

```bash
docker exec -it tontuyau-backend tontuyau sync full
```

It reads the whole TubeArchivist library (read-only) into TonTuyau's index, copies the images (about 4 GB) and makes their resized versions, channel by channel. On a library of about 45,000 videos it takes **about 20 minutes**. Later full runs only copy and resize what's new: about 3 minutes.

While it runs, the command shows where it is, as plain lines (the same in a terminal and in a log): a line at each step and every 10 seconds, with the time since it started. During a long step with nothing new to count (a slow TubeArchivist answer, the switch to the new indices), the current line is printed again every 10 seconds, so you can see the time go up and know it's still running.

```
[00:00] channels      0 / 150 read
[00:41] videos        12 / 150 channels · 4,210 videos · art 4,180 copied, 8,350 resized
[19:05] media check   45,339 files checked · 2 missing
[19:12] deletions     nothing to delete
```

- Each update is a **line of its own** (about 130 to 180 for a first full sync), with or without `-t`, and piped too (`docker exec tontuyau-backend tontuyau sync full | tee sync.log` keeps a copy); each step ends with its final counts.
- A count is shown as "x / y" only when the total is really known (the channels); the videos are counted as they come.

Then it prints the run's report (`tontuyau sync status` shows it again). `tontuyau sync incremental`, `tontuyau index rebuild` and `tontuyau sync apply-deletions <run>` show their progress the same way. The scheduled syncs print nothing (their start and end are in the backend's log).

After it, the scheduler keeps TonTuyau up to date by itself (`TONTUYAU_SYNC_INCREMENTAL_CRON`, `TONTUYAU_SYNC_FULL_CRON` in `TONTUYAU_TZ`). Without the command above, its first run would do the full import anyway, at the next scheduled time.

### Updating

By hand (QA13-10), when you're there to watch:

```bash
docker compose pull && docker compose up -d
```

`TONTUYAU_VERSION` in `.env` picks the version: `latest` by default, or a pinned one like `1.0.0` (or `1.0` for the latest 1.0.x). When a new version changes the index definitions, **the backend rebuilds the index by itself at startup** (about 2 minutes on 45,000 videos; the app keeps serving the current index meanwhile): see [Upgrading](#upgrading). `docker exec tontuyau-backend tontuyau --version` says what runs.

### Logs

```bash
docker compose logs -f tontuyau-backend      # the API and the scheduled syncs
docker compose logs --tail 100 tontuyau-frontend
docker exec tontuyau-backend tontuyau sync status
```

Full runs also write their deletion lists to the `tontuyau-sync-logs` volume (`/data/logs/sync` in the container).

### Using folders instead of volumes (optional)

The stack keeps Elasticsearch's data, the images and the sync logs in **named volumes**: nothing to do. If you'd rather have them in folders of the VM (bind mounts, e.g. `- /srv/tontuyau/es:/usr/share/elasticsearch/data` in place of `tontuyau-es-data:…`), Docker creates a missing folder as `root`, and the containers can't write there. Give them their owners once, before `docker compose up`:

```bash
sudo chown -R 1000:0 <es-folder> && sudo chmod -R g+rwX <es-folder>   # Elasticsearch runs as uid 1000, group 0
sudo chown -R 1654:1654 <art-folder> <sync-logs-folder>               # the backend runs as uid 1654
```

Rather than editing `docker-compose.yml`, you can name the folders in a `docker-compose.override.yml` next to it: Compose reads it by itself, each line replaces the volume mounted at the same place in the container, and taking a newer compose file doesn't undo it.

```yaml
services:
  tontuyau-es:
    volumes:
      - /srv/tontuyau/es:/usr/share/elasticsearch/data
  tontuyau-backend:
    volumes:
      - /srv/tontuyau/art:/data/art
      - /srv/tontuyau/logs:/data/logs/sync
```

### Backups

| Volume | Content | Back it up? |
|---|---|---|
| `tontuyau-db-data` | the accounts and the keys of the login cookies (PostgreSQL) | **Yes**: `docker exec tontuyau-db pg_dump -U tontuyau tontuyau > tontuyau-db.sql` |
| `tontuyau-es-data` | TonTuyau's index (Elasticsearch) | Not needed: a full sync rebuilds it from TubeArchivist (about 13 minutes) |
| `tontuyau-art` | the images copied from TubeArchivist and their resized versions (about 6 GB) | Optional: the next full sync copies and resizes what's missing (about 20 minutes) |
| `tontuyau-sync-logs` | full runs' deletion lists | Optional, small |
| `TONTUYAU_MEDIA_PATH` (a folder, not a volume) | your videos | Yours: TonTuyau never writes there |
| `.env` | the settings and secrets | **Yes** (it holds the TubeArchivist token and the passwords: keep the copy private) |

A volume to a tarball, for example (a folder, see above: back it up like any folder): `docker run --rm -v tontuyau_tontuyau-art:/v:ro -v "$PWD":/b busybox tar czf /b/art.tgz -C /v .` (Compose prefixes the volume names with the project name `tontuyau_`; `docker volume ls` lists them.)

### Command line

The backend's image puts `tontuyau` on the PATH:

```bash
docker exec tontuyau-backend tontuyau sync status
docker exec tontuyau-backend tontuyau sync incremental
docker exec tontuyau-backend tontuyau sync full
docker exec tontuyau-backend tontuyau sync apply-deletions <run>
docker exec tontuyau-backend tontuyau index rebuild
docker exec tontuyau-backend tontuyau --version
```

See [Sync and command line](#sync-and-command-line) for what each does and the exit codes.

### Images on GHCR

GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs only on **pull requests** to `main` and on **version tags** (to save Actions minutes: pushing a branch runs nothing, its pull request does; a newer push to an open pull request cancels the run in progress). Each run does `scripts/check.sh` and builds both images. Images are **published only from a version tag** `vX.Y.Z`, when everything passed, as `X.Y.Z`, `X.Y` and `latest`; a pull request builds them without pushing. There are no `main` or `sha-…` image tags any more.

**One-time step after the first push to GHCR** (GitHub creates a new package as private, the first time a version tag publishes it): for each of `tontuyau-backend` and `tontuyau-frontend`, open github.com → your profile → **Packages** → the package → **Package settings**:
1. **Danger Zone → Change visibility → Public** (QA13-3), so the VM pulls without logging in.
2. Check that the package is linked to `Eclegv/TonTuyau` (the images carry the `org.opencontainers.image.source` label, so GitHub links them by itself; otherwise **Connect repository**), and that **Manage Actions access** lists the repository with write access.

Each release tag (`v1.0.0`, `v1.0.1`, …) publishes `X.Y.Z`, `X.Y` and moves `latest`: pick one of those in `TONTUYAU_VERSION`. A pre-release tag (`v1.3.0-alpha.1`) publishes only its own `1.3.0-alpha.1`, to try a version before its release.

## Development setup

Prerequisites: .NET 10 SDK, Node 24+, pnpm (via corepack), Docker (for Elasticsearch, PostgreSQL and the integration tests that need them; your user must be in the `docker` group). The integration tests start one PostgreSQL (`postgres:17`) and give each scenario a database of its own. The Elasticsearch integration tests start a container with a 512 MB heap and refuse to start with less than 2 GB of free RAM.

```bash
cp .env.example .env    # then fill in the values, and uncomment the "Development only" lines; .env is git-ignored
./scripts/check.sh      # version check, format check, build, all tests, front and back
```

Run locally:

```bash
docker compose --env-file .env -f docker/docker-compose.yml -f docker/compose.dev.yml up -d tontuyau-es tontuyau-db   # localhost:9200, :5432
set -a; . ./.env; set +a; cd backend && dotnet run --project src/TonTuyau.Api   # http://localhost:5080
cd frontend && pnpm install && pnpm dev                              # http://localhost:5173, proxies /api /media /art
```

Front mockup without any backend (M5a): the dev server answers `/api`, `/art` and `/media` itself from synthetic fixtures in `frontend/mock/`:

```bash
cd frontend && pnpm run dev:mockup             # http://localhost:5173 (or PORT=5180; `-- --port` doesn't reach Vite with pnpm 12)
```

Open `/__mock` for the review knobs: empty library, failing API, slow responses, and real or synthetic channels. Real channels come from an optional, git-ignored `dev-data/mockup/` folder (or `TONTUYAU_MOCK_REAL_DIR`). See [docs/milestones/M5a-front-mockup.md](docs/milestones/M5a-front-mockup.md).

The production front server (`frontend/server/`, what the front container runs) serves `dist/` and forwards `/api`, `/media`, `/art` like the dev server: `pnpm run build && PORT=8080 TONTUYAU_BACKEND_URL=http://localhost:5080 node server/main.ts`. Images like CI's: `scripts/build-images.sh` (tag `dev`), then run the stack with `TONTUYAU_VERSION=dev`.

The version lives in `backend/Directory.Build.props` (`<Version>`) and `frontend/package.json`; `scripts/check-version.sh` (in `check.sh`) fails when they differ. Bump both at each release (a minor per milestone, a patch for the fixes between them).

The front's API types are generated from the backend's OpenAPI document: `frontend/src/lib/api/openapi.json` is a committed copy, `schema.d.ts` is made from it. After a backend change that alters the document (a backend test then fails), start the backend and run `cd frontend && pnpm run api:openapi` (copies the document and regenerates the types; `pnpm run api:types` regenerates from the copy alone).

## Configuration

All settings are environment variables prefixed `TONTUYAU_` (see [.env.example](.env.example)). The backend **refuses to start** with a missing or invalid value and says which one. In the Docker stack, `.env` holds them; the compose file fixes `TONTUYAU_ES_URL` (`http://tontuyau-es:9200`) and the three paths (`/youtube`, `/data/art`, `/data/logs/sync`), and passes every other one through (empty = the default below).

| Variable | Required | Default |
|---|---|---|
| `TONTUYAU_TA_URL` | yes | |
| `TONTUYAU_TA_TOKEN` | yes | |
| `TONTUYAU_ES_URL` | yes | |
| `TONTUYAU_ES_USERNAME` | no | `elastic` |
| `TONTUYAU_ES_PASSWORD` | yes | |
| `TONTUYAU_ES_INDEX_PREFIX` | no | `tontuyau` (indices `tontuyau-videos-v1`… behind aliases `tontuyau-videos` / `tontuyau-videos-write`) |
| `TONTUYAU_MEDIA_ROOT` | no | `/youtube` |
| `TONTUYAU_ART_ROOT` | no | `/data/art` |
| `TONTUYAU_SYNC_INCREMENTAL_CRON` | no | `0 7-23 * * *` (hourly, 07:00–23:00) |
| `TONTUYAU_SYNC_FULL_CRON` | no | `0 1 * * 6` (Saturday 01:00, the night Friday → Saturday) |
| `TONTUYAU_TZ` | no | `Europe/Paris` |
| `TONTUYAU_SYNC_LOG_ROOT` | no | `/data/logs/sync` (full runs' deletion lists) |
| `TONTUYAU_SYNC_MAX_DELETIONS` | no | `100` (above: nothing deleted until `apply-deletions`) |
| `TONTUYAU_SYNC_INCREMENTAL_MARGIN_HOURS` | no | `24` |
| `TONTUYAU_SYNC_SCHEDULER_ENABLED` | no | `true` |
| `TONTUYAU_TA_RESULT_WINDOW` | no | `10000` (TubeArchivist's list window) |
| `TONTUYAU_SYNC_REJECT_GUARD_MIN_DOCUMENTS` | no | `20`: the format guard doesn't judge before this many documents are read (`0` = from the first page) |
| `TONTUYAU_SYNC_REJECT_GUARD_PERCENT` | no | `50`: past that minimum, a run stops when more than this share of what it read is rejected (`100` = guard off) |
| `TONTUYAU_ART_RESIZE_PARALLELISM` | no | the machine's core count (in the stack: the backend container's, 2): how many images the sync resizes at once (1–64) |
| `TONTUYAU_DB_HOST` | no | `tontuyau-db` (`host` or `host:port`; fixed in the stack) |
| `TONTUYAU_DB_NAME` | no | `tontuyau` |
| `TONTUYAU_DB_USERNAME` | no | `tontuyau` |
| `TONTUYAU_DB_PASSWORD` | yes | |
| `TONTUYAU_ADMIN_USER` | no | `admin`: the account the first start creates |
| `TONTUYAU_ADMIN_PASSWORD` | at the first start | its password; read only while there's no account at all |
| `TONTUYAU_COOKIE_SECURE` | no | `true`: the login cookie only travels over HTTPS; `false` to browse over plain `http://` |
| `TONTUYAU_TRUSTED_PROXIES` | no | empty. Your reverse proxy's address (several: commas), so each visitor behind it gets their own login attempts |
| `TONTUYAU_PUBLIC_URL` | no | empty: no link previews. The address people reach TonTuyau at (`https://tontuyau.example.net`, no path), read by the front server: a link shared in Discord, Messenger… then shows the channel, the title and the video. Their servers fetch the page and the video there |

Read by the compose file only:

| Variable | Default |
|---|---|
| `TONTUYAU_VERSION` | `latest`: the images' tag (`1.0.0`, `1.0` or `latest`; or a pre-release to try, such as `1.3.0-alpha.1`) |
| `TONTUYAU_HTTP_PORT` | `80`: the port of the web app on the VM, the only one the stack publishes |
| `TONTUYAU_MEDIA_PATH` | required: the folder of videos on the VM (`<channel id>/<video id>.<ext>`), mounted read-only |

## Logging in

Browsing needs no login. The accounts live in PostgreSQL; at its first start (no account yet) the backend creates the admin, `TONTUYAU_ADMIN_USER` (default `admin`) with `TONTUYAU_ADMIN_PASSWORD`, and refuses to start without that password. Later starts never read them again: changing them in `.env` changes nothing. Passwords are stored as OWASP recommends (Argon2id, 19 MiB, 2 passes, 1 lane, a random salt each), never as such.

`/login` (or "Log in" in the top bar) opens a session: a cookie (HttpOnly, SameSite=Strict, Secure) valid 8 hours and renewed while in use, or 30 days with "Stay logged in". A restart keeps everyone logged in (the cookies' keys are in PostgreSQL). Log out from the menu under your initial.

- **HTTPS**: the cookie is `Secure`, so browsers only keep it over HTTPS (and on `localhost`). Browsing TonTuyau over plain `http://` on the LAN, set `TONTUYAU_COOKIE_SECURE=false`.
- **Guessing passwords**: 5 attempts a minute per client address, then 429. The address is the one the front server saw; **behind a reverse proxy, that's the proxy's**: name it in `TONTUYAU_TRUSTED_PROXIES` and the visitor's address is read from the `X-Forwarded-For` it adds, so each visitor gets their own 5 attempts. Only the proxy's entry is trusted: an address a client writes itself before it doesn't count. Without the setting, everyone behind the proxy shares the 5 attempts.
- Logging out deletes the cookie in the browser; a copy taken before stays valid until it expires (no server-side session list).

## Upgrading

From 1.1: add `TONTUYAU_DB_PASSWORD` and `TONTUYAU_ADMIN_PASSWORD` to `.env`, take the new compose file, then `docker compose up -d` (the new `tontuyau-db` starts first).


After upgrading, **the index rebuilds itself at startup** when the new version changed the index definitions (fields, analysers): the backend notices it, starts a rebuild in the background (a full run into a new index generation, then one alias switch) and keeps serving the current indices meanwhile. It's a sync like the others (same lock, TubeArchivist read-only, the same safety limits). If it fails, or TubeArchivist can't be reached, every following sync rebuilds instead of its normal run until it succeeds. With `TONTUYAU_SYNC_SCHEDULER_ENABLED=false` the backend never starts it by itself: it logs a warning, `tontuyau sync status` says "Index rebuild : pending", and `tontuyau index rebuild` (or any `tontuyau sync`) does it.


## REST API

Read-only for the library, under `/api`, `/media` and `/art`, plus logging in and out (`/api/auth/…`); the contract is [docs/API_CONTRACT.md](docs/API_CONTRACT.md). The backend publishes its OpenAPI document at `/openapi/v1.json`; in Development, a Scalar UI to try it is at `/scalar`.

```bash
curl -s 'localhost:5080/api/home?size=2'
curl -s 'localhost:5080/api/search?q=creme%20brulee'
curl -s 'localhost:5080/api/search/suggest?q=tom%20c'
curl -sI -H 'Range: bytes=0-99' 'localhost:5080/media/<video id>'
```

Lists are cursor-paged (`nextCursor`, exact `total`); errors are RFC 9457 ProblemDetails; every answer carries `X-TonTuyau-Version`.

Health: `GET /health` answers `200 Healthy` as soon as the process runs (the stack's healthcheck uses it, QA14-5: other clients may use the API, so the container's health doesn't depend on Elasticsearch); `GET /health/ready` answers 200 only when Elasticsearch is usable and TonTuyau's indices exist (503 otherwise). Until then, and whenever Elasticsearch is down, the API answers errors: the app shows them with a Retry button. There is no sync endpoint: the sync is driven from the command line below.

## Sync and command line

The backend syncs from TubeArchivist on its own schedule (see [docs/SYNC_DESIGN.md](docs/SYNC_DESIGN.md)). The same binary is also a command line, used inside the container:

```bash
docker exec tontuyau-backend tontuyau sync status
docker exec tontuyau-backend tontuyau sync incremental
docker exec tontuyau-backend tontuyau sync full
docker exec tontuyau-backend tontuyau sync apply-deletions <run>
docker exec tontuyau-backend tontuyau index rebuild
docker exec tontuyau-backend tontuyau --version
```

The commands that run a sync show their progress while they run, then the report: see [The first full sync](#4-the-first-full-sync). Exit codes: 0 done, 1 the run failed or was canceled, 2 usage or configuration error, 3 another sync is running, 4 refused. Only one sync runs at a time across the backend and the CLI. Ctrl-C stops a run: it's recorded as canceled, and what it did until then stays. `apply-deletions` is a run of its own in the history, for the full run whose list it applies (the full run's record stays as that run left it).

## Before going public

TonTuyau is designed for the LAN. Before exposing it to the internet: decide whether browsing should need a login (it doesn't yet), run the planned RCE / path-traversal audit, and regenerate the TubeArchivist API token.

Already done, as if it were public (QA14-3): the front server sends a strict **Content-Security-Policy** on every answer (`default-src 'self'`, images from the site only with `img-src 'self'` (no `data:` URLs), no inline script or style, `object-src 'none'`, `frame-ancestors 'self'`: the full policy is in `frontend/server/server.ts`), with `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and `X-Frame-Options: SAMEORIGIN`. `check.sh` fails if the built page or its stylesheets get anything the policy would block (an inline script or style, a `data:` URL).

## License

[MIT](LICENSE)
