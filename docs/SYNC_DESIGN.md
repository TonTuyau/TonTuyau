# TonTuyau — Sync design

> The dedicated discussion for the TubeArchivist → TonTuyau sync (Q7, R3-4).
> Format chosen 2026-10-01: **I propose, you take it apart.** Nothing here is settled until you've agreed to it; I change it for your reasons, I don't defend it for the sake of it. I build it in M3 once it's agreed.
> Status: **AGREED: v5 is the spec for M3** (2026-10-01), **implemented in M3** (2026-10-02, see *Implementation notes*). Any change goes through a new review round below.

---

## TubeArchivist facts the design has to live with

Measured on your instance (v0.5.12, page size 100), read-only, 2026-10-01:

| Behaviour | Detail |
|---|---|
| Page numbering | Pages are **1-based**: page *N* starts at item `(N−1) × page_size`. `page=0` is an alias of `page=1` |
| `last_page` | **Unreliable**: page 1 of 115 channels says `last_page: 2`, page 2 says `last_page: 0` |
| Past the last page | **HTTP 200 with `data: []`** (not 404 as the docs say) |
| Past the 10k window | **HTTP 500** with an HTML error page (e.g. `page=101` at page size 100). `page=-1` → 500 too |
| `total_hits` | Capped at 10,000 (`max_hits: true`); true totals only from `/api/stats/video/` |
| Sort keys | `published`, `downloaded`, `views`, `likes`, `duration` (+ `order=asc|desc`); `filesize` rejected (400) |
| Filters | `channel=`, `type=videos|streams|shorts`, `watch=`, `height=`; an unknown channel id → 200, empty list |
| Unknown item | `/api/video/<id>/`, `/api/channel/<id>/` → **404** `{"error": "... not found"}` |
| Per-channel total | `/api/channel/<id>/aggs/` → `total_items.value`: the channel's true video count, **not capped** at 10k (checked: 5,397 for the biggest channel, matching its list) |
| Bad parameter | **400** `{"<param>": ["... is not a valid choice."]}` |
| No / wrong token | **403** `{"detail": "Authentication credentials were not provided."}` |
| Dates | `date_downloaded` = epoch seconds (int); `published`, `vid_last_refresh` = ISO-8601 |
| Sizes today | 44,971 videos, 115 channels, biggest channel 5,397 videos (scan of 2026-10-01) |
| Data quality | Full scan: **0 duplicate ids**; **5 videos** (ARTE) whose `media_url` points to another channel's folder where the file doesn't exist; all others pass the M1 validators |
| Cost reference | Reading every channel's video list once, page size 100: 619 requests in ~60 s |

The M1 TA client (`ITubeArchivistGateway`) exposes these as plain building blocks: one page (with `Items`, `Rejected`, `PageSize`, `IsPastEnd`), one item, one image, typed errors for 400/403/404/5xx/network. It does **not** contain any sync strategy.

---



## Modes (Q5, Q6, R3-1)

- **Incremental**: during the day (R3-1; default hourly 07:00–23:00 Paris, `0 7-23 * * *`); must only fetch what's new to TonTuyau.
- **Full**: weekly, the night Friday → Saturday at 01:00 Paris (`0 1 * * 6`, R3-1); refreshes all metadata and removes what TA no longer has.
- Both can be started by the scheduler **or** by the CLI inside the container.

## Requirements

- R1. Incremental runs must not re-scan the 44.9k videos.
- R2. The very first run on an empty index must import everything, despite the 10k list cap.
- R3. A run that dies at any point must leave no permanent silent gap; a later run makes up for it.
- R4. Two runs must never corrupt each other (scheduler vs CLI, incremental vs full).
- R5. TA slow/down never takes TonTuyau's UI down; the UI keeps serving the current index.
- R6. Progress, counts and errors are visible in `sync status` and the logs.
- R7. Nothing is ever sent to TA except GET/HEAD.
- R8. A video is never removed from TonTuyau because of a TA hiccup.

## Cases the design must handle

