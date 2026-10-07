# M3: Sync

Branch: `milestone/m3-sync` · Features: `feature/m3-gateway-additions`, `feature/m3-sync-state`, `feature/m3-local-storage`, `feature/m3-sync-engine`, `feature/m3-cli-scheduler`, `feature/m3-docs`

Spec: [SYNC_DESIGN.md](../SYNC_DESIGN.md) v5 (agreed), with *Implementation notes* for the precisions made while building. Open choices: [QA_SESSION_04.md](../QA_SESSION_04.md).

## What was built

**TubeArchivist client additions**
- `GetChannelCountAsync` (`/api/stats/channel/`), `GetChannelVideoTotalAsync` (`/api/channel/<id>/aggs/`, uncapped). Finding: TA answers **0, not 404**, for an unknown channel's aggs.
- `RejectedItem.Id`: a broken document with a valid id can still be marked "seen".
- Fixtures re-recorded (25, anonymised, leak-scanned).

**Sync state** (`<prefix>-sync` index, never rebuilt)
- **Lock**: one document, `_create` to take it, `if_seq_no` + `if_primary_term` to take over an expired lease, renew and release (no `_version`: Elasticsearch 9 rejects internal versioning for this, as shown during the M3 discussion). Lease 10 min, renewed every minute; the lease's `Lost` token cancels the run if another process got the lock.
- **Checkpoint** (watermark, full run requested), **run history** (newest first, latest finished full run), **pending art**.

**Local storage**
- **Art** under `TONTUYAU_ART_ROOT` with TA's layout (`/cache/videos/a/x.jpg` → `<root>/videos/a/x.jpg`), atomic writes (temp + rename).
- **Media share check** (read-only): root Missing / Empty / Available; per-file existence.
- **Deletion lists**: `<TONTUYAU_SYNC_LOG_ROOT>/<run>-deletions.jsonl`.
- Every path goes through `LibraryPaths` (art paths, media paths, run ids) and is resolved under its root.

**Engine** (`SyncService`): incremental, full, confirm-and-delete, `apply-deletions`, index rebuild, status, as in the spec. Repositories gained `ChangedIds`, `MarkSeen`, `FindNotSeen` / `FindByChannels` (paged with `search_after`, no 10k cap on our side) and the `media_missing` bookkeeping flag (kept across content rewrites, outside the content hash).

**CLI and scheduler**
- `tontuyau sync incremental|full|status|apply-deletions <run>`, `tontuyau index rebuild`, `tontuyau help`. Exit codes **0** done, **1** run failed, **2** usage/config error, **3** another sync running, **4** refused. Runs in its own host (no web server, no scheduler) and shares the lock with the backend.
- `SyncSchedule`: both crons in `TONTUYAU_TZ`; a full run wins a tie. `SyncScheduler` (hosted in the backend) skips a tick when the lock is held; off with `TONTUYAU_SYNC_SCHEDULER_ENABLED=false`.
- New settings: `TONTUYAU_SYNC_LOG_ROOT`, `TONTUYAU_SYNC_MAX_DELETIONS` (100), `TONTUYAU_SYNC_INCREMENTAL_MARGIN_HOURS` (24), `TONTUYAU_SYNC_SCHEDULER_ENABLED`, `TONTUYAU_TA_RESULT_WINDOW` (10000). Unparsable values refuse startup.

## How to check it

```bash
git checkout milestone/m3-sync
./scripts/check.sh                                     # Docker + ≥ 2 GB free RAM; ~3 min

# The CLI, without Elasticsearch: help and usage answer immediately
cd backend && dotnet run --project src/TonTuyau.Api -- help
```

Reading the scenarios is the fastest way to review the behaviour: `backend/tests/TonTuyau.IntegrationTests/Features/Sync.feature` (31), `SyncState.feature` (9), `Cli.feature` (7).

## Tests added

| Kind | What | Count |
|---|---|---|
| Reqnroll | `Sync.feature` on an **in-process fake TubeArchivist** with TA's measured quirks + real Elasticsearch: first run, incremental cost, gap / window net, new channel, old import, same second, list shifting while paging, crash / hiccup / outage, art retry, broken docs, format guard, R4-2, deletion (video, channel, metadata, partial run, short lists, still-in-TA, capped types, limit + apply incl. outdated list and come-back), media flags, unmounted share, lock contention, rebuild ok / aborted | 31 |
| Reqnroll | `SyncState.feature`: lock contention / release / crashed holder (expired or running lease) / renewal / lost lock, checkpoint, run history, pending art | 9 |
| Reqnroll | `Cli.feature`: full sync, status, busy (3), failed (1), refused (4), malformed run id, rebuild | 7 |
| Unit | schedule vs a brute-force minute-by-minute reference over a whole week (336 cases, DST nights), named cases; scheduler under a fake clock (due runs, busy skip, disabled); CLI parsing | ~360 |
| Unit | path rules (art, media, run ids, traversal, trailing newline), storage on temp dirs, new settings | ~60 |

Total: **929 backend + 2 frontend, all passing.**

**Mutation checks by hand** (each caught by at least one scenario): deleting without asking TA; no page-window net; every channel deletable; no format guard; watermark saved on a failed run; renewing the lock without the version pair.

## Real-data trial (2026-10-02)

Your TubeArchivist v0.5.12, read-only, from the laptop, into a local Elasticsearch 9.5.4 (1 GB heap). No media share mounted, so the file check was skipped, which also exercised the "share not mounted" rule.

| Run | Duration | Result |
|---|---|---|
| First full sync (empty index) | **13 min 3 s** | 44,979 videos / 116 channels = **exactly TA's own totals**; 0 rejected; 5 media paths corrected (the ARTE ones); **45,327 images copied, 0 failed: 4.1 GB** (the R5-2 estimate of 2.5 GB was low: the real average is ~94 KB, not the 58 KB of the 11 samples) |
| Incremental right after | **1.2 s** | 200 read (2 pages: more than 100 downloads in the 24 h before the watermark), 0 new, 0 changed |
| Second full sync (a "Saturday") | **1 min 17 s** | 44,979 unchanged (nothing rewritten), 0 art downloaded, 0 deletions, deletion list empty |

Takeaways: the weekly run costs ~1–2 minutes when nothing changed (expect more "changed" on a real Saturday, as view counts move); a fresh install's first sync needs ~13 minutes and ~4 GB for art on the VM's volume.

## Bugs found and fixed during the milestone

- **Regex anchors** (also in M1 code): `$` in .NET also matches before a trailing newline, so `"abc.jpg\n"` was accepted as a path. Every strict id/path regex now ends with `\z`; regression cases added.
- **CLI help needed Elasticsearch**: `tontuyau help` tried to reach Elasticsearch first. Help and usage now answer without it.
- **Test hosts and inotify**: ~70 hosts in one test run hit Linux's 128-watcher limit; test hosts no longer watch config files.

## Decisions taken during the milestone

See *Implementation notes* in [SYNC_DESIGN.md](../SYNC_DESIGN.md). The ones that are real choices (art refresh, a trial on real data, one failing channel failing the run, guard thresholds, art that never comes back, media check frequency) are in [QA_SESSION_04.md](../QA_SESSION_04.md), with today's behaviour as default.

## Deviations from the plan

- The fake TubeArchivist is a small in-process ASP.NET Core app rather than WireMock, because it needs real state (a library that downloads, deletes and breaks things mid-run).
- Real-data trial done after the review fixes (see above).

## Known limitations / follow-ups

- Thumbnails replaced by TA under the same name aren't re-copied (R5-1).
- `sync status` is text only; the REST view of it comes with M4.
