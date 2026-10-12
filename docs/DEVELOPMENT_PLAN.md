# TonTuyau — Development Plan

> Status: **v13: TonTuyau 1.1.0 (QA15/QA16 follow-ups, timecodes, the Firefox shorts fixes); next: M8, real data on the VM** (2026-10-07)
>
> Decisions and open questions live in the Q&A files:
> - [QA_SESSION_01.md](QA_SESSION_01.md): round 1 answers + round 2 questions and answers
> - [QA_SESSION_02.md](QA_SESSION_02.md): what round 2 changes + round 3 questions and answers
> - [QA_SESSION_03.md](QA_SESSION_03.md): what round 3 changes, M1 findings, round 4 questions
> - [QA_SESSION_04.md](QA_SESSION_04.md): questions from building the sync (M3)
> - [QA_SESSION_05.md](QA_SESSION_05.md): before M4/M5a (parallel work, suggestions, download names, no sync endpoint)
> - [QA_SESSION_06.md](QA_SESSION_06.md): open questions of M4 and M5a
> - [QA_SESSION_07.md](QA_SESSION_07.md): shorts player, phone layout, links (M5a)
> - [QA_SESSION_08.md](QA_SESSION_08.md): front performance (resized art at sync, M5)
> - [QA_SESSION_09.md](QA_SESSION_09.md): resized art details (M5)
> - [QA_SESSION_10.md](QA_SESSION_10.md): M5 front (search prefix, Dolby audio, states, player, tags)
> - [QA_SESSION_11.md](QA_SESSION_11.md): M5 QA10 round (auto-rebuild switch, search details, readiness output)
> - [QA_SESSION_13.md](QA_SESSION_13.md): M7 Docker + GHCR
> - [QA_SESSION_14.md](QA_SESSION_14.md): M7 choices to confirm (base image, headers, healthcheck, tagging, GHCR visibility)
> - [QA_SESSION_15.md](QA_SESSION_15.md): M7 follow-ups (the not-ready window, `data:` images, backend headers)
> - [QA_SESSION_16.md](QA_SESSION_16.md): sync progress and wide grids (the progress on a terminal, heartbeat, a real-data look, grid caps)
> - [QA_SESSION_17.md](QA_SESSION_17.md): 1.1.0 choices (the CSP check, the heartbeat's rhythm, `t=` on load, the shorts wheel, the shorts overlay)
> - [FUTURE_TA_REPLACEMENT.md](FUTURE_TA_REPLACEMENT.md): future plan, replacing TubeArchivist (after M8)
> - [FUTURE_FEDERATION.md](FUTURE_FEDERATION.md): future plan, federation between TonTuyau instances (private) and through a central instance (public)
> - [API_CONTRACT.md](API_CONTRACT.md): the REST API contract
> - [SYNC_DESIGN.md](SYNC_DESIGN.md): the sync discussion
>
> (Rn-m) tags point to the question a decision comes from.

---

## 1. Goal

A self-hostable, YouTube-like web UI that runs in Docker and browses the library a [TubeArchivist](https://github.com/tubearchivist/tubearchivist) (TA) instance has already downloaded.

- No login, no subscriptions (for now). Every channel and video is listed and searchable.
- Data flow is one-way: **TA → TonTuyau backend → TonTuyau's Elasticsearch → REST API → Svelte front.**
- **TonTuyau never modifies TA**, enforced in code (see §3, *Read-only guard*). It never downloads from YouTube.
- Videos are served as-is from a read-only NFS mount. **No transcoding** (no GPU).
- Library today: **44,971 videos, 115 channels, 21.1 TB** (TA v0.5.12, scan of 2026-10-01). The design must hold at 10× that.

### In scope (v1)

Home, channel list, channel page, watch page, search across videos + channels, **Shorts** and **Streams** as their own tabs/sections, **Download original file**, **FR/EN i18n**, YouTube-like look with swappable styling.

### Out of scope (v1)

Accounts/login, subscriptions, watch state (watched/progress), **playlists (R2-11)**, comments, SponsorBlock, related videos, subtitles (R2-12), transcoding/adaptive streaming, editing anything, public internet exposure.

---

## 2. Facts about TubeArchivist that drive the design

From the TA docs/source and **read-only probes of your instance (v0.5.12)**. Full details in [QA_SESSION_01.md → Live findings](QA_SESSION_01.md#live-findings-from-your-tubearchivist-read-only-probes-2026-10-01).

| Fact | Consequence |
|---|---|
| Auth: `Authorization: Token <token>` | Token only in env / `.env` (git-ignored) |
| Page size set in TA's user config (was 12, **raised to 100** by you, R2-22); `page_size` query param ignored | Full walk ≈ 450 requests. Code reads `page_size` from each response, never assumes it |
| List endpoints stop at **10,000 hits**; `total_hits` is capped too | No true totals from list endpoints; `/api/stats/video/` gives real counts |
| Pages are 1-based (`page=0` = `page=1`); `last_page` is unreliable; past the end → **200 + empty list**; past the 10k window → **500** | The client never trusts `last_page`; an empty page means the end; 500 is a typed error |
| `/api/video/` params: `sort=published\|downloaded\|views\|likes\|duration` (`filesize` is **rejected** by v0.5.12 despite the docstring), `order=asc\|desc`, `channel=`, `playlist=`, `type=videos\|streams\|shorts` | Tools available for sync and for checking our counts |
| Biggest channel: 5,396 videos | Under the cap today, not guaranteed forever |
| 14,210 videos `active: false` (gone from YouTube) | Shown with a badge (R2-9) |
| 0 playlists today | Playlists out of v1; `playlist[]` ids still indexed (R2-11) |
| Codecs: AV1 (up to 4K), VP9, H.264 video; Opus audio; `.mp4` | Browser playback compatibility (R2-13) |
| `media_url` `/youtube/<channel_id>/<video_id>.mp4` (served by TA at `<TA_URL>/youtube/…` with the token); `vid_thumb_url` `/cache/videos/<c>/<id>.jpg`; channel `thumb`/`banner`/`tvart` under `/cache/channels/` | Media mapping; image copy sources |
| `date_downloaded` epoch int; `published`, `*_last_refresh` ISO-8601 strings | Validators accept both; mapping normalises them |
| `player.watched/position` are per TA user | Ignored (no watch state) |

---

## 3. Architecture

```
                 ┌──────────────────────────────── docker-compose (VM) ─────────────────────────────────┐
                 │                                                                                      │
 Browser ─HTTP─▶ │  tontuyau-frontend (Node)                                                             │
   (LAN)         │   ├─ serves the Svelte/Vite build                                                    │
                 │   └─ proxies /api, /media, /art ─────────┐   (R2-14)                                 │
                 │                                          ▼                                           │
                 │  tontuyau-backend (ASP.NET Core, .NET 10)                                             │
                 │   ├─ Controllers → Services → Repositories (+ Validators)                            │
                 │   ├─ /api/*   REST, read-only                                                        │
                 │   ├─ /media/* video files, HTTP range requests      ◀── NFS /youtube (read-only)     │
                 │   ├─ /art/*   thumbnails & channel art              ◀── volume tontuyau-art           │
                 │   ├─ SyncScheduler (incremental + weekly full)                                       │
                 │   └─ CLI: `tontuyau sync …` (docker exec)                                             │
                 │              │                                                                       │
                 │              ▼                                                                       │
                 │  tontuyau-es (Elasticsearch 9.x, single node, security on)  ◀── volume tontuyau-es    │
                 └──────────────┬───────────────────────────────────────────────────────────────────────┘
                                │ GET/HEAD only + Token
                                ▼
                     TubeArchivist v0.5.12 (192.168.1.40:7000, untouched)
```

### Backend: ASP.NET Core (.NET 10 LTS)

**Layers** (your Q17):

| Layer | Responsibility | Never does |
|---|---|---|
| **Controller** | HTTP in/out, route + query binding, calls one service method, maps results to DTOs | business rules, ES queries |
| **Validator** | checks inputs (API requests) and incoming TA data; **throws** a typed exception (`ValidationException`, `BrokenSourceDataException`) on broken data | silently fix data |
| **Service** | use cases (list videos, search, get channel, run sync…), orchestrates validators/repositories/clients | HTTP or ES details |
| **Repository** | TonTuyau's ES data access (videos, channels, sync state) | business decisions |
| **TA client (gateway)** | typed read-only HTTP client for TA, DTOs, pagination quirks | anything but GET/HEAD |

Exceptions are mapped once, in middleware, to **ProblemDetails** (RFC 9457): validation → 400, not found → 404, unexpected → 500 with no internals leaked (R2-15).

**Read-only guard**: a `DelegatingHandler` on the TA `HttpClient` throws on any method other than GET/HEAD *before* the request is sent. Tested for every `HttpMethod`. No code path in TonTuyau builds a TA write request.

**Other pieces**
- ES client: `Elastic.Clients.Elasticsearch` (major version matching ES, R2-24).
- Resilience: `Microsoft.Extensions.Http.Resilience` (retry with backoff + timeout) on the TA client.
- Scheduling: a `BackgroundService` + `Cronos` cron expressions; `TONTUYAU_SYNC_INCREMENTAL_CRON` (default hourly 07:00–23:00), `TONTUYAU_SYNC_FULL_CRON` (default the night Friday → Saturday at 01:00, R3-1), `TONTUYAU_TZ=Europe/Paris` (R2-7).
- CLI: the same binary takes verbs: `sync incremental | sync full | sync status | index rebuild` (R2-8). No HTTP sync endpoint.
- Media: resolved **by video id → indexed path → checked to be under the media root**, served with `enableRangeProcessing: true`. Download = same file with `Content-Disposition: attachment; filename="<title>.mp4"`.
- Art: copied from TA into the `tontuyau-art` volume during sync, served with long-lived cache headers + ETag.
- OpenAPI document generated by the backend; Scalar UI in Development only (R2-25).
- Health: `/health` (liveness), `/health/ready` (ES reachable).

### Elasticsearch: TonTuyau's own instance

| Alias | Content |
|---|---|
| `tontuyau-videos` | one doc per video (incl. shorts/streams, `vid_type`, `active`, codecs) |
| `tontuyau-channels` | one doc per channel (+ per-type video counts, computed by the backend) |
| `tontuyau-sync` | sync state / run history (shape from SYNC_DESIGN.md; created in M3) |

Versioned indices behind a read alias and a write alias (`tontuyau-videos-v1` ← `tontuyau-videos`, `tontuyau-videos-write`), so `index rebuild` writes a new generation and swaps reads atomically (done in M2). French analyzer + accent folding, English sub-field, edge-ngram for as-you-type; searched fields: title, channel name, tags, description (R2-18). Paging with `search_after` (cursors), never deep `from/size`.

### Frontend: Svelte + Vite (no SvelteKit)

- Svelte 5 + TypeScript (strict) + Vite, **pnpm**, **Vitest** (+ `@testing-library/svelte`).
- Client-side router (lightweight, e.g. `svelte-spa-router` or our own small hash/history router; chosen in M5).
- Production: a small Node server serves `dist/` and proxies `/api`, `/media`, `/art` to the backend (R2-14).
- **No inference**: no client-side counting, sorting, filtering or deriving. Lists, counts, badges, sort orders, labels from data: all from the API. The only client-side checks are *browser capabilities* (can this browser play AV1?) and UI preferences (language, theme).
- API types generated from the backend's OpenAPI (`openapi-typescript`).
- **i18n**: FR + EN, typed dictionary, browser language by default + switcher, `Intl` for dates/numbers (R2-17).
- **Styling**: YouTube-like. All visual values are CSS custom properties (design tokens) in a theme file; components never hard-code colours/fonts/spacing. Light/dark follow the system (R2-16).
- Pages: Home (latest published, sections per type, R2-19) · Channels · Channel (Videos / Shorts / Streams tabs, YouTube sorts) · Watch (badge for inactive videos, R2-9) · Search.

### REST API

The contract is **[API_CONTRACT.md](API_CONTRACT.md)** (endpoints, parameters, JSON shapes, errors), shared by the backend (M4) and the front (M5a and later). Read-only, cursor-paged lists, ProblemDetails errors, every URL and count given by the API. No sync endpoint (CLI only, Q5-4).

### Repository layout

```
TonTuyau/
├─ backend/
│  ├─ TonTuyau.slnx
│  ├─ src/
│  │  ├─ TonTuyau.Api/              # host, controllers, middleware, media/art endpoints, CLI verbs
│  │  ├─ TonTuyau.Application/      # services, validators, exceptions, domain models
│  │  ├─ TonTuyau.Infrastructure/   # ES repositories, index definitions
│  │  └─ TonTuyau.TubeArchivist/    # read-only TA client, DTOs, read-only guard, pager
│  └─ tests/
│     ├─ TonTuyau.*.UnitTests/      # xUnit + Xunit.Combinatorial, one per src project
│     └─ TonTuyau.IntegrationTests/ # Reqnroll (.feature files) + Testcontainers ES + WireMock TA
├─ frontend/                       # Svelte + Vite + pnpm + Vitest, + server/ (Node prod server)
├─ docker/
│  ├─ backend.Dockerfile
│  ├─ frontend.Dockerfile
│  └─ docker-compose.yml           # backend + frontend + es
├─ tools/
│  ├─ record-fixtures/             # read-only TA recorder (+ anonymiser, R2-4)
│  └─ fetch-dev-media/             # read-only download of a few real videos + art into dev-data/ (R2-5, R3-2)
├─ dev-data/                       # git-ignored: simulated NFS + art
├─ docs/
├─ scripts/check.sh                # build + all tests, front and back
├─ .env.example
├─ LICENSE                         # MIT
└─ README.md
```

---

## 4. The sync job

Its own document and discussion: **[SYNC_DESIGN.md](SYNC_DESIGN.md)**, **agreed (v5) as the M3 spec**. TA pagination facts, modes, requirements R1–R8, cases 1–29 to survive, and your design + review rounds. The schedule (R3-1): incremental during the day (default hourly 07:00–23:00), full the night Friday → Saturday at 01:00, `Europe/Paris`.

---

## 5. Testing strategy

### Unit tests: xUnit + Xunit.Combinatorial

- One test project per source project.
- `[Theory, CombinatorialData]` for parameter matrices (e.g. `sort × order × type` for query building; every `HttpMethod` for the read-only guard; page sizes × totals for the pager), `[PairwiseData]` when the full matrix gets too big.
- Validators: each rule has passing and failing cases; failures assert the exception type + message.
- TA client: fake `HttpMessageHandler` replaying recorded fixtures (incl. the 404-past-end and page 0/1 quirks).
- Media endpoint: ranges (start-end, open-ended, suffix, multiple, unsatisfiable → 416), traversal attempts in ids.
- Front (Vitest): components fed API-shaped data through a mocked `fetch`. Tests check that what's displayed = what the API returned, no more.

### Integration tests: Reqnroll

- Human-readable `.feature` files (Gherkin) with step definitions in C#. Runner: Reqnroll + xUnit.
- System under test: the real backend in-process (`WebApplicationFactory`) + **real Elasticsearch** (Testcontainers) + **fake TA** (WireMock.Net serving recorded fixtures). **The real TA is never called by tests.**
- Feature files (initial): `Browse.feature`, `Search.feature`, `Media.feature`, `ReadOnlyTubeArchivist.feature`, `Sync.feature` (after the design session).
- Written by me in English, reviewed by you (R2-3). API level only for v1 (R2-2). Docker required on the machine running them (R2-1).
- Fixtures are recorded read-only from your TA and **anonymised** before commit (R2-4).

### One command

`scripts/check.sh`: backend build + unit + integration tests, front lint + typecheck + Vitest.

---

## 6. Git workflow (Q18)

- `main`: reviewed milestones only.
- `milestone/mN-<name>`: branched from `main` (or from the previous milestone while it's in review, R2-20).
- `feature/mN-<name>`: branched from its milestone, merged back with `--no-ff` once its tests pass.
- Each milestone branch carries `docs/milestones/MN-<name>.md` (what was built, how to check it, tests, decisions, deviations, limits; R2-20).
- You review the milestone branch; I merge it into `main` after your OK. The next milestone may branch off the one in review.
- Conventional-commit messages. Secrets never committed (`.env` git-ignored, `.env.example` committed).

---

## 7. Milestones

| # | Milestone branch | Features (branches) | Done when |
|---|---|---|---|
| M0 | `milestone/m0-skeleton` | solution + projects, Vite app, compose skeleton, `check.sh`, LICENSE, README, `.env.example` | everything builds; placeholder unit + Reqnroll + Vitest tests pass |
| M1 | `milestone/m1-ta-client` | read-only guard, DTOs + validators, pager (quirks), fixture recorder (+anonymiser) | TA client fully unit-tested on recorded fixtures |
| M2 | `milestone/m2-search-store` | index definitions + analyzers, repositories, `index rebuild` | Reqnroll scenarios against real ES pass |
| — | **Sync design discussion** | [SYNC_DESIGN.md](SYNC_DESIGN.md), iterated | ✅ agreed 2026-10-01 (v5) |
| M3 | `milestone/m3-sync` | incremental, full, art copy, scheduler, CLI, status | every §4 case is a passing scenario/test |
| M4 | `milestone/m4-api` | controllers/services, ProblemDetails, media/download/art serving, OpenAPI | `Browse`/`Search`/`Media` features pass |
| M5a | `milestone/m5a-front-mockup` | clickable mockup of every page, fed by mock data shaped like the API, that you review **live** in the browser while I adjust it | you approve each page; that approval is the spec for M5/M6 |
| M5 (+M6) | `milestone/m5-front` | **the real front, browse and search together** (M6 folded in, 2026-10-04: search was already built in the mockup), **built from the approved M5a code**: real API instead of the mock (kept for tests/offline), loading/error/not-found states, player fits the video's ratio + small/large player, resized art made by the sync (Q8-1, contract v1.4), real-data check on the trial index | Vitest + backend suites pass; usable against the dev stack with real data |
| M7 | `milestone/m7-docker` | Dockerfiles, compose with resource limits (ES 4 GB heap / 8 GB, 3 CPU; backend 1 GB / 2 CPU; front 256 MB / 1 CPU; volumes on local disk, R2-21), Node prod server, README install guide; GitHub Actions publishing to GHCR, version 0.7.0 (QA13) | `docker compose up` gives a working stack on the VM. Built 2026-10-06, notes: [M7-docker.md](milestones/M7-docker.md) |
| **1.0.0** | `main`, tag `v1.0.0` | **the first release**: M0–M7 and their follow-ups (Content-Security-Policy, generic `/health`, the videos as one folder, command-line sync progress, wide-screen grids, the watch list scrolling on, no resource limits, the compose file checked, the pipe logo, CI on pull requests and version tags only) | notes: [releases/1.0.0.md](releases/1.0.0.md) |
| **1.1.0** | `release/1.1.0` → `main`, tag `v1.1.0` | QA15-2 b (`img-src 'self'`), QA16-1 (plain progress lines everywhere), QA16-3 b (heartbeat), clickable timecodes in descriptions and `&t=` on the watch page, the Firefox shorts stall (left videos released, one short per wheel gesture), the shorts overlay on the video's bottom edge | notes: [releases/1.1.0.md](releases/1.1.0.md) |
| **1.2.0** | `release/1.2.0` → `main`, tag `v1.2.0` | the first part of the administration: accounts in PostgreSQL (OWASP Argon2id), logging in (5 attempts a minute per visitor, `TONTUYAU_TRUSTED_PROXIES`), the admin dashboard (totals, play time, import history by period, every channel) | notes: [releases/1.2.0.md](releases/1.2.0.md) |
| **1.2.1** | `work/1.2.1-last-day` → `main`, tag `v1.2.1` | the dashboard's last day: a bar for every sync, an empty one too | notes: [releases/1.2.1.md](releases/1.2.1.md) |
| **1.2.2** | `work/1.2.2-log-scale` → `main`, tag `v1.2.2` | the import history on a linear or log scale, remembered | notes: [releases/1.2.2.md](releases/1.2.2.md) |
| **1.3.0** | `release/1.3.0` → `main`, tag `v1.3.0` | link previews in chat apps (Discord, Messenger…): a video's channel, title and player, a channel's name, description and icon (`TONTUYAU_PUBLIC_URL`) | notes: [releases/1.3.0.md](releases/1.3.0.md) |
| **1.4.0** | `release/1.4.0` → `main`, tag `v1.4.0` | the Tasks page: the incremental sync, the full sync and applying deletions, run and canceled from the page or the command line, every run's history and log | notes: [releases/1.4.0.md](releases/1.4.0.md) |
| M8 | a `release/1.x` branch | first real import from your TA, measurements | import time, search latency and RAM recorded in the README |
| Later | — | **Replace TubeArchivist** (your idea, 2026-10-05): LDAP admin login (`tontuyau_admin` only, browsing stays open), admin UI, own C# downloader around yt-dlp with fine-grained rescans, full read-only dump of TubeArchivist's Elasticsearch, switch-over in stages | see [FUTURE_TA_REPLACEMENT.md](FUTURE_TA_REPLACEMENT.md); not planned in detail yet |
| Later | — | **TubeArchivist Companion compatibility**: the admin API answers the browser extension's requests like TubeArchivist does (same paths, token, JSON), so the Companion feeds TonTuyau's downloader | part of the replacement; see [FUTURE_TA_REPLACEMENT.md](FUTURE_TA_REPLACEMENT.md) |
| Later | — | **Private federation**: TonTuyau instances linked to each other (browse, search, watch the linked libraries) | see [FUTURE_FEDERATION.md](FUTURE_FEDERATION.md) |
| Later | — | **Public federation**: one central instance handles login and reaches every joined instance's library | after the private one and the legal question; see [FUTURE_FEDERATION.md](FUTURE_FEDERATION.md) |

M0–M2 don't depend on the sync design, so they can go ahead while you think about it.

**Front workflow (your request, 2026-10-01):** before the real front is built, a **mockup** is reviewed live: I change it, you see it immediately, we iterate until you're OK, and only then is it built for real in the app (M5/M6). Details are settled when M5a starts (see the open question in the Q&A file at that point).

---

## 8. Sources

- TubeArchivist: https://github.com/tubearchivist/tubearchivist · API intro: https://docs.tubearchivist.com/api/introduction/
- Source read: `backend/video/views.py`, `backend/video/serializers.py`, `backend/channel/views.py`
- Read-only probes of your TA v0.5.12 on 2026-10-01: `/api/ping/`, `/api/stats/video/`, `/api/stats/channel/`, `/api/video/`, `/api/channel/`, `/api/playlist/`