**Incremental**
1. Empty index: first run, 44,884 videos, page size 12, 10k list cap.
2. Nothing new since the last run.
3. 3 new videos in 2 channels.
4. TonTuyau was down for a long time: **15,000** new videos since the last run (more than the 10k cap).
5. A brand-new channel with 500 videos appears.
6. A video appears in TA whose `date_downloaded` is **older** than videos TonTuyau already has (manual import / rescan in TA).
7. Several videos share the same `date_downloaded` second, and they straddle a page boundary.
8. TA downloads new videos **while** the sync is paging (the list shifts under the pager).
9. The `page=0` / `page=1` duplicate quirk.
10. TA reports `last_page` wrongly, and a page past the end comes back as **200 with an empty list**. A page past the 10k cap comes back as **HTTP 500** (see facts below).
11. The process is killed after page *k* of *n* (container restart, OOM).
12. TA returns 500 / times out on page *k*, then recovers within the same run, or doesn't.
13. TA is unreachable for the whole run.
14. A new video belongs to a channel TonTuyau doesn't know yet.
15. Metadata OK, but its thumbnail copy fails (missing in TA's cache, or a timeout).
16. One video document fails validation (broken data).

**Full (weekly)**
17. A video was deleted in TA: it must disappear from TonTuyau.
18. A whole channel was deleted in TA.
19. Metadata changed in TA (title, views, likes, thumbnail replaced, `active` flipped).
20. The run takes ~450 requests (page size 100), maybe more if TA's page size is lowered again, and it is still running when the first daytime incremental is due (07:00), or when someone starts one from the CLI.
21. The run dies at 60%. Videos not reached yet must **not** be treated as deleted.
22. TA answers 200 but with **empty or truncated** lists (TA's own ES trouble). TonTuyau must not be wiped.
23. A video changes type (video → stream) or changes channel.
24. A channel grows past 10,000 videos.

**Operational**
25. `tontuyau sync full` started from the CLI while the scheduler's run is in progress (two processes).
26. `tontuyau index rebuild` while users are browsing.
27. TA is upgraded and a field is renamed or disappears.
28. Clocks of the TA VM and the TonTuyau VM disagree.
29. TA lists a video whose file is missing on the NFS (or the NFS mount is down entirely).

## What a design has to answer

- What state is persisted between runs, where, and when it is written.
- How "new" is defined, and why that definition survives cases 6–8.
- What makes re-running a half-finished run safe.
- What evidence is needed before deleting something (cases 21–22).
- How mutual exclusion works across processes (case 25).

The cases above will become Reqnroll scenarios (`Sync.feature`), with unit tests for the parts underneath.

---

## Proposal (v5)

### The idea in four sentences

TonTuyau's index is a **disposable mirror** of TubeArchivist: every write is an *upsert by id*, so re-doing work is always safe, and the worst outcome of any bug or crash is "do it again". The **incremental** run reads TubeArchivist's newest downloads first and stops once it's back in territory it already knows (plus a safety margin). The **full** run walks every channel's list (each one is far under the 10k cap), refreshes everything it sees, and only *then*, if the walk was complete and plausible, deletes what it didn't see. Only one run at a time, enforced by a lock that both the scheduler and the CLI must take.

### State (stored in TonTuyau's ES, index `tontuyau-sync`)

| Item | Content | Written when |
|---|---|---|
| `watermark` | The highest `date_downloaded` (TA's own clock) that a **completed** run has indexed | Only at the successful end of a run |
| `lock` | Owner (scheduler/CLI, host, pid), mode, start time, lease expiry | Taken at start (create-if-absent), renewed every minute, deleted at the end |
| `runs` | One document per run: mode, trigger, start/end, counts (read, new, changed, unchanged, rejected, corrected, deleted, art copied/failed), outcome, error | Updated as the run goes, final at the end |
| `seen_in_run` (field on every video/channel doc) | Id of the last full run that saw it | During full runs |
| `pending_art` | Thumbnails/channel art that failed to copy | When a copy fails; cleared when it succeeds |

### Incremental run (hourly, 07:00–23:00)

```
take the lock (or exit: "a sync is already running")
gap = TA total (/api/stats/video/) − videos in TonTuyau                          ← review 1
if there's no watermark, or gap > 10,000, or a full run was requested → do a full run instead
floor = watermark − 24 h                              (safety margin, configurable)
for page = 1, 2, … of /api/video/?sort=downloaded&order=desc:
    upsert every valid item (unchanged docs are skipped by content hash)
    fetch + upsert any channel TonTuyau doesn't know yet, copy its art
    copy thumbnails of new videos (failures → pending_art, never block the run)
    stop when the page is past the end, or the oldest item on it is older than floor
    safety net: page 100 done without stopping → stop here (page 101 would be a TA 500),  ← review 3
        don't move the watermark, request a full run for the next tick
retry pending_art
watermark = max(watermark, highest date_downloaded seen)
release the lock
```

Typical cost: **2 requests** (stats + one page of 100, which covers the last 24 h unless you download more than 100 videos a day).

### Full run (weekly, Saturday 01:00)

```
take the lock;  run = new id;  channel_stats = /api/stats/channel/
walk /api/channel/ pages → upsert each channel, seen_in_run = run
channels_complete = channel walk had no error and channels walked == channel_stats.doc_count

for each channel:                                                       ← review 4: decided per channel
    walk /api/video/?channel=<id>&sort=published&order=asc pages until past the end
    upsert each item, seen_in_run = run (rejected items are marked seen too, so they're not deleted)
    expected = the list's total_hits, or /api/channel/<id>/aggs/ when the list is capped (> 10k)
    if the list is capped: walk it once per type (videos / shorts / streams)
        if one type alone is > 10k: import the 10,000 reachable ones, log a warning (channel, type, count)
    channel is deletable = its walk had no error and walked (valid + rejected) == expected

to_delete = videos of deletable channels whose seen_in_run ≠ run
          + channels whose seen_in_run ≠ run, with their videos (only if channels_complete)
write to_delete to /data/logs/sync/<run>-deletions.jsonl, deleted or not
    (one line each: id, type, title, channel, date_downloaded, last seen run)
non-deletable channels: listed in the run report with the reason (walk error, count mismatch, type > 10k)
if |to_delete| > TONTUYAU_SYNC_MAX_DELETIONS:
    no deletion, report "N to delete > limit M, check <file>, then: tontuyau sync apply-deletions <run>"
else:
    confirm-and-delete(to_delete)
watermark = max(watermark, highest date_downloaded seen)
release the lock
```

```
confirm-and-delete(list):                                              ← review 4
    for each item: GET /api/video/<id>/ (or /api/channel/<id>/)
        404            → delete it from TonTuyau (+ its copied art), count "deleted"
        200            → keep it, report "still in TubeArchivist" (the walk missed it: worth a look)
        error after retries → stop: delete nothing more, report; the rest waits for the next run
```

`TONTUYAU_SYNC_MAX_DELETIONS` is an absolute number (default **100**, confirmed).

### Applying a blocked deletion (CLI only, review 2)

```
tontuyau sync apply-deletions <run>
take the lock
refuse if <run> isn't the latest full run
confirm-and-delete(the ids in /data/logs/sync/<run>-deletions.jsonl that are still in the index)
record in the run's history: applied by CLI, when, how many
release the lock
```

No second walk of TubeArchivist: it works from exactly the list you checked, and each item is confirmed gone from TA (404) right before it's deleted. The scheduler never runs this, so a scheduled run never deletes more than the limit.

Cost today: ~620 requests, ~60 s (+ one `/aggs/` per capped channel, none today, + one `GET` per deletion). It's cheap enough that a crashed full run is simply **restarted from scratch** next time, with no resume logic.

### Rules that apply to both

- **Rejected documents** are never indexed. A video that was valid and becomes invalid **keeps its last good copy** in TonTuyau and is listed in `sync status`.
- **R4-2 correction**: a `media_url` pointing to another channel's folder is rebuilt from the video's own `channel_id`, and counted as *corrected*.
- **Format change guard**: if more than 50 % of a run's documents are rejected, the run stops without writing anything more ("TubeArchivist's format may have changed").
- **TA unreachable / 5xx after retries**: the run ends as *failed*, no state is written, and the UI keeps serving the current index (it only reads ES).
- **Missing files**: the full run also checks each video's file on the NFS (a local `stat`, no TA request) and flags missing ones. If the media root itself is missing or empty, the check is skipped and reported, never "45k files missing".
- **Lock**: lease 10 min, renewed every minute. A lock whose lease has expired (crashed process) can be taken over. A scheduler tick that finds the lock taken is skipped. The CLI prints who holds it and exits with a non-zero code.
- **`index rebuild`**: builds into new versioned indices and swaps the aliases at the end, so browsing continues on the old ones meanwhile.

### Where each case goes

| Cases | Handled by |
|---|---|
| 1 empty index | No watermark (and gap = everything) → full run |
| 2, 3, 5, 14 nothing new / a few new / new channel / unknown channel | Incremental, stop rule; unknown channels fetched on the fly |
| 4 15k new videos | Gap check before paging → full run. A gap hidden by TA deletions (e.g. 12k new − 3k deleted → 9k): the page-100 net stops the incremental cleanly and the next tick runs a full run |
| 6 old `date_downloaded` appears | **Not** caught by incremental; caught by the next full run (up to 7 days late) |
| 7, 8 same second / list shifting while paging | 24 h margin + idempotent upserts (duplicates are harmless, skips are covered by the overlap) |
| 9, 10 page quirks | M1 client (1-based pages, empty page = end, 500 = error) |
| 11, 12, 13 crash / TA errors / TA down | State only written at the end; re-doing is safe |
| 15 art copy fails | `pending_art`, retried every run |
| 16, 27 broken docs / format change | Rejected + reported; 50 % guard |
| 17, 18, 19, 23 deletions / metadata changes / type or channel change | Full run: upsert refreshes; deletion per channel, each one confirmed by a 404 |
| 20, 25 runs overlapping | Lock |
| 21, 22 full run dies / TA returns truncated lists | Deletion only in channels whose walk is complete and whose count matches TA's total for that channel; each deletion confirmed by a 404; above the limit, only by `apply-deletions <run>`; the list is always written to a file |
| 24 channel > 10k | Split per type, true total from `/aggs/`; a type > 10k: its 10k reachable videos are imported, warning logged, no deletion **in that channel** |
| 26 rebuild while browsing | Alias swap |
| 28 clock skew | Watermark uses TA's dates only, never TonTuyau's clock |
| 29 missing files / NFS down | Flag per video; skip the check if the mount looks absent |

### Choices I made that you might make differently

1. **A date watermark** rather than remembering the set of known ids.
2. **A 24 h margin.**
3. **Old-date imports wait for the weekly run** (case 6).
4. **Full run restarts instead of resuming.**
5. **Sweep conditions**: exact count match + an absolute deletion limit (review 1).
6. **Keep the last good copy** of a video that becomes invalid.
7. **Escalating** a too-far-behind incremental into a full run automatically (now decided up front by the gap check, review 1).

---

## Implementation notes (M3, 2026-10-02)

Built as specified, with these precisions (the ones that are real choices are open questions in [QA_SESSION_04.md](QA_SESSION_04.md)):
- **Escalation**: an incremental run becomes a full run when there's no watermark, when the gap TA total − TonTuyau total exceeds the window, or when a previous run requested one (page-window net). The run is recorded as `full` with an "Escalated…" note.
- **Window**: `TONTUYAU_TA_RESULT_WINDOW` (10,000). Both the gap check and the page net use it; the net stops after page *n* when *n × page_size ≥ window*.
- **Per-channel walk**: sorted by `published` ascending; the per-type walk happens only when the channel's list says its total is capped; the expected count then comes from `/aggs/`. A type reaching the window marks the channel not deletable.
- **Any TA error during a full run fails the run** (no per-channel tolerance) → R5-3.
- **Format guard**: > 50 % rejected, after ≥ 20 documents → R5-4.
- **Art**: copied when missing locally; failures go to `pending_art`, retried at the end of every run → R5-1, R5-5.
- **Media check**: full runs only; skipped (with a note) when the media root is missing or empty → R5-6. The flag is bookkeeping (`media_missing`), not content.
- **Rejected documents** with a valid id are marked seen (kept, never deleted for being broken); a deletion candidate that TA still has but sends broken counts as "still in TubeArchivist".
- **Lock**: 10 min lease renewed every minute; a run whose lease is lost is cancelled and recorded as failed.
- **`apply-deletions`** reads the run's JSONL list, keeps only items still in TonTuyau, confirms each with TA (404) and records the result in that run's history.
- **`index rebuild`** = begin rebuild (write alias → new generation) + full run + atomic read-alias switch; any failure aborts and users stay on the old generation.

---

## Your review

### Round 1 (2026-10-01)

**Incremental**
- "Page 100 reached" isn't realistic with an hourly cron. If 10k videos really are missing, skip the incremental entirely: compare our total with TA's total at the beginning, and if the gap is > 10k, go straight to a full run.

**Full**
- If one type is > 10k, log a warning and still import the 10k reachable videos, so we have data.
- The deletion threshold should be a configurable absolute value, not a percentage, plus a file log of those videos so the user can check them.
- `--allow-mass-delete` must appear in the full run pseudo-code.

### Claude's answers (round 1)

1. **Gap check up front: adopted.** It's better than my version: it decides before spending 100 requests, and it costs one stats request. One case it can't see: deletions in TA hide new videos in a total. For example, 12k new and 3k deleted gives a gap of 9k, so the incremental runs and hits the 10k window. With an hourly cron that's very unlikely. If it happens without a safety net, each hourly run fails on TA's 500 at page 101 until Saturday. → **Q1**
2. **Import the reachable 10k + warning: adopted.** One consequence I kept: that run must not sweep. Videos it couldn't reach would look deleted. → **Q2** on how far to take this.
3. **Absolute limit + deletion file: adopted.** The file is written on every full run, whether it deletes or not. When the limit blocks the sweep, you have the exact list to check before forcing it. Default proposed: 100. → **Q3**
4. **`--allow-mass-delete` in the pseudo-code: done.** It only lifts the *size* limit. An incomplete walk or a count mismatch still blocks the sweep, because then the list itself can't be trusted.

### Round 2 (2026-10-01)

1. Even when deletions hide part of the gap, it's still 12k new videos to scan, so a full run is needed anyway: **drop the page-100 net**.
2. A type with 10k+ videos on a channel **must not delete**.
3. Only apply deletions to the **scanned deletable videos**: no second full run just to validate deletions.

### Claude's answers (round 2)

*(Point 1 was reversed in round 3, once the masked-gap case was clear: the net is back.)*

1. **Dropped.** To be explicit about what we accept: in that masked case, the gap check sees 9k and starts an incremental, which fails on TA's 500 at page 101 every hour until a full run happens (Saturday, or `tontuyau sync full` by hand). Those failures show up in `sync status`. If you'd rather the gap check *also* catch that case, tell me; otherwise it's settled.
2. **Adopted** as: no deletion at all in that run. That's the simplest reading. If you meant "no deletion *for that channel*, but other channels still get cleaned", say so. It needs a per-channel count check instead of the global one.
3. **Adopted**: `tontuyau sync apply-deletions <run>` (pseudo-code above). Two rules I added, which you may not want: it refuses a list older than the latest full run (a newer run has a newer list), and it refuses a run that wasn't sweep-safe. One optional extra if you want certainty without a full run: before deleting, check each listed id with one `GET /api/video/<id>/`. 404 confirms the deletion; 500 deletions = 500 requests, about 50 s. Not included unless you want it.

### Round 3 (2026-10-01)

1. Now that the masked-gap case is clear (deletions hiding new videos in the total): **keep the page-100 net**.

### Round 4 (2026-10-01)

1. A type > 10k blocks deletion **only for that channel**.
2. A check is worth it before any irreversible action.

### Claude's answers (round 4)

1. **Applied.** The run-wide "walked == TA total" check couldn't work any more: one capped channel would fail it for everyone. It's replaced by a **per-channel** check: walked == the channel's own total (the list's `total_hits`, or `/api/channel/<id>/aggs/`, which isn't capped). Each channel decides for itself, and deleting whole channels additionally requires the channel list itself to be complete.
2. **Applied to every deletion**, not just `apply-deletions`, because the scheduled sweep under the limit is just as irreversible. Each item gets `GET /api/video/<id>/` right before deletion: 404 → delete, 200 → keep and report, error → stop deleting. In fairness, TonTuyau deletions are *recoverable* (a wrongly deleted video comes back with the next full run, since TA still has it), but the check avoids a video vanishing for up to a week, for ≤ 100 cheap requests.
3. **Default limit 100**: confirmed by you (2026-10-01). One env var to change.

**All review points are now applied.** → You approved v5 as the M3 spec (2026-10-01). Each case becomes a `Sync.feature` scenario.

---

## Review rounds

| Round | Changes |
|---|---|
| 1 | Gap check (TA total − ours > 10k → full run) before paging. A type > 10k imports its reachable 10k + warning, no sweep. Deletion limit is absolute (`TONTUYAU_SYNC_MAX_DELETIONS`), deletion list always written to `/data/logs/sync/<run>-deletions.jsonl`. `--allow-mass-delete` in the pseudo-code; lifts only the size limit |
| 2 | Page-100 net dropped (masked-gap failures accepted until the next full run). A type > 10k blocks deletion for the whole run. `--allow-mass-delete` replaced by `tontuyau sync apply-deletions <run>`: applies exactly the reviewed list, latest sweep-safe full run only |
| 3 | Page-100 net restored: it covers the gap the total can't see (TA deletions masking new videos). Stopping there requests a full run for the next tick |
| 4 | Deletion decided **per channel** (walked == the channel's own TA total, `/aggs/` when capped); the run-wide count check is gone. Every deletion is confirmed by a 404 from TA right before it happens (sweep and `apply-deletions`) |
| — | **v5 agreed as the M3 spec** (2026-10-01) |
