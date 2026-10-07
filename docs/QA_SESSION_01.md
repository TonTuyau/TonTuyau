# TonTuyau — Q&A session 01

> 2026-10-01. Round 1 = your answers to the questions in [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md), with what each answer changes. Round 2 = new questions, **answered 2026-10-01** (what they change: [QA_SESSION_02.md](QA_SESSION_02.md)).
>
> The plan has been updated with the round 1 decisions.

---

## Standing rules (from your message)

- **Tests**: unit tests with xUnit + Xunit.Combinatorial; integration tests with **Reqnroll** (Gherkin `.feature` files, readable by humans).
- **TonTuyau must never modify data in TubeArchivist.** (The TA VM has a backup if it happens.)
  → Enforced in code, not just by convention: the TA HTTP client gets a guard handler that **rejects every method except GET/HEAD**, before anything leaves the process. One unit test per HTTP method checks it.
  → Every probe I ran against TA today was a read-only `GET`.

---

## Round 1 — answers and what they change

| # | Topic | Your answer | What it changes / notes |
|---|---|---|---|
| Q1 | Toolchain | Install what's needed | .NET 10 SDK → `~/.dotnet`, Node 24 LTS → `~/.local/node`, pnpm via corepack. **Docker can't be installed without sudo** → R2-1 |
| Q2 | TA access | `http://192.168.1.40:7000` + API key | Stored only in `TonTuyau/.env` (git-ignored, `chmod 600`). Connection checked: **TA v0.5.12**. See *Live findings* below and R2-23 |
| Q3 | NFS | Mounted on the VM; simulate locally | Local `dev-data/youtube/` (git-ignored) with TA's layout → R2-5 |
| Q4 | Thumbnails/art | Copy into TonTuyau's own volume | TonTuyau becomes independent of TA for images; adds a copy step to the sync → R2-6 |
| Q5 | Deletions/metadata | New videos only; **weekly at night: full metadata refresh + deletion** | Two sync modes: *incremental* and *full*. Full = ~3,740 TA requests at the current page size (see findings) → R2-7 |
| Q6 | Trigger | CLI only, inside the container | No HTTP sync endpoint at all. `docker exec … tontuyau sync …` → R2-8 |
| Q7 | Sync design | Separate session; I list the cases, you think of a solution, we iterate | The case list is in plan §4. **No algorithm written by me** |
| Q8 | Scope v1 | Channels, Shorts, Streams, Download file, Playlists | Comments, SponsorBlock and related videos are out. Subtitles aren't in your list → R2-12. TA has **0 playlists** today → R2-11 |
| Q9 | Watch state | None | No watched/progress anywhere, front or back |
| Q10 | Codecs | Lots of H.264, VP9, AV1; H.265 unknown. Client-side transcoding? | See *Answer to your Q10 question* below → R2-13 |
| Q11 | Language | i18n French + English | → R2-17 |
| Q12 | Look | As close to YouTube as possible, styling easy to swap | Design tokens + theme layer → R2-16 |
| Q13 | Containers | Backend + Frontend + TonTuyau's own ES | **3 containers** (was 2): `tontuyau-backend`, `tontuyau-frontend`, `tontuyau-es` |
| Q14 | Exposure | LAN only for now; public later with login + RCE/path traversal audit | No auth in v1, but path handling is still written traversal-safe (by id, never raw paths) so the later audit has less to fix |
| Q15 | Host | Dedicated VM, max 30 GB RAM | Plenty. Proposal in R2-21 |
| Q16 | Front | Svelte + Vite, served by Node, pnpm | **Not SvelteKit**: plain Svelte + Vite + a client-side router. Node serves it in prod → R2-14 |
| Q17 | Backend | Controllers, layers: Controller → Service → Repository, + Validators that throw on broken data | → R2-15 (exception policy) |
| Q18 | Git | Branch per feature, merged into a milestone branch you review | → R2-20 (naming, who merges to `main`) |
| Q19 | License | MIT | `LICENSE` file added in M0 |
| Q20 | Other | Nothing for now | Round 2 below |

### Answer to your Q10 question: can transcoding run client-side?

Short answer: **there's nothing to transcode when the browser can decode the codec**, and when it can't, real-time transcoding in the browser isn't practical.

- Browsers *decode* the file themselves, in hardware or software. For MP4 files, what matters is which codecs the browser can decode. Rough support as of 2026 (to be checked on your own devices):

