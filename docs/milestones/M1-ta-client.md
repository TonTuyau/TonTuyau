# M1: TubeArchivist client

Branch: `milestone/m1-ta-client` · Features: `feature/m1-docs-schedule`, `feature/m1-ta-client-core`, `feature/m1-docs`

## What was built

**Contract** (`TonTuyau.Application/TubeArchivist`, `TonTuyau.Application/Library`)
- `ITubeArchivistGateway`: read-only **building blocks** (ping, library stats, one channel page, one video page, one video, one channel, one cache image). No sync logic: that's for [SYNC_DESIGN.md](../SYNC_DESIGN.md).
- Domain records `Video`, `Channel`, `MediaTrack`, `VideoType`. `Video.MediaPath` is relative to the media root (`<channel>/<id>.mp4`).
- `Page<T>`: `Items`, `Rejected` (documents that failed validation, with reasons), `PageSize` as reported by TA, `TotalHits` + `TotalIsCapped`. TA's `last_page` is **not** exposed (it's wrong); `IsPastEnd` = nothing at all on the page.
- Typed failures: `TubeArchivistAuthException` (401/403), `…BadRequestException` (400 + field errors), `…ServerException` (5xx, including the past-10k-window 500), `…UnavailableException` (network/timeout), `…ProtocolException` (not the expected JSON). Messages never contain the token.

**Client** (`TonTuyau.TubeArchivist`)
- Handler order (outermost first): **`ReadOnlyGuardHandler`** → `TokenAuthHandler` → standard resilience (3 retries, 30 s per attempt, 2 min total) → network. The guard throws `ReadOnlyViolationException` for anything but GET/HEAD, so retries and bugs stay read-only too. `Authorization` is redacted from HttpClient logs.
- Pages are **1-based**; page < 1 is refused before any request (TA treats 0 as 1 and fails on −1).
- Ids are checked against YouTube formats before they go into a URL; cache image paths must match `/cache/videos/<c>/<id>.<img>` or `/cache/channels/<channel>_<thumb|banner|tvart>.<img>`.
- **Validators** (`VideoDtoValidator`, `ChannelDtoValidator`) throw `ValidationException` with every broken field. Notably `media_url` must be exactly `/youtube/<this video's channel>/<this video's id>.<mp4|webm|mkv|m4v|mov>`, which rules out traversal and pointing at another file.
- A list page parses each document separately: a wrong JSON type or a failed rule moves *that* document to `Rejected`, the rest of the page is kept.
- Registered in the backend with `AddTubeArchivistGateway()`.

**Tools**
- `tools/fetch-dev-media/fetch.sh`: copied 11 real videos (the 10 picked + the 4K AV1, 1.5 GB), their thumbnails, channel art and metadata into `dev-data/` (git-ignored), with TA's folder layout. GET only.
- `backend/tools/TonTuyau.FixtureRecorder`:
  - **record**: 23 TA answers (incl. 400, 403, 404, 500, past-end, page 0 alias) through the read-only guard, **anonymised** (ids, names, titles, descriptions, tags; same real id → same fake everywhere; thumbnail folder letter follows the fake id). Refuses to write if a real id or the token is still present.
  - **scan**: walks the whole library through the client, channel by channel, and reports what the validators reject. Console only.

## How to check it

```bash
git checkout milestone/m1-ta-client
./scripts/check.sh                                   # expect "All checks passed."

# Optional, against your TA (read-only):
cd backend && set -a && . ../.env && set +a
dotnet run --project tools/TonTuyau.FixtureRecorder -- scan
```

## Tests added

| Kind | Where | Count |
|---|---|---|
| Unit | `TonTuyau.TubeArchivist.UnitTests`: guard × 10 write methods × sync/async; gateway on fixtures (pages, quirks, filters, errors, mapping); URL building sort × order × type × channel; malformed ids; 12 refused cache paths (traversal, encoded, nested, other folders, absolute URLs); failure mapping × 7; broken docs inside a page; video validator × 27 broken fields; channel validator × 10; mapper | 244 |
| Unit | `TonTuyau.FixtureRecorder.UnitTests`: anonymiser (no real data survives, formats kept, consistency, determinism, input untouched) | 10 |
| Reqnroll | `ReadOnlyTubeArchivist.feature` (8 scenarios): POST/PUT/PATCH/DELETE never reach a fake TA (WireMock); reads carry the token; 503 → 4 GETs then a typed error; retries stay GET; refused token reported without revealing it | 8 |
| Existing | M0 (config, health) | 71 |

Total: **333 backend + 2 frontend, all passing.** Two mutation checks were run by hand: allowing DELETE in the guard, and loosening the thumbnail regex. The tests caught both. That led to 5 more traversal cases.

## Findings on your TA (read-only)

- **Pagination differs from TA's docs**: pages are 1-based; `last_page` is wrong; past the end = **200 + empty list** (not 404); past the 10k window = **500**; `page=-1` = 500. Recorded in [SYNC_DESIGN.md](../SYNC_DESIGN.md).
- **Full scan** (2026-10-01): 44,971 videos, 115 channels, **0 duplicates**, biggest channel 5,397. **44,966 valid, 5 rejected**: five ARTE videos whose `media_url` points to the *ARTE Info* channel folder, where the file **doesn't exist** (404). The real file is in the ARTE folder. TA's own data is inconsistent there → question R4-2.

## Decisions taken during the milestone

- **Strict validation, lenient optional fields**: missing description/tags/stats/refresh dates are fine; ids, type, dates, duration, media/thumbnail paths are required and strict.
- Unknown `channel_tabs` values and non video/audio streams are **ignored**, not errors (TA may add some; they don't break anything).
- **500 is retried** like other 5xx (TA hiccups), although the past-window 500 is deterministic. Code should never ask for it, so the cost is 3 quick retries at worst.
- Images are **buffered** in memory (they're small) so no TA connection stays open while the caller writes the file.
- `MediaStream` was renamed **`MediaTrack`** (analyzer rule: no type names ending in "Stream"), and the TA field `streams` maps to `Tracks`.
- The schedule defaults moved to R3-1: incremental `0 7-23 * * *` (hourly, daytime), full `0 1 * * 6`.

## Deviations from the plan

- One feature branch (`feature/m1-ta-client-core`, 3 commits) instead of four. Guard, DTOs, pager and recorder are interdependent (the tests need the recorded fixtures, and the recorder uses the guard and URL builder), so splitting them would only have added merges.
- There's no separate "pager" class. The gateway fetches one page; deciding *which* pages to walk belongs to the sync design.
- The `Media.feature`/`Browse.feature` scenarios belong to M4, so only the TA-side Reqnroll feature exists so far.

## Known limitations / follow-ups

- The 5 inconsistent ARTE videos are rejected for now (R4-2).
- Docker is still missing on the laptop, so no Testcontainers yet. M2 needs it.
