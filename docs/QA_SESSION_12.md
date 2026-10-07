# TonTuyau — Q&A session 12

> 2026-10-05. Parked on a tired Monday: the questions still open after the M5 real-data review. Answer whenever you like, on each **Answer:** line. M5 itself is finished and waits for your review (`milestone/m5-front`).

---

**M5-16. The "Unavailable on YouTube" badge.** TubeArchivist flags **14,210 of 45,319 videos** as inactive, but you can still find many of them on YouTube. Its refresh most likely failed (YouTube's anti-bot check) and it marked them inactive anyway. The badge shows TubeArchivist's flag. Fixing it on TubeArchivist's side (cookies, then a refresh) should clear most flags, and TonTuyau's next sync follows.
  a) keep the badge as it is; it gets right once TubeArchivist is fixed
  b) a setting to hide the badge
  c) reword it to say who claims it: "Marked unavailable by TubeArchivist", with a hint that the check can fail
  d) TonTuyau checks YouTube itself (not advised: a new dependency on YouTube, same anti-bot problems)
Proposal: a), or c) if you don't want to wait for the TubeArchivist fix
**Answer:** a), keep the badge as it is: it will be useful later (once TubeArchivist is fixed, and for the future downloader's confirmed removals).

**M5-17. "Gone from YouTube" counts per type on the channel page.** Today the page shows one count per type ("3.5K videos • 1.2K shorts • 101 lives" for ARTE; 4,830 in total). Your idea: see quickly how much a channel lost, e.g. ARTE: 1,735 videos, 101 shorts and 70 lives flagged gone (likely real for ARTE: rights expire).
  a) keep one count per type
  b) add the gone count next to each type: "3.5K videos (1.7K gone from YouTube) • …" (small contract change + front)
Caveat for b): the numbers come from the same TubeArchivist flag as M5-16, so they're inflated until TubeArchivist is fixed.
Proposal: none; it's your call (b) is more useful once the flags are right)
**Answer:** a) for now: could be useful, but no such case yet. Kept as an idea for later.

**M5-18. "Videos on YouTube" (what YouTube says a channel has today).** TubeArchivist doesn't store it: not in its channel data, not in its per-channel totals, and its download queue has almost nothing. Only YouTube itself has it (through yt-dlp). If you saw it somewhere in TubeArchivist, say where.
  a) leave it for the future downloader (FUTURE_TA_REPLACEMENT.md), which reads channels through yt-dlp anyway
  b) (only if you found it in TubeArchivist) where: …
Proposal: a)
**Answer:** Dropped: it was only useful as the channel's current number on YouTube, which we don't have. (May come back with the future downloader.)
