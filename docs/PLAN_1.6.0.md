# TonTuyau 1.6.0: plan of its pull requests

Draft, 2026-10-17. 1.6 is the second step of replacing TubeArchivist (see `FUTURE_TA_REPLACEMENT.md`): **PostgreSQL
becomes the library's only source of truth**, then **TonTuyau downloads videos itself**. Same process as 1.5: one
`work/1.6.0-…` branch per pull request into `release/1.6.0`, reviewed and merged in order, alphas tagged from
`release/1.6.0` when asked, then `release/1.6.0` → `main` and `v1.6.0`. Mockups before any new page.

## Part A: the library in PostgreSQL (decided 2026-10-15 and 2026-10-16)

Elasticsearch becomes an index only, rebuilt from PostgreSQL alone; nothing lives only there any more.

| PR | Branch | What |
|---|---|---|
| A1 | `work/1.6.0-library-tables` | The tables `videos` and `channels` (metadata, file, tracks, pictures, where it came from: TubeArchivist or TonTuyau), their EF Core mapping and repositories, behind the interfaces the sync already uses. Nothing reads them yet. |
| A2 | `work/1.6.0-sync-to-postgres` | The TubeArchivist sync writes PostgreSQL first, then the index from it. YouTube's facts (`youtube_videos`, `youtube_channels`) laid over in PostgreSQL, no longer at each index write. |
| A3 | `work/1.6.0-index-from-postgres` | An index rebuild reads PostgreSQL only, never TubeArchivist: fast, and possible with TubeArchivist down. The upgrade fills the tables once (from the current index or a full import: to decide). |
| A4 | `work/1.6.0-runs-in-postgres` | The runs (`sync-runs`), the run lock and the sync checkpoint (`sync`) move to PostgreSQL: a run's page reads one database; the lock becomes a row (or a PostgreSQL lock) instead of the lease on Elasticsearch. |
| A5 | `work/1.6.0-reads-from-postgres` | The pages that show one thing (a video, a channel, a run) read PostgreSQL; lists and search stay on the index. |
| — | tag `v1.6.0-alpha.1` | Tried on the VM: same pages, same data, the index rebuilt from PostgreSQL. |

## Part B: TonTuyau downloads

| PR | Branch | What |
|---|---|---|
| B1 | `work/1.6.0-media-folder` | The media folder writable (TubeArchivist's layout kept: `<channel_id>/<video_id>.<ext>`), ffmpeg in the image, a download of one video with yt-dlp into it (formats from a setting), its file, tracks and pictures recorded. No page yet. |
| B2 | `work/1.6.0-download-queue` | A download queue in PostgreSQL: one at a time, the pause between videos and the hold after a refusal (1.5's), resumable, cancellable; a run in the history like the refreshes. |
| B3 | `work/1.6.0-add-video` | "Add a video" on the Tasks page and `POST` in the API: queued, downloaded, then in the library. A video already in the library (from TubeArchivist or TonTuyau) isn't downloaded again. |
| B4 | `work/1.6.0-add-channel` | "Add a channel" (a subscription) with its three tabs (videos, shorts, streams), and which tabs to download; a channel's admin page shows it. |
| B5 | `work/1.6.0-youtube-syncs` | The YouTube incremental sync (each subscribed channel's new videos) and full sync (everything each channel has), on their own schedule, with the Tasks page's cards. |
| B6 | `work/1.6.0-download-settings` | The YouTube page's download settings: formats and quality, which tabs by default, the schedule, how many videos a sync takes at most. |
| B7 | `work/1.6.0-extension-api` | LaPompe's "Add this video / channel": endpoints for a browser-extension key, and the contract it needs. |
| — | tag `v1.6.0-alpha.2` | Tried on the VM: a few channels downloaded by TonTuyau beside TubeArchivist's. |
| B8 | `work/1.6.0-version` | Version 1.6.0, the release notes, the upgrade steps (the media folder now writable). |

## Questions to decide (before the PR each one opens)

1. **A3:** at the upgrade, fill PostgreSQL from the current index (quick, nothing re-read), or with a full import from TubeArchivist (slower, from the source)?
2. **A4:** copy the run history from `sync-runs` once, or start the history afresh?
3. **A2/A5:** do YouTube's facts stay in their own tables, or become columns of `videos` and `channels`?
4. **B1:** yt-dlp in the backend container (as in 1.5), or a worker container of its own (downloads can be heavy)?
5. **B1/B6:** which formats by default (the best quality, a maximum height, a codec preference)?
6. **B3/B5, important:** TubeArchivist's full sync deletes from TonTuyau what TubeArchivist no longer has. It must never delete what TonTuyau downloaded itself: which rule (by origin, or by file)?
7. **B3:** "downloaded once, whoever has it first": when TubeArchivist later downloads a video TonTuyau already has, which file wins?
8. **Carry-over from 1.5.1, if kept:** a refresh noticing a stale YouTube cookie ("Sign in to confirm your age" while one is stored): note and go on, or go on hold until a fresh one arrives?

## Not in 1.6 (planned later)

- Refreshing every video, resumable and rate-limited; the TubeArchivist sync becoming a full sync only, at startup and on demand (1.7).
- TonTuyau's own player: chapters on its bar, and videos fetched a few seconds at a time (it also eases the Cloudflare tunnel's congestion seen in 1.5.0).
- A "Debug" configuration that logs what's needed to find a slowdown.
