# TonTuyau 2.0.0: plan of its pull requests

Draft, 2026-10-17; 2.0.0 since 2026-10-18 (it was 1.6 and 1.7, then took TonTuyau's own player and a Debug
configuration). 2.0 replaces TubeArchivist (see `FUTURE_TA_REPLACEMENT.md`): **PostgreSQL becomes the library's only
source of truth**, **TonTuyau downloads videos itself**, TubeArchivist becomes a one-time import, and TonTuyau gets
**its own player**. Same process as 1.5: one `work/2.0.0-…` branch per pull request into `release/2.0.0`, reviewed and
merged in order, alphas tagged from `release/2.0.0` when asked, then `release/2.0.0` → `main` and `v2.0.0`. Mockups
before any new page.

## Part A: the library in PostgreSQL (decided 2026-10-15 and 2026-10-16)

Elasticsearch becomes an index only, rebuilt from PostgreSQL alone; nothing lives only there any more.

**No upgrade path (decided 2026-10-18):** TonTuyau has no users yet. 2.0 starts from an empty database and a full sync
from TubeArchivist: nothing is carried over from 1.5 (no data migration, no fill from the index, no run history).

| PR | Branch | What |
|---|---|---|
| A1 | `work/2.0.0-library-tables` | New tables `videos` and `channels`, one row per video and per channel with everything: metadata, file, tracks, dates, pictures, the state on YouTube, chapters. **One column each (decided 2026-10-18 in A1's review)**: what TubeArchivist brings only adds a video or channel TonTuyau doesn't have, and never writes it again; a refresh (later a download) writes the same columns. Every row from TubeArchivist starts "not checked" on YouTube: TubeArchivist's own `active` flag isn't kept. The state on YouTube (state, checked, reason, refreshed) has a table of its own for each, `video_states` (`video_id` → `videos`) and `channel_states` (`channel_id` → `channels`), so either can gain a field the other doesn't. Their EF Core mapping and stores (`IVideoStore`, `IChannelStore`: add what's missing, record the file flag and the pictures' versions). Nothing reads them yet; A2 drops `youtube_videos` and `youtube_channels` and points `video_comments` to `videos`. |
| A2 | `work/2.0.0-sync-to-postgres` | The TubeArchivist sync writes PostgreSQL first, then the index from it. A refresh writes YouTube's facts into the same rows: TonTuyau's values prevail over TubeArchivist's, in PostgreSQL, no longer at each index write. |
| A3 | `work/2.0.0-index-from-postgres` | An index rebuild reads PostgreSQL only, never TubeArchivist: fast, and possible with TubeArchivist down. |
| A4 | `work/2.0.0-runs-in-postgres` | The runs (`sync-runs`), the run lock and the sync checkpoint (`sync`) move to PostgreSQL: a run's page reads one database; the lock becomes a row (or a PostgreSQL lock) instead of the lease on Elasticsearch. |
| A5 | `work/2.0.0-reads-from-postgres` | The pages that show one thing (a video, a channel, a run) read PostgreSQL; lists and search stay on the index. |
| A6 | `work/2.0.0-tubearchivist-import` | TubeArchivist becomes an import (decided 2026-10-18, was planned for 1.7): a full run only, at startup when its settings are there and on demand from the Tasks page, that takes from TubeArchivist only what TonTuyau doesn't have. No more deletions from TubeArchivist, nor incremental sync: the "apply deletions" task, the incremental sync, their schedule and guards go (`TONTUYAU_SYNC_INCREMENTAL_CRON`, `TONTUYAU_SYNC_FULL_CRON`, `TONTUYAU_SYNC_SCHEDULER_ENABLED`, `TONTUYAU_SYNC_MAX_DELETIONS`, `TONTUYAU_SYNC_INCREMENTAL_MARGIN_HOURS`, `TONTUYAU_SYNC_REJECT_GUARD_*`). `TONTUYAU_TA_URL` and `TONTUYAU_TA_TOKEN` optional: without them, TonTuyau runs without TubeArchivist, which can be shut down after the first run. |
| — | tag `v2.0.0-alpha.1` | Tried on the VM from an empty database and a full sync: same pages, same data, the index rebuilt from PostgreSQL. |

## Part B: TonTuyau downloads

| PR | Branch | What |
|---|---|---|
| B1 | `work/2.0.0-media-folder` | The media folder writable (TubeArchivist's layout kept: `<channel_id>/<video_id>.<ext>`), ffmpeg in the backend image (yt-dlp merges the best video and the best audio with it; a static build copied in and pinned by its SHA-256, as yt-dlp and Deno: the chiseled image has no package manager), a download of one video with yt-dlp, in the backend container as in 1.5, into it (the format from a setting, in yt-dlp's syntax, by default `bestvideo+bestaudio`; merged into `.mkv` with its chapters and metadata inside, decided 2026-10-15), its file, tracks and pictures recorded. No page yet. |
| B2 | `work/2.0.0-download-queue` | A download queue in PostgreSQL: one at a time, the pause between videos and the hold after a refusal (1.5's), resumable, cancellable; a run in the history like the refreshes. |
| B3 | `work/2.0.0-add-video` | "Add a video" on the Tasks page and `POST` in the API: queued, downloaded, then in the library. A video already in the library (from TubeArchivist or TonTuyau) isn't downloaded again. |
| B4 | `work/2.0.0-add-channel` | "Add a channel" (a subscription) with its three tabs (videos, shorts, streams), and which tabs to download; a channel's admin page shows it. |
| B5 | `work/2.0.0-youtube-syncs` | The YouTube incremental sync (each subscribed channel's new videos) and full sync (everything each channel has), on their own schedule, with the Tasks page's cards. |
| B6 | `work/2.0.0-download-settings` | The YouTube page's download settings: the format, in yt-dlp's syntax (`bestvideo+bestaudio` by default; `bestvideo[height>=700]+bestaudio`, for instance), which tabs by default, the schedule, how many videos a sync takes at most. |
| B7 | `work/2.0.0-extension-api` | LaPompe's "Add this video / channel": endpoints for a browser-extension key, and the contract it needs. |
| B8 | `work/2.0.0-refresh-all` | Refreshing every video from YouTube (decided 2026-10-18, was planned for 1.7): one run over the whole library, resumable, at the pace of 1.5's refreshes (the pause between videos, the hold after a refusal), from the Tasks page. |
| — | tag `v2.0.0-alpha.2` | Tried on the VM: a few channels downloaded by TonTuyau, TubeArchivist shut down after the first run. |

## Part C: TonTuyau's own player, and Debug (added 2026-10-18)

| PR | Branch | What |
|---|---|---|
| C1 | `work/2.0.0-player-mockup` | The mockup of TonTuyau's own player on the watch page (its controls, the chapters on its bar, the keyboard, phones), reviewed live before it's built. |
| C2 | `work/2.0.0-player` | The player built from the mockup, in place of the browser's own: the chapters on its bar. |
| C3 | `work/2.0.0-player-fetching` | The video fetched a few seconds ahead at a time, not the whole file as fast as the connection allows: lighter on the Cloudflare tunnel, whose congestion slowed every page in 1.5.0 (an HLS playlist whose parts ffmpeg copies out of the file when they're asked for, nothing kept: question 9). Close to instant: each file's keyframe times read once (at download and import) and kept in PostgreSQL, so the playlist comes from the database; one ffmpeg per viewing, started at the first part and going on with the next ones, so only the start and a seek pay for starting it; the init segment and the first part prepared when the watch page opens, before play. |
| C4 | `work/2.0.0-debug` | A "Debug" configuration that logs what's needed to find a slowdown (a setting in `.env`, applied at restart): every meaningful step (requests, database queries, index calls, yt-dlp and ffmpeg commands, task steps) with its start, end, duration and result, tied to its request or run (question 10). |
| — | tag `v2.0.0-alpha.3` | Tried on the VM: watching through the tunnel while videos download, Debug on. |

## Release

| PR | Branch | What |
|---|---|---|
| R1 | `work/2.0.0-version` | Version 2.0.0, the release notes, the upgrade steps (an empty database and a first run from TubeArchivist, the media folder now writable, the settings gone or optional). |

## Questions to decide (before the PR each one opens)

1. ~~**A3:** at the upgrade, fill PostgreSQL from the current index, or with a full import?~~ Neither: no upgrade path, an empty database and a full sync (2026-10-18).
2. ~~**A4:** copy the run history from `sync-runs` once, or start afresh?~~ Afresh: nothing is carried over (2026-10-18).
3. ~~**A2/A5:** do YouTube's facts stay in their own tables?~~ No: columns of the new `videos` and `channels`, which replace the `youtube_*` tables (2026-10-18).
4. ~~**B1:** yt-dlp in the backend container, or a worker container of its own?~~ In the backend container, as in 1.5 (2026-10-18).
5. ~~**B1/B6:** which formats by default?~~ The best video and the best audio (`bestvideo+bestaudio`), set on the page in yt-dlp's own format syntax (`bestvideo[height>=700]+bestaudio`, for instance) (2026-10-18).
6. ~~**B3/B5:** which rule keeps TubeArchivist's sync from deleting what TonTuyau downloaded?~~ TubeArchivist is only a source of missing data: on a later run, TonTuyau takes from it what it doesn't have and ignores the rest, so nothing is deleted because TubeArchivist no longer has it (2026-10-18).
7. ~~**B3:** when TubeArchivist later downloads a video TonTuyau already has, which file wins?~~ It doesn't arise: TubeArchivist is shut down after TonTuyau's first run at startup. Its settings (`TONTUYAU_TA_URL`, `TONTUYAU_TA_TOKEN`) become optional: without them, TonTuyau runs without TubeArchivist (2026-10-18).
8. ~~**Carry-over from 1.5.1:** a refresh noticing a stale YouTube cookie: note and go on, or hold?~~ Note and go on, as a refresh already does (2026-10-18).
9. ~~**C3:** how is the video fetched a few seconds at a time?~~ An HLS playlist (`.m3u8`) whose parts are made on the fly: ffmpeg copies the few seconds asked for out of the file into an MP4 fragment (no re-encoding, nothing written to disk), as Jellyfin does (2026-10-18). The files stay as they are: `.mkv` for TonTuyau's downloads (decided 2026-10-15: simpler, and a damaged spot loses only what it falls in), TubeArchivist's `.mp4` untouched. Caveats C3 handles: parts start on keyframes (the playlist from the file's keyframe index, `Cues` or `stss`); each part keeps its exact timestamps, or the player stutters at the joins (tested across many joins and seeks); one ffmpeg per viewing (not per part), the number running at once capped, stopped when the viewer leaves and restarted at a seek; the init segment (`EXT-X-MAP`) made on the fly too; the delay that leaves (about 0.1 to 0.3 s per part, mostly ffmpeg starting) kept out of sight by the keyframe times kept in PostgreSQL, one ffmpeg per viewing and the first part prepared when the page opens; the parts `Cache-Control: private`. How far ahead the player fetches is hls.js's setting (Chrome, Firefox; Safari plays HLS by itself); VP9/AV1/Opus play in Chrome and Firefox, in Safari only on recent devices, as today. "Download the file" still gives the original.
10. ~~**C4:** how is "Debug" switched on, and what does it log?~~ A setting in `.env`, applied at restart, for now; it logs every meaningful step, not every function: each request, database query, index call, yt-dlp or ffmpeg command and task step, with its start, end, duration and result, tied to the request or run it belongs to so the log reads in order (.NET's own tracing, `Activity`); secrets (the cookie, keys, passwords) never logged (2026-10-18).
