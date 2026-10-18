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
| A1 | `work/2.0.0-library-tables` | New tables `videos` and `channels`, one row per video and per channel with everything: what TubeArchivist brings (metadata, file, tracks, dates, pictures) and YouTube's facts (state, refreshed metadata, chapters). They replace `youtube_videos` and `youtube_channels`, dropped; `video_comments` points to `videos`. Their EF Core mapping and repositories, behind the interfaces the sync already uses. Nothing reads them yet. |
| A2 | `work/2.0.0-sync-to-postgres` | The TubeArchivist sync writes PostgreSQL first, then the index from it. A refresh writes YouTube's facts into the same rows: TonTuyau's values prevail over TubeArchivist's, in PostgreSQL, no longer at each index write. |
| A3 | `work/2.0.0-index-from-postgres` | An index rebuild reads PostgreSQL only, never TubeArchivist: fast, and possible with TubeArchivist down. |
| A4 | `work/2.0.0-runs-in-postgres` | The runs (`sync-runs`), the run lock and the sync checkpoint (`sync`) move to PostgreSQL: a run's page reads one database; the lock becomes a row (or a PostgreSQL lock) instead of the lease on Elasticsearch. |
| A5 | `work/2.0.0-reads-from-postgres` | The pages that show one thing (a video, a channel, a run) read PostgreSQL; lists and search stay on the index. |
| A6 | `work/2.0.0-tubearchivist-import` | TubeArchivist becomes an import (decided 2026-10-18, was planned for 1.7): a full run only, at startup when its settings are there and on demand from the Tasks page, that takes from TubeArchivist only what TonTuyau doesn't have. No more deletions from TubeArchivist, nor incremental sync: the "apply deletions" task, the incremental sync, their schedule and guards go (`TONTUYAU_SYNC_INCREMENTAL_CRON`, `TONTUYAU_SYNC_FULL_CRON`, `TONTUYAU_SYNC_SCHEDULER_ENABLED`, `TONTUYAU_SYNC_MAX_DELETIONS`, `TONTUYAU_SYNC_INCREMENTAL_MARGIN_HOURS`, `TONTUYAU_SYNC_REJECT_GUARD_*`). `TONTUYAU_TA_URL` and `TONTUYAU_TA_TOKEN` optional: without them, TonTuyau runs without TubeArchivist, which can be shut down after the first run. |
| — | tag `v2.0.0-alpha.1` | Tried on the VM from an empty database and a full sync: same pages, same data, the index rebuilt from PostgreSQL. |

## Part B: TonTuyau downloads

| PR | Branch | What |
|---|---|---|
| B1 | `work/2.0.0-media-folder` | The media folder writable (TubeArchivist's layout kept: `<channel_id>/<video_id>.<ext>`), ffmpeg in the backend image (yt-dlp merges the best video and the best audio with it; a static build copied in and pinned by its SHA-256, as yt-dlp and Deno: the chiseled image has no package manager), a download of one video with yt-dlp, in the backend container as in 1.5, into it (the format from a setting, in yt-dlp's syntax, by default `bestvideo+bestaudio`; written as a fragmented MP4, for the player's playlist: question 9), its file, tracks and pictures recorded. No page yet. |
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
| C3 | `work/2.0.0-player-fetching` | The video fetched a few seconds ahead at a time, not the whole file as fast as the connection allows: lighter on the Cloudflare tunnel, whose congestion slowed every page in 1.5.0 (an HLS playlist of byte ranges of the file itself, nothing cut: question 9). TubeArchivist's files remuxed once into fragmented MP4. |
| C4 | `work/2.0.0-debug` | A "Debug" configuration that logs what's needed to find a slowdown (a setting in `.env`, applied at restart; what it logs: question 10). |
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
9. ~~**C3:** how is the video fetched a few seconds at a time?~~ An HLS playlist (`.m3u8`) whose parts are byte ranges of the video file itself (`EXT-X-BYTERANGE`): nothing is cut, nothing is cached (2026-10-18). It needs fragmented MP4 files: TonTuyau writes its downloads so, and remuxes TubeArchivist's files once after the import (a copy, no re-encoding; written aside, then swapped in; a WebM/MKV becomes `.mp4`). The playlist is built once per file from its fragment index. How far ahead the player fetches is hls.js's own setting (Chrome, Firefox; Safari plays HLS by itself). VP9/AV1 in MP4 play in Chrome and Firefox, in Safari only on recent devices, as today.
10. **C4:** ~~how is "Debug" switched on?~~ A setting in `.env`, applied at restart, for now (2026-10-18). Still open: what it logs (the requests slower than a second, as tried in 1.5.0-alpha.7; the database and index queries; yt-dlp's commands)?
