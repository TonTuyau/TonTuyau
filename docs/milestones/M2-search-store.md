# M2: Search store (Elasticsearch)

Branch: `milestone/m2-search-store` · Features: `feature/m2-media-path-correction`, `feature/m2-search-store`, `feature/m2-docs`, review fix-ups `feature/m2-fixups-formatting`, `feature/m2-fixups-rebuild`

## What was built

**R4-2, moved here from your M1 review** (`TonTuyau.TubeArchivist`)
- `media_url` pointing to another channel's folder is no longer rejected: the path is always `<channel_id>/<video_id>.<ext>` and the video carries `MediaPathCorrected = true`. Another *video's* id in `media_url` is still rejected.
- Library scan after the change: **44,972 valid, 5 corrected, 0 rejected**.

**Store contract** (`TonTuyau.Application/Store`)
- `IVideoRepository`, `IChannelRepository`: `UpsertAsync(items, runId?)` → `UpsertResult(Created, Updated, Unchanged)`, `GetAsync`, `CountAsync`, `DeleteAsync`.
- `IIndexManager`: `EnsureAsync`, `GetStatusAsync` (generation, definition up to date?, document count), `BeginRebuildAsync` / `CompleteRebuildAsync` / `AbortRebuildAsync`, `RefreshAsync`.
- `SearchStoreException` for anything Elasticsearch refuses or can't do.
- `Video` gains `ChannelName` (required by the validator), so search can find videos by channel name.

**Elasticsearch** (`TonTuyau.Infrastructure/Elasticsearch`)
- **Indices**: `tontuyau-videos-v<N>` and `tontuyau-channels-v<N>`, each behind a **read alias** (`tontuyau-videos`) and a **write alias** (`tontuyau-videos-write`). Prefix configurable (`TONTUYAU_ES_INDEX_PREFIX`).
- **Definitions** in `Definitions/*.json` (embedded): 1 shard, 0 replicas, `"dynamic": "strict"`. Text fields use:
  - **French** (elision, stop words, light stemmer, accent folding), with an **English** sub-field;
  - **edge-ngram autocomplete** on titles and names;
  - a **sort** keyword (lowercase + folded) for channel names.
  Each definition's hash is stored in `_meta.definition_hash`. An index created by an older definition is reported as outdated (log warning + status).
- **Upsert**: one `_mget` of the stored content hashes, then one `_bulk`. Documents whose content hash didn't change are **not rewritten**. With a run id, every document passed gets `seen_in_run` (unchanged ones get a partial update). Without a run id, the previous mark is kept. Batches of 500.
- **Rebuild**: create generation N+1, move the write alias → syncs write to N+1 while users read N → **one atomic `_aliases` call** switches every read alias, then N is deleted. Abort = write alias back, N+1 deleted. A second rebuild while one is running is refused.
- All requests go through `EsTransport` (the client's low-level transport with explicit JSON), so every request shape is visible in the code.
- `IndexInitializer` (hosted service) creates missing indices at startup and retries every 10 s while Elasticsearch isn't there. It never stops the backend.
- **Health**: `/health` = liveness (no checks); **`/health/ready`** = Elasticsearch green/yellow **and** indices created.

**Compose**: Elasticsearch image **9.5.4**. The 9.5.5 I had written has no published image. `docker compose config` now validates.

## How to check it

```bash
git checkout milestone/m2-search-store
./scripts/check.sh            # needs Docker (your user in the `docker` group) and ≥ 2 GB free RAM
```

## Tests added

| Kind | Where | Count |
|---|---|---|
| Unit | `TonTuyau.Infrastructure.UnitTests`: **mapping fields == document fields** (strict mappings: a forgotten field would break every write), every analyzer/normalizer used is defined, definition hashes, index names/generations × prefixes, video/channel round-trips through JSON (Combinatorial), content hash changes for each of 13 field changes, bookkeeping not in the hash | 81 |
| Unit | Index prefix validation (8 bad, 3 good), R4-2 mapping + validator, `channel_name` required | +28 |
| Reqnroll | `SearchStore.feature` (17 scenarios) on a **real Elasticsearch 9.5.4** (Testcontainers): creation, restart keeps data, idempotent upserts, run marks, deletes, rebuild / abort / one at a time, outdated definition detected, French analysis × 6 (accents, elisions, plurals, case, `œ`, `ñ`), English stemming, readiness | 17 |

Total: **447 backend + 2 frontend, all passing.** Mutation checks run by hand:
- **Disabling the unchanged-skip:** caught.
- **Removing accent folding from the French analyzer:** not caught at first. The light French stemmer already folds é/â/û/ç, so I added `œuvre`/`oeuvre` and `piñata`/`pinata`, which only folding handles. Now caught.

## Decisions taken during the milestone

- **Two aliases per index** (read + write) rather than writing to a named generation. Repositories always write to the write alias, so rebuilds need no special code path in the sync.
- **Content hash** (SHA-256 of the document without bookkeeping fields) decides "unchanged". The weekly full run then rewrites only what changed, instead of 45k documents.
- **`seen_in_run` is kept by plain incremental writes**, so an incremental between two full runs can't make a video look "not seen" to the deletion step.
- **Low-level JSON instead of the typed client** for these operations: explicit, easy to review, and no surprises from the client's serializer. The typed query DSL stays available for M4's search.
- **Test Elasticsearch is small** (512 MB heap, 1.5 GB container limit, ML disabled) and **refuses to start below 2 GB free RAM**, because of your laptop's earlier out-of-memory crash. Godot was using 4.7 GB during this milestone.
- `ServiceCollectionExtensions` in Infrastructure is named `SearchStoreServiceCollectionExtensions`, to avoid a name clash with the TubeArchivist one.

## Deviations from the plan

- **`tontuyau-sync` index moves to M3**: its shape (lock, watermark, run history) is defined by the sync spec, so it's created with the code that uses it.
- **`tontuyau index rebuild` CLI** is wired in M3. A rebuild is "new generation + full import + swap", and the full import is M3. The alias machinery is done and tested here.
- **Search queries** (lists, filters, as-you-type) are M4, with `Search.feature`. M2 checks the analyzers directly through `_analyze`.

## Known limitations / follow-ups

- My shell needed `sg docker` because the app was started before you joined the group. In a new session of yours, `./scripts/check.sh` works directly.
- The Elasticsearch integration tests take ~50 s (mostly container start).

## Review fix-ups (2026-10-02)

See [M2-review.md](M2-review.md).
- **Formatting ("column reading")**: Roslynator formatting analyzers (RCS0053/0054/0056, 120 columns) as build errors; every LINQ chain of 2+ calls one call per line; a call wraps one argument per line when an argument is itself a call/constructor with 2+ arguments. No behaviour change.
- **`MoveAliasAsync`**: one helper for the three alias moves (begin, complete, abort).
- **Leftover generation**: a rebuild deletes generation N+1 first when nothing points to it, and refuses when an alias does. +1 scenario (now 18 in `SearchStore.feature`).
- Tests: **448 backend + 2 frontend, all passing.**