| Codec | Chrome / Edge | Firefox | Safari (macOS/iOS) |
|---|---|---|---|
| H.264 | ✅ | ✅ | ✅ |
| VP9 | ✅ | ✅ | ⚠️ depends on version/device |
| AV1 | ✅ (software decode if no hardware) | ✅ | ⚠️ only on chips with AV1 hardware (recent Apple Silicon) |
| H.265/HEVC | ⚠️ only with a hardware decoder | ⚠️ limited | ✅ |
| Opus audio (seen in your files) | ✅ | ✅ | ⚠️ depends on version |

- **Transcoding in the browser** (ffmpeg compiled to WebAssembly) works on short clips, but runs far slower than real time for 1080p/4K. It also has to read the whole file first. Not usable for a player.
- **Software decoding in the browser** (a WASM AV1 decoder feeding WebCodecs) is technically possible. It's a big piece of work, eats CPU on 4K, and isn't worth it for v1.
- **Real risk in your library**: I saw **4K AV1 at ~5 Mbit/s**. Chrome/Firefox decode that in software on machines without AV1 hardware, which can stutter on weak laptops or TVs.

**Proposal**: the backend exposes each video's codec info (`streams[]`). The front asks the browser `canPlayType` / `MediaSource.isTypeSupported` and, if the answer is no, shows *"This browser can't play <codec>"* + the *Download file* button. Asking the browser what it can do is a client capability, not data inference, so I think this keeps your "front doesn't infer anything" rule. Confirm in R2-13.

### Live findings from your TubeArchivist (read-only probes, 2026-10-01)

| Finding | Impact |
|---|---|
| Version **v0.5.12** | We target this version; fixtures are recorded from it |
| **44,884 videos** (37,974 videos / 5,943 shorts / 967 streams), **21.1 TB**, total duration ~2 y 91 d (from `/api/stats/video/`) | Sizing of ES, thumbnail copy, download sizes |
| **14,210 videos are `active: false`** (removed from YouTube, still archived) | → R2-9 |
| **115 channels** (109 subscribed, 6 not) | → R2-10 |
| **Page size is 12** (TA user setting). The `page_size` query param is **ignored** | A full walk = 44,884 / 12 ≈ **3,740 requests**. Changing TA's page size would be editing TA → R2-22 |
| `total_hits` **stops at 10,000** when `max_hits: true`. Even the *count* is capped on list endpoints; only `/api/stats/*` gives true totals | Sync can't rely on `total_hits` from list endpoints |
| **`page=0` and `page=1` return the same page** (confirmed: 120 channels fetched over pages 0–9 → 108 unique) | Classic duplicate/skip trap for any pager. Unit-tested in the TA client |
| Biggest channel: **5,396 videos** (next: 4,812, 2,561) | Under the 10k cap today, but not guaranteed forever |
| **0 playlists** in TA | → R2-11 |
| Codecs seen: **av1, h264, vp9** video; **opus** audio; containers `.mp4`; up to 3840×2160 | Q10 / R2-13 |
| `media_url` = `/youtube/<channel_id>/<video_id>.mp4`, `vid_thumb_url` = `/cache/videos/<first char>/<video_id>.jpg`, channel `thumb`/`banner`/`tvart` under `/cache/channels/` | Local simulation layout; image copy sources |
| `subtitles: []` on the samples | → R2-12 |
| Dates: `date_downloaded` is epoch seconds (int); `published`, `vid_last_refresh`, `channel_last_refresh` are ISO-8601 strings | Validator must accept both; the mapping normalises them |

---

## Round 2 — new questions

### Environment & tests

**R2-1. Docker on the laptop.** Docker needs a system install (sudo), which I can't do. Without it here: no Reqnroll integration tests against a real ES (Testcontainers) and no local image builds. Options:
  a) you install Docker Engine (`apt install docker.io` or docker-ce) and add your user to the `docker` group
  b) rootless Docker / Podman (still needs the `uidmap` package, so sudo once)
  c) no Docker here: integration tests and builds run only on the VM
Default: a)
**Answer:** I will install Docker and add myself to the group.

**R2-2. Reqnroll scope.** Integration tests = backend running for real (in-process `WebApplicationFactory`) + real ES (Testcontainers) + **fake TA** (WireMock.Net). Should Reqnroll also drive the **UI end-to-end** (Playwright for .NET), or stay at the HTTP/API level?
Default: API level for v1; UI end-to-end later.
**Answer:** Default for v1.

