# TonTuyau — Q&A session 04

> 2026-10-02. Questions that came up while building M3 (the sync), parked here instead of decided alone.
> Answered 2026-10-02. What changes: only R5-4 (configurable guard thresholds), applied with the M3 review fix-ups ([M3-review.md](milestones/M3-review.md)); R5-2 waits until TubeArchivist is reachable again.

---

**R5-1. Thumbnails replaced in TubeArchivist at the same path.**
M3 copies a thumbnail or channel art only when TonTuyau doesn't have it yet. If TubeArchivist refreshes a video and replaces its thumbnail *under the same file name*, TonTuyau keeps the old image.
Why not re-copy on every change: the content hash includes view and like counts, which change every week, so the weekly run would re-download most of the ~45k thumbnails (~2.5 GB) every Saturday.
  a) accept it (today's behaviour)
  b) the weekly full run asks TubeArchivist for each image with `If-Modified-Since` (a GET that answers 304 "unchanged" without the image): ~45k small requests, a few minutes, only new images downloaded
  c) re-copy the thumbnail only when TubeArchivist's `vid_last_refresh` moved since the last copy
Default: a)
**Answer:** Default.

**R5-2. A first run on your real library.** The first full sync copies **~2.5 GB of thumbnails + ~80 MB of channel art** from your TubeArchivist (estimated from the dev samples, average 58 KB each), plus ~620 list requests. That's the M8 milestone ("real-data run"), but doing a trial now on the laptop would catch real-data surprises before M4 builds on the sync. Read-only, as always. OK to do it now, or wait for M8 on the VM?
Default: wait for M8 (nothing downloaded without your OK).
**Answer:** Right now I'm not home, so you can't reach TubeArchivist: delay it.

**R5-3. One failing channel fails the whole full run.** The spec says a channel is deletable when "its walk had no error". M3 is stricter: if TubeArchivist fails (after retries) on any channel's list, the whole full run fails and nothing is deleted anywhere; the next run starts over. Simpler and safer, at the cost of one flaky channel blocking all deletions until it recovers.
  a) keep it (today)
  b) per channel: mark that channel "not deletable", continue with the others, report it
Default: a)
**Answer:** Keep it for now.

**R5-4. Format-change guard thresholds.** The run stops when more than **50 %** of the documents read so far are rejected, but only once at least **20** documents have been read (so 2 broken out of 3 doesn't stop a run). OK?
Default: yes.
**Answer:** Yes, and make the thresholds configurable, in case of a huge TA library.

**R5-5. Art that never comes back.** An image TubeArchivist answers 404 for goes to "pending" and is retried at every run, forever. Harmless (a few requests), but a pending list that only grows is noise in `sync status`.
  a) keep retrying (today)
  b) give up after N attempts (e.g. 10) and list them as "missing in TubeArchivist"
Default: a)
**Answer:** Keep trying; we'll check in production how fast this list grows.

**R5-6. Media-file check only in full runs.** Missing files on the share are detected by the weekly full run (a local check of ~45k files), not by the hourly incremental. A video downloaded today whose file is missing would be flagged on Saturday. OK?
Default: yes.
**Answer:** Yes, it's fine: we can start a full run whenever we want.

**R5-7. M3 review.** M3 is ready on `milestone/m3-sync` ([notes](milestones/M3-sync.md)). As usual: you review, I merge, then M4 (REST API) — or M5a (the live mockup) first, if you'd rather the pages shape the API (raised in the plan).
**Answer:** Starting the review.
