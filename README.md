# TonTuyau

<img src="frontend/public/favicon.svg" alt="" width="64" height="64" align="right">

*Ton tuyau*: French for "your pipe" (or "your tube").

A self-hosted, YouTube-like web UI for the library of a [TubeArchivist](https://github.com/tubearchivist/tubearchivist) instance. Browsing needs no login: every channel and video is listed and searchable (an admin account can log in, for the administration). Since 2.0 TonTuyau keeps the library itself (PostgreSQL): TubeArchivist is only read once, to import what it has, and never modified.

- **Backend**: ASP.NET Core (.NET 10), REST API, its PostgreSQL (the library, the accounts, the runs) and its own Elasticsearch 9 (the search indices, written from PostgreSQL), an import from TubeArchivist at startup.
- **Frontend**: Svelte 5 + Vite + TypeScript, built with pnpm, tested with Vitest.
- **Videos**: the original files, straight from your videos folder, no transcoding; since 2.0 TonTuyau downloads videos there itself (yt-dlp, merged into `.mkv` by ffmpeg).

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

TonTuyau runs as four containers: `tontuyau-reservoir` (its own Elasticsearch: the Reservoir), `tontuyau-db` (its PostgreSQL: the library, the accounts, the runs), `tontuyau-backend` (the API, the import and the `tontuyau` command line) and `tontuyau-frontend` (the web app, which also forwards `/api`, `/media` and `/art` to the backend). Only the front is reachable from the LAN, on port 80 by default. The images are `ghcr.io/tontuyau/tontuyau-backend` and `ghcr.io/tontuyau/tontuyau-frontend` (`linux/amd64`).

### Prerequisites

- A Linux VM (x86-64) with Docker Engine and the Compose plugin (`docker compose version`). The stack sets **no CPU or memory limits**: it uses what the VM has. Elasticsearch takes a fixed heap, `TONTUYAU_ES_HEAP` (default 4 GB), plus its file cache; about 8 GB of RAM in total and 4+ cores are comfortable, and more cores make the import's image resizing faster. To cap a container anyway, add `deploy.resources.limits` in a `docker-compose.override.yml` next to the compose file (example in [releases/1.0.0](docs/releases/1.0.0.md)).
- For Elasticsearch, on the VM: `sudo sysctl -w vm.max_map_count=262144` (and the same line, `vm.max_map_count=262144`, in `/etc/sysctl.d/99-tontuyau.conf` so it survives a reboot).
- About 10 GB of local disk for the volumes (the index, and about 6 GB of images with their resized versions), plus the Docker images (about 3.5 GB, Elasticsearch's being most of it).
- To import a TubeArchivist library: its URL and an API token (TubeArchivist → Settings → API token). TonTuyau only ever reads from it.
- The folder of videos TubeArchivist downloads into (its `youtube` folder: `<channel id>/<video id>.<ext>`), readable on the VM.

### 1. The videos folder

TonTuyau keeps the videos in **one folder** on the VM: TubeArchivist's `youtube` folder, with one folder per channel (`<channel id>/<video id>.<ext>`). Set its path in `.env` as `TONTUYAU_MEDIA_PATH` (e.g. `/mnt/youtube`). Where the folder comes from is up to you (a local folder, a share mounted there).

Since 2.0, TonTuyau **writes its own downloads** there, in the same layout (`<channel id>/<video id>.mkv`), going through a hidden `.tontuyau-downloads` folder first, so a half-downloaded file never shows. The backend runs as uid 1654: give it write access to the folder, for example

```bash
sudo setfacl -R -m u:1654:rwX -m d:u:1654:rwX /mnt/youtube   # or: sudo chown -R 1654:1654 /mnt/youtube
```

On an NFS share, the export must allow writing for that user (or map it with `all_squash`/`anonuid`). TubeArchivist's files are never changed; a download that can't write fails with "The media folder can't be written to".

Check it lists channel folders (`UC…`):

```bash
ls /mnt/youtube | head
```

- If the folder doesn't exist, `docker compose up` stops with an error rather than starting with no videos.
- If it's empty, the import notes it and never marks your files as missing.

### 2. Get the compose file and `.env`

In an empty folder (for example `/opt/tontuyau`), put [`docker/docker-compose.yml`](docker/docker-compose.yml) and [`.env.example`](.env.example) from this repository, then:

```bash
cp .env.example .env
chmod 600 .env
```

Fill in `TONTUYAU_ES_PASSWORD` and `TONTUYAU_DB_PASSWORD` (any long random strings: `openssl rand -base64 24`), and `TONTUYAU_ADMIN_PASSWORD`: the admin account's password, read at the first start only (see [Logging in](#logging-in)). To import a TubeArchivist library, also `TONTUYAU_TA_URL` and `TONTUYAU_TA_TOKEN` (see [The TubeArchivist import](#4-the-tubearchivist-import)). Every other variable has a default; `.env.example` explains each one.

### 3. Start

```bash
docker compose pull && docker compose up -d
docker compose ps        # all four "healthy" after a minute or two
```

The backend's "healthy" means its process answers (`/health`); `/health/ready` also says whether Elasticsearch and TonTuyau's indices are usable (see [REST API](#rest-api)).

Then open `http://<vm-ip>/` (or the port in `TONTUYAU_HTTP_PORT`). The library is empty until the import.

### 4. The TubeArchivist import

With `TONTUYAU_TA_URL` and `TONTUYAU_TA_TOKEN` set, **the backend imports TubeArchivist's library each time it starts**, in the background: the app answers meanwhile. It reads the whole library (read-only), adds the videos and channels TonTuyau doesn't have, copies their images (about 4 GB) and makes their resized versions, then checks their files. On a library of about 45,000 videos it takes **about 20 minutes**. What TonTuyau already has is left as it is, and nothing is ever deleted: run again, an import only adds what's new.

Once it ran (the Tasks page shows it, or `docker exec tontuyau-backend tontuyau status`), **TubeArchivist can be shut down**: remove the two settings from `.env` and `docker compose up -d`. Without them the backend starts without importing, and the Tasks page says why its import can't start.

To follow it, or to run one by hand:

```bash
docker exec -it tontuyau-backend tontuyau import
```

While it runs, the command shows where it is, as plain lines (the same in a terminal and in a log): a line at each step and every 10 seconds, with the time since it started. During a long step with nothing new to count (a slow TubeArchivist answer), the current line is printed again every 10 seconds, so you can see the time go up and know it's still running.

```
[00:00] channels      0 / 150 read
[00:41] videos        12 / 150 channels · 4,210 videos · art 4,180 copied, 8,350 resized
[19:05] media check   45,339 files checked · 2 missing
```

- Each update is a **line of its own**, with or without `-t`, and piped too (`docker exec tontuyau-backend tontuyau import | tee import.log` keeps a copy); each step ends with its final counts.
- A count is shown as "x / y" only when the total is really known (the channels); the videos are counted as they come.
- While the import at startup goes on, `tontuyau import` waits its turn in the queue (2.0), then runs: the Tasks page follows both.

Then it prints the run's report (`tontuyau status` shows it again). `tontuyau index rebuild` shows its progress the same way. The import at startup writes the same progress lines to the backend's log (`docker logs tontuyau-backend`), and every run's lines (its start, progress, warnings, errors, end) are also kept in PostgreSQL, for the admin's Tasks page (1.4).

### Updating

By hand (QA13-10), when you're there to watch:

```bash
docker compose pull && docker compose up -d
```

`TONTUYAU_VERSION` in `.env` picks the version: `latest` by default, or a pinned one like `1.0.0` (or `1.0` for the latest 1.0.x). When a new version changes the index definitions, **the backend rebuilds the index by itself at startup**, from PostgreSQL (the app keeps serving the current index meanwhile): see [Upgrading](#upgrading). `docker exec tontuyau-backend tontuyau --version` says what runs.

### Logs

```bash
docker compose logs -f tontuyau-backend      # the API, the import and the runs
docker compose logs --tail 100 tontuyau-frontend
docker exec tontuyau-backend tontuyau status
```

The images carry only what TonTuyau needs (1.5.1): no shell, no package manager. `docker compose logs`,
`docker exec tontuyau-backend tontuyau …` and `docker cp` work as before; `docker exec … sh` doesn't. To look inside
a container, `docker debug tontuyau-backend` attaches a temporary toolbox without changing the image.

### Using folders instead of volumes (optional)

The stack keeps PostgreSQL's and Elasticsearch's data, the images and the yt-dlp versions in **named volumes**: nothing to do. If you'd rather have them in folders of the VM (bind mounts, e.g. `- /srv/tontuyau/es:/usr/share/elasticsearch/data` in place of `tontuyau-es-data:…`), Docker creates a missing folder as `root`, and the containers can't write there. Give them their owners once, before `docker compose up`:

```bash
sudo chown -R 1000:0 <es-folder> && sudo chmod -R g+rwX <es-folder>   # Elasticsearch runs as uid 1000, group 0
sudo chown -R 1654:1654 <art-folder> <tools-folder>                   # the backend runs as uid 1654
```

Rather than editing `docker-compose.yml`, you can name the folders in a `docker-compose.override.yml` next to it: Compose reads it by itself, each line replaces the volume mounted at the same place in the container, and taking a newer compose file doesn't undo it.

```yaml
services:
  tontuyau-reservoir:
    volumes:
      - /srv/tontuyau/es:/usr/share/elasticsearch/data
  tontuyau-backend:
    volumes:
      - /srv/tontuyau/art:/data/art
      - /srv/tontuyau/tools:/data/tools
```

### Backups

| Volume | Content | Back it up? |
|---|---|---|
| `tontuyau-db-data` | the library (2.0: every video and channel, what YouTube said of them), the accounts, the runs and their logs, the keys of the login cookies (PostgreSQL) | **Yes**: `docker exec tontuyau-db pg_dump -U tontuyau tontuyau > tontuyau-db.sql`. Once TubeArchivist is shut down, it's the only copy of the library |
| `tontuyau-es-data` | TonTuyau's search indices (Elasticsearch) | Not needed: `tontuyau index rebuild` writes them again from PostgreSQL |
| `tontuyau-art` | the images copied from TubeArchivist and their resized versions (about 6 GB) | **Yes** once TubeArchivist is shut down; while it's there, an import copies and resizes what's missing again |
| `tontuyau-tools` | the yt-dlp versions TonTuyau downloaded (1.5), about 40 MB | Not needed: downloaded again |
| `TONTUYAU_MEDIA_PATH` (a folder, not a volume) | your videos: TubeArchivist's, and TonTuyau's downloads (2.0) | **Yes**, as any folder: once TubeArchivist is shut down, TonTuyau's downloads exist only there |
| `.env` | the settings and secrets | **Yes** (it holds the passwords, and the TubeArchivist token while it's there: keep the copy private) |

A volume to a tarball, for example (a folder, see above: back it up like any folder): `docker run --rm -v tontuyau_tontuyau-art:/v:ro -v "$PWD":/b busybox tar czf /b/art.tgz -C /v .` (Compose prefixes the volume names with the project name `tontuyau_`; `docker volume ls` lists them.)

### Command line

The backend's image puts `tontuyau` on the PATH:

```bash
docker exec tontuyau-backend tontuyau status
docker exec tontuyau-backend tontuyau import
docker exec tontuyau-backend tontuyau index rebuild
docker exec tontuyau-backend tontuyau youtube video <id>
docker exec tontuyau-backend tontuyau youtube channel <id>
docker exec tontuyau-backend tontuyau youtube channel-videos <id>
docker exec tontuyau-backend tontuyau youtube download <id>
docker exec tontuyau-backend tontuyau health
docker exec tontuyau-backend tontuyau --version
```

`health` asks the backend running in the container whether it answers (exit code 0 or 1): it's the container's
health check, since the image has no shell nor curl.

See [Import and command line](#import-and-command-line) for what each does and the exit codes.

### Images on GHCR

GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs only on **pull requests** to `main` and on **version tags** (to save Actions minutes: pushing a branch runs nothing, its pull request does; a newer push to an open pull request cancels the run in progress). Each run does `scripts/check.sh` and builds both images. Images are **published only from a version tag** `vX.Y.Z`, when everything passed, as `X.Y.Z`, `X.Y` and `latest`; a pull request builds them without pushing. There are no `main` or `sha-…` image tags any more.

**One-time step after the first push to GHCR** (GitHub creates a new package as private, the first time a version tag publishes it): for each of `tontuyau-backend` and `tontuyau-frontend`, open github.com/TonTuyau → **Packages** → the package → **Package settings**:
1. **Danger Zone → Change visibility → Public** (QA13-3), so the VM pulls without logging in.
2. Check that the package is linked to `TonTuyau/TonTuyau` (the images carry the `org.opencontainers.image.source` label, so GitHub links them by itself; otherwise **Connect repository**), and that **Manage Actions access** lists the repository with write access.

Each release tag (`v1.0.0`, `v1.0.1`, …) publishes `X.Y.Z`, `X.Y` and moves `latest`: pick one of those in `TONTUYAU_VERSION`. A pre-release tag (`v1.3.0-alpha.1`) publishes only its own `1.3.0-alpha.1`, to try a version before its release.

Each month (the 15th), the latest release's images are **rebuilt from the same sources on fresh base images** and
published again under its tags (`.github/workflows/rebuild.yml`), once what they carry runs
(`scripts/smoke-image.sh`): the base's security fixes (Ubuntu, Node, .NET) reach you without a new TonTuyau release;
`docker compose pull && docker compose up -d` takes them. It can be started by hand too (Actions → Rebuild the latest
release).

## Development setup

Prerequisites: .NET 10 SDK, Node 24+, pnpm (via corepack), Docker (for Elasticsearch, PostgreSQL and the integration tests that need them; your user must be in the `docker` group). The integration tests start one PostgreSQL (`postgres:17`) and give each scenario a database of its own. The Elasticsearch integration tests start a container with a 512 MB heap and refuse to start with less than 2 GB of free RAM.

```bash
cp .env.example .env    # then fill in the values, and uncomment the "Development only" lines; .env is git-ignored
./scripts/check.sh      # version check, format check, build, all tests, front and back
```

Run locally:

```bash
docker compose --env-file .env -f docker/docker-compose.yml -f docker/compose.dev.yml up -d tontuyau-reservoir tontuyau-db   # localhost:9200, :5432
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

All settings are environment variables prefixed `TONTUYAU_` (see [.env.example](.env.example)). The backend **refuses to start** with a missing or invalid value and says which one. In the Docker stack, `.env` holds them; the compose file fixes `TONTUYAU_ES_URL` (`http://tontuyau-reservoir:9200`) and the paths (`/youtube`, `/data/art`, `/data/tools`), and passes every other one through (empty = the default below).

| Variable | Required | Default |
|---|---|---|
| `TONTUYAU_TA_URL` | no | empty: no import. TubeArchivist's address, for the import at startup (2.0); both or neither |
| `TONTUYAU_TA_TOKEN` | no | empty. Its API token |
| `TONTUYAU_ES_URL` | yes | |
| `TONTUYAU_ES_USERNAME` | no | `elastic` |
| `TONTUYAU_ES_PASSWORD` | yes | |
| `TONTUYAU_ES_INDEX_PREFIX` | no | `tontuyau` (indices `tontuyau-videos-v1`… behind aliases `tontuyau-videos` / `tontuyau-videos-write`) |
| `TONTUYAU_MEDIA_ROOT` | no | `/youtube` |
| `TONTUYAU_ART_ROOT` | no | `/data/art` |
| `TONTUYAU_TZ` | no | `Europe/Paris`: the dashboard's days |
| `TONTUYAU_TA_RESULT_WINDOW` | no | `10000` (TubeArchivist's list window) |
| `TONTUYAU_ART_RESIZE_PARALLELISM` | no | the machine's core count (in the stack: the backend container's): how many images the import resizes at once (1–64) |
| `TONTUYAU_DB_HOST` | no | `tontuyau-db` (`host` or `host:port`; fixed in the stack) |
| `TONTUYAU_DB_NAME` | no | `tontuyau` |
| `TONTUYAU_DB_USERNAME` | no | `tontuyau` |
| `TONTUYAU_DB_PASSWORD` | yes | |
| `TONTUYAU_ADMIN_USER` | no | `admin`: the account the first start creates |
| `TONTUYAU_ADMIN_PASSWORD` | at the first start | its password; read only while there's no account at all |
| `TONTUYAU_COOKIE_SECURE` | no | `true`: the login cookie only travels over HTTPS; `false` to browse over plain `http://` |
| `TONTUYAU_CLI_SOCKET` | no | `/tmp/tontuyau.sock`: the Unix socket the command line reaches the backend through, inside the container (empty: none, and the command line can't run anything) |
| `TONTUYAU_YTDLP_PATH` | no | `yt-dlp`: the yt-dlp program TonTuyau reads YouTube with (1.5); the image has it, pinned |
| `TONTUYAU_DENO_PATH` | no | `deno`: the Deno program yt-dlp runs YouTube's JavaScript with; the image has it, pinned |
| `TONTUYAU_FFMPEG_PATH` | no | `ffmpeg`: the ffmpeg yt-dlp merges a download with (2.0; ffprobe next to it); the image has both, pinned (FFmpeg 9.0, LGPL) |
| `TONTUYAU_TOOLS_ROOT` | no | `/data/tools`: where the yt-dlp versions TonTuyau downloads go (a volume in the stack) |
| `TONTUYAU_TRUSTED_PROXIES` | no | empty. Your reverse proxy's address (several: commas), so each visitor behind it gets their own login attempts |
| `TONTUYAU_PUBLIC_URL` | no | empty: no link previews. The address people reach TonTuyau at (`https://tontuyau.example.net`, no path), read by the front server: a link shared in Discord, Messenger… then shows the channel, the title and the video. Their servers fetch the page and the video there |

Read by the compose file only:

| Variable | Default |
|---|---|
| `TONTUYAU_VERSION` | `latest`: the images' tag (`1.0.0`, `1.0` or `latest`; or a pre-release to try, such as `1.3.0-alpha.1`) |
| `TONTUYAU_HTTP_PORT` | `80`: the port of the web app on the VM, the only one the stack publishes |
| `TONTUYAU_MEDIA_PATH` | required: the folder of videos on the VM (`<channel id>/<video id>.<ext>`), written to by TonTuyau's downloads (uid 1654) |

## Logging in

Browsing needs no login. The accounts live in PostgreSQL; at its first start (no account yet) the backend creates the admin, `TONTUYAU_ADMIN_USER` (default `admin`) with `TONTUYAU_ADMIN_PASSWORD`, and refuses to start without that password. Later starts never read them again: changing them in `.env` changes nothing. Passwords are stored as OWASP recommends (Argon2id, 19 MiB, 2 passes, 1 lane, a random salt each), never as such.

`/login` (or "Log in" in the top bar) opens a session: a cookie (HttpOnly, SameSite=Strict, Secure) valid 8 hours and renewed while in use, or 30 days with "Stay logged in". A restart keeps everyone logged in (the cookies' keys are in PostgreSQL). Log out from the menu under your initial.

- **HTTPS**: the cookie is `Secure`, so browsers only keep it over HTTPS (and on `localhost`). Browsing TonTuyau over plain `http://` on the LAN, set `TONTUYAU_COOKIE_SECURE=false`.
- **Guessing passwords**: 5 attempts a minute per client address, then 429. The address is the one the front server saw; **behind a reverse proxy, that's the proxy's**: name it in `TONTUYAU_TRUSTED_PROXIES` and the visitor's address is read from the `X-Forwarded-For` it adds, so each visitor gets their own 5 attempts. Only the proxy's entry is trusted: an address a client writes itself before it doesn't count. Without the setting, everyone behind the proxy shares the 5 attempts.
- Logging out deletes the cookie in the browser; a copy taken before stays valid until it expires (no server-side session list).

## Upgrading

From 1.1: add `TONTUYAU_DB_PASSWORD` and `TONTUYAU_ADMIN_PASSWORD` to `.env`, take the new compose file, then `docker compose up -d` (the new `tontuyau-db` starts first).


To 2.0: **start from an empty TonTuyau** and import again. 2.0 keeps the library in PostgreSQL, from new tables; it doesn't read what 1.x stored. Stop the stack, remove its PostgreSQL and Elasticsearch volumes (`docker compose down`, then `docker volume rm tontuyau_tontuyau-db-data tontuyau_tontuyau-es-data`), take the new compose file and `.env.example` (the `TONTUYAU_SYNC_*` settings are gone), keep `TONTUYAU_TA_URL` and `TONTUYAU_TA_TOKEN`, and `docker compose up -d`: the admin account is created again, and the import at startup fills the library. The images (`tontuyau-art`) can stay: they're reused.

After upgrading, **the index rebuilds itself at startup** when the new version changed the index definitions (fields, analysers): the backend notices it, writes the library from PostgreSQL into a new index generation in the background, then switches with one alias swap, and keeps serving the current indices meanwhile. It's a run like the others (same lock). If it fails, the next run (the import, `tontuyau index rebuild`) rebuilds first; `tontuyau status` says "Index rebuild : pending" until it succeeded.


## REST API

Read-only for the library, under `/api`, `/media` and `/art`, plus logging in and out (`/api/auth/…`); the contract is [docs/API_CONTRACT.md](docs/API_CONTRACT.md). The backend publishes its OpenAPI document at `/openapi/v1.json`; in Development, a Scalar UI to try it is at `/scalar`.

```bash
curl -s 'localhost:5080/api/home?size=2'
curl -s 'localhost:5080/api/search?q=creme%20brulee'
curl -s 'localhost:5080/api/search/suggest?q=tom%20c'
curl -sI -H 'Range: bytes=0-99' 'localhost:5080/media/<video id>'
```

Lists are cursor-paged (`nextCursor`, exact `total`); errors are RFC 9457 ProblemDetails; every answer carries `X-TonTuyau-Version`.

Health: `GET /health` answers `200 Healthy` as soon as the process runs (the stack's healthcheck uses it, QA14-5: other clients may use the API, so the container's health doesn't depend on Elasticsearch); `GET /health/ready` answers 200 only when Elasticsearch is usable and TonTuyau's indices exist (503 otherwise). Until then, and whenever Elasticsearch is down, the API answers errors: the app shows them with a Retry button. The runs (the import, the index rebuild, the refreshes on YouTube) are started from the admin's Tasks page or the command line below.

## Import and command line

The backend imports TubeArchivist's library when it starts (see [The TubeArchivist import](#4-the-tubearchivist-import)). The same binary is also a command line, used inside the container:

```bash
docker exec tontuyau-backend tontuyau status
docker exec tontuyau-backend tontuyau import
docker exec tontuyau-backend tontuyau index rebuild
docker exec tontuyau-backend tontuyau --version
```

The commands that start a run show its progress while it runs, then the report. Exit codes: 0 done, 1 the run failed or was canceled, 2 usage or configuration error, 4 refused (an import without TubeArchivist's settings), 5 a refresh put on hold (YouTube refused; 1.5); 3 (another run is going on) isn't used since 2.0. Only one run goes on at a time across the backend and the CLI: since 2.0 a command started while another runs waits its turn in the queue (first come, first served; the Tasks page lists it, with Cancel), prints that it's queued, then follows the run once it starts. While a run is on hold, the queue waits for it. Since 1.4 the command line asks the backend to run the command (through a Unix socket in the container, `TONTUYAU_CLI_SOCKET`, `/tmp/tontuyau.sock` by default: only a process inside it can use it), so the backend must be running; the run is in the backend's log and on the Tasks page, and the command prints the run's log as it comes. Ctrl-C cancels the run in the backend: it's recorded as canceled, what it did until then stays, and the report follows; a second Ctrl-C leaves without waiting. `status` reads the store itself. `youtube …` (1.5) reads a video, a channel, or a channel and each of its videos of the library on YouTube: a run like the import (one at a time with it). `youtube download <id>` (2.0; the id or a YouTube link; the Tasks page's "Download a video" too) downloads a video the library doesn't have into the videos folder (`<channel id>/<id>.mkv`), with the YouTube page's format (`bestvideo+bestaudio` by default), and adds it, with its channel when it's new; a video of the library is refused (exit code 4), unless its file is missing: then its file is downloaded and the flag cleared; YouTube refusing puts it on hold (exit code 5).

## Before going public

TonTuyau is designed for the LAN. Before exposing it to the internet: decide whether browsing should need a login (it doesn't yet), run the planned RCE / path-traversal audit, and regenerate the TubeArchivist API token (or shut TubeArchivist down once imported).

Already done, as if it were public (QA14-3): the front server sends a strict **Content-Security-Policy** on every answer (`default-src 'self'`, images from the site only with `img-src 'self'` (no `data:` URLs), no inline script or style, `object-src 'none'`, `frame-ancestors 'self'`: the full policy is in `frontend/server/server.ts`), with `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and `X-Frame-Options: SAMEORIGIN`. `check.sh` fails if the built page or its stylesheets get anything the policy would block (an inline script or style, a `data:` URL).

## License

[MIT](LICENSE)