**R2-3. Who writes the `.feature` files, and in which language?** Gherkin is a good place for *you* to state expected behaviour (especially sync). Reqnroll supports French keywords (`# language: fr` → *Étant donné / Quand / Alors*).
  a) you write the scenarios, I write step definitions + code
  b) I write them, you review
  c) mixed: you write the sync ones, I write the rest
Language: English or French?
Default: c), English.
**Answer:** Option b), in English.

**R2-4. Test data from your real TA.** Automated tests **never call the real TA**. They replay JSON fixtures recorded read-only from it. These fixtures contain your real data (channel names, titles, descriptions). Since the repo is MIT and may go public later: commit them as-is, or **anonymise** them (the recording script rewrites names/titles/descriptions, keeps structure and edge cases)?
Default: anonymise.
**Answer:** Anonymise.

**R2-5. Local NFS simulation.** Options:
  a) generate short clips with `ffmpeg` (installed here): H.264, VP9, AV1 (+ one HEVC to test the "can't play" path) in TA's `<channel_id>/<video_id>.mp4` layout, matching the fixtures
  b) download a handful of real files from TA (read-only `GET`, but GBs: your sample 4K file is 1.3 GB)
Default: a)
**Answer:** Download a handful of videos, I have space for this.

### Sync (operational questions only, the algorithm stays for the dedicated session)

**R2-6. Image copy.** ~44.9k video thumbnails + 3 images × 115 channels. Copy as-is (JPEG, ~2–4 GB estimated), or also generate smaller sizes / WebP (faster grids, more CPU during sync)?
Default: as-is for v1.
**Answer:** Default.

**R2-7. Schedule.** How often does the *incremental* sync run? Which night and time for the *weekly full* run, in which timezone?
Default: incremental every 15 min; full on Sunday 03:00 `Europe/Paris`; both configurable through env (cron syntax).
**Answer:** Weekly full run every Saturday night at 1 AM Paris time; incremental run every night at 1 AM.

**R2-8. CLI commands.** Proposed: `tontuyau sync incremental`, `tontuyau sync full`, `tontuyau sync status`, plus `tontuyau index rebuild` (drop & re-import TonTuyau's ES from scratch). Anything to add or drop? (Note: a CLI run and the scheduled run from the server process may both happen at once. That's in the sync case list.)
Default: those four.
**Answer:** Default.

### Features

**R2-9. Inactive videos** (14,210, removed from YouTube, still archived by TA): show normally / show with a *"Removed from YouTube"* badge (the backend sends the flag, the front just displays it) / hide?
Default: show with badge.
**Answer:** Show with a badge.

**R2-10. Unsubscribed channels** (6): you said "every available channel". I read that as all 115, subscription status ignored. Right?
Default: all 115, status not shown.
**Answer:** Default.

**R2-11. Playlists.** TA has 0 playlists today, so the feature can only be tested on fake data. Keep in v1, or move until you actually have some?
Default: keep in v1 (fixtures), low priority inside the milestone.
**Answer:** Remove it from v1.

**R2-12. Subtitles.** Not in your Q8 list, and none on the samples. Confirm: out of v1?
Default: out.
**Answer:** Out.

**R2-13. Unplayable codec.** OK with the proposal above (backend exposes codecs, browser capability check, message + download button)? And which devices matter (desktop browsers only, phones, a TV)?
Default: yes; desktop Chrome/Firefox first.
**Answer:** Default.

**R2-14. Front in production.** With plain Svelte + Vite, `vite preview` isn't meant for production. Proposal: the front container runs a small Node server that serves the built files **and reverse-proxies `/api`, `/media`, `/art` to the backend**, so the browser sees one origin and no CORS is needed. Alternative: the browser calls the backend on its own port (CORS on the backend).
Default: Node server + proxy.
**Answer:** Default.

**R2-15. Validators throwing on broken data.**
  - Incoming API requests (bad sort value, bad cursor…) → validator throws → mapped to HTTP **400** with a ProblemDetails body (RFC 9457). OK?
  - Data from TA failing validation during sync → throws. Should it **abort the whole run**, or **skip that item, log it, and report it** in `sync status`? (This feeds into the sync design, but the policy is yours to pick.)
  - "Repository" layer: the ES data access = repositories. The TA access = a read-only *client/gateway* (not a repository, since we never store there). OK with that naming?
