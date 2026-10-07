# TonTuyau — Q&A session 03

> 2026-10-01. What the round 3 answers (recorded inline in [QA_SESSION_02.md](QA_SESSION_02.md)) change, what M1 found, and round 4 questions.

---

## Round 3: what the answers change

| # | Answer | Consequence |
|---|---|---|
| R3-1 | Incremental in daytime if cheap; full the night Friday → Saturday 01:00 | Defaults now `TONTUYAU_SYNC_INCREMENTAL_CRON=0 7-23 * * *` (hourly 07:00–23:00) and `TONTUYAU_SYNC_FULL_CRON=0 1 * * 6`. The two never coincide. The hourly frequency is my pick → R4-1 |
| R3-2 | 10 files + images + the 4K | Done: 11 videos (1.5 GB) + thumbnails + channel art + metadata in `dev-data/`, codecs checked with ffprobe |
| R3-3 | Real metadata locally, anonymised fixtures in git | Done (anonymiser + leak check) |
| R3-4 | Sync design = its own file and discussion; you review a milestone → I merge → I start the next | [SYNC_DESIGN.md](SYNC_DESIGN.md) created (facts, requirements, cases, empty "Your design" section). M0 merged to `main`; M1 done and waiting for your review |

## M1 findings worth your attention

1. **TA's pagination doesn't do what its docs say**: 1-based pages, wrong `last_page`, 200 + empty list past the end, 500 past 10k. Details in [SYNC_DESIGN.md](SYNC_DESIGN.md).
2. **Five ARTE videos have a wrong `media_url` in TA**: it points to the *ARTE Info* folder, where the file doesn't exist. The file is in the *ARTE* folder that matches their `channel_id`. Ids: `PHizfeozVws`, `XtwM1km6m6k`, `MRw4xVz43lw`, `IIHDikXiN_k`, `KIzG-vXqeKE`. → R4-2

---

## Round 4: questions

**R4-1. Incremental frequency.** Hourly from 07:00 to 23:00 is ~17 runs a day. Is that OK, or do you want another rhythm (every 30 min, every 2 h…)?
Default: hourly 07:00–23:00.
**Answer:** Default.

**R4-2. The 5 inconsistent ARTE videos.**
  a) keep rejecting them (they're missing from TonTuyau, listed as rejected in `sync status`), and you fix them in TA if you want (e.g. a rescan/refresh of those videos on your side)
  b) accept them but trust `channel_id` over `media_url` for the file path. That means TonTuyau corrects TA's data, which bends "only rely on TA"
  c) accept them as TA says, with a playback that 404s
Default: a)
**Answer:** Correct TA's data: when it's fixed in TA, TonTuyau gets the corrected data anyway.

**R4-3. M1 review.** M1 is ready on `milestone/m1-ta-client` ([notes](milestones/M1-ta-client.md)). When you OK it, I merge it into `main` and start M2 (Elasticsearch store). M2 needs Docker on the laptop for the Testcontainers tests. Is it installed?
**Answer:** Docker is installed. I need time to review, so don't start anything.

**R4-4. Sync design.** Whenever you're ready, write your first version in [SYNC_DESIGN.md](SYNC_DESIGN.md) → *Your design*. M2 doesn't depend on it.
**Answer:** I don't really know how to formulate a design, since you listed all the cases it must handle. Then, after round 0 felt like guessing my decisions instead of making them: **format changed to "Claude proposes, you take it apart"**.

---

## Round 4: what the answers change

| # | Consequence |
|---|---|
| R4-1 | Incremental stays hourly 07:00–23:00 |
| R4-2 | New rule: when `media_url` points to another channel's folder, TonTuyau builds the path from the video's own `channel_id` (`<channel_id>/<video_id>.<ext from media_url>`) and records that it corrected it. Once TA is fixed, TA's value matches again and nothing is corrected. **Not applied yet**: I'll add it with your M1 review comments, so the branch doesn't move while you review |
| R4-3 | Nothing started. Docker is there, but your user can't reach the daemon yet (`permission denied on /var/run/docker.sock`): `sudo usermod -aG docker $USER`, then log out/in. M2 needs it |
| R4-4 | Round 0 (toy questions) dropped. [SYNC_DESIGN.md](SYNC_DESIGN.md) now holds **proposal v1**, with a *Your review* section to tear it apart |