Default: 400 + ProblemDetails; skip + report; naming as described.
**Answer:** Default.

**R2-16. Swappable styling — how far?**
  a) **theme = tokens only** (colors, fonts, radii, spacing as CSS custom properties; components never hard-code them). Swapping a theme = swapping one file.
  b) a) + **layout slots** (header/sidebar/grid components replaceable per theme)
  Runtime-selectable (a theme picker) or chosen at build time?
Default: a), build-time, plus a light/dark switch that follows the system.
**Answer:** Default.

**R2-17. i18n details.** Initial language from the browser, with a FR/EN switcher; remember the choice in `localStorage` (a UI preference, not data). Library: `svelte-i18n`, or a tiny typed dictionary of our own? Dates/numbers formatted by the browser's `Intl` (e.g. "224 k abonnés" / "224K subscribers").
Default: all of that, with our own typed dictionary (missing-key = compile error).
**Answer:** Default.

**R2-18. Search behaviour.** Your content is mostly French, with some English.
  - Fields searched: title, channel name, tags, description. Anything else?
  - Analyzer: French stemming + accent folding (`creme` finds *crème*), English stemming on a sub-field, edge-ngram for as-you-type?
  - Search filters in v1: type (video/short/stream), channel, upload date, duration?
Default: all of the above.
**Answer:** Default.

**R2-19. Home page & sorting.** YouTube's home is recommendations, which we can't do. Home = **latest downloaded**, or **latest published**? Channel page sort options: latest / oldest / most viewed (like YouTube)?
Default: home = latest published across all channels, in sections (Videos / Shorts / Streams); channel sorts as YouTube.
**Answer:** Default.

### Process & infra

**R2-20. Branch flow.** Proposal:
  - `main` ← only receives reviewed milestones
  - `milestone/m1-ta-client` ← branched from `main`
  - `feature/m1-ta-paging`, `feature/m1-readonly-guard` … ← branched from the milestone, merged back with `--no-ff` by me
  - you review the milestone branch; **who merges it into `main`, you or me after your OK?**
  - Do I wait for your review before starting the next milestone, or branch it off the unreviewed one?
  - The docs (this file, the plan, LICENSE) go in as the first commit on `main`?
Default: as above; I merge after your OK; next milestone branches off the previous one while you review; docs = first commit on `main`.
**Answer:** Default + an MD documentation for each milestone.

**R2-21. Resources on the VM** (30 GB max). Proposal: ES heap 4 GB (container limit 8 GB), backend limit 1 GB, front 256 MB. ~45k docs is small for ES. How many CPU cores does the VM have? Where do TonTuyau's volumes live (ES data + ~4 GB images): local VM disk or NFS? (ES on NFS isn't recommended.)
Default: as proposed, ES data on local VM disk.
**Answer:** I can give 6 cores; volumes will live locally.

**R2-22. TA page size.** TA returns 12 items per page. Raising it (TA settings → page size) would cut the weekly full run from ~3,740 requests to ~450 at 100/page. That's a change in TA, so it's **your call and your hands**. TonTuyau will never touch it, and the code won't assume any page size.
Default: leave TA as-is.
**Answer:** Increased to 100.

**R2-23. API key hygiene.** The key is now in `.env` (git-ignored) **and** in this chat transcript. Regenerating it in TA (settings → API token) would be an edit to TA, so it's yours to do if you want; then update `.env`. LAN-only for now, so it's low risk.
Default: keep it for now; regenerate before any public exposure.
**Answer:** Default.

**R2-24. ES version & security.** Elasticsearch **9.x** (current) with the matching official .NET client, security enabled (password for the `elastic` user, from `.env`) even on the LAN? Or 8.x?
Default: 9.x, security on.
**Answer:** Default.

**R2-25. Code style & tooling.** `.editorconfig` + `dotnet format`, nullable reference types + warnings as errors on the backend; ESLint + Prettier + `svelte-check` (TypeScript strict) on the front; OpenAPI document generated by the backend with a Scalar UI in Development only; built-in .NET logging as JSON to stdout. Anything you want different?
Default: all of that.
**Answer:** Default.

**R2-26. Anything else?**
**Answer:** Nothing more for now.
