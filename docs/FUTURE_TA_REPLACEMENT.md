# TonTuyau — Future plan: replacing TubeArchivist

> 2026-10-05. Your idea, written down after the M5 review. **Nothing here is built now**: it's a direction for after M8, recorded so today's choices don't block it. Decisions made in chat are marked **Decided**.

## The idea

TonTuyau stops being a viewer of TubeArchivist and becomes the archive itself: TubeArchivist (Python) is retired and its job is redone in C#, inside TonTuyau. What TubeArchivist is missing, and the reason to do it:

1. **A proper front, for viewers and for the admin**: the YouTube-like browsing we have, plus an admin UI held to the same standard.
2. **LDAP** instead of TubeArchivist's single account.
3. **Better scanning / rescanning control**: rescan one existing video, rescan one channel only, flag video ids that aren't worth rescanning, etc.

## Decided

- **Only the admin logs in.** LDAP group **`tontuyau_admin`** gives access to the admin part (downloads, subscriptions, rescans, settings). **There is no user account and no `tontuyau_user` group**: browsing and watching stay open on the LAN, without login, as today.
- **No extra capture in the sync for now.** The sync doesn't start keeping TubeArchivist's downloader data (subscriptions, per-channel settings, ignore list…) ahead of time.
- **The migration reads TubeArchivist's Elasticsearch directly.** You'll expose TubeArchivist's Elasticsearch on the LAN; the migration then dumps it completely, instead of going only through TubeArchivist's API.

## What already exists (and makes the migration small)

TonTuyau's sync already keeps its own copy, in its own Elasticsearch and art volume, of every video and channel: metadata, tracks, media paths, pictures (+ resized versions), with format checks, data corrections (R4-2) and deletion safety, proven on the real library (45,319 videos). On switch-over day the content is already in TonTuyau.

What only TubeArchivist knows, as a **downloader**, is what the dump must bring over:

| Data | Why it matters |
|---|---|
| Subscribed channels (and playlists) + per-channel settings (download format, limits, overrides) | what to keep downloading |
| Download queue, and the **ignored** video ids | pending work; what never to download again |
| yt-dlp settings, cookies, app settings | how downloads are made (cookies: see the anti-bot point below) |
| Playlists, subtitles, comments, SponsorBlock data | out of TonTuyau v1, but archived by TubeArchivist: kept so nothing is lost even if not shown yet |
| Refresh history (`vid_last_refresh`, `active`) | to repair the false "unavailable" flags (below) |

The exact list comes from TubeArchivist's indices on the day of the dump (their names and shapes depend on its version, v0.5.12 today).

## Principles for the replacement

- **yt-dlp stays, as an external program.** Rewriting TubeArchivist in C# doesn't mean reimplementing YouTube extraction (it changes every few weeks): the C# side runs **yt-dlp** as a separate process and owns everything around it: scheduling, queue, rescans, storage, admin UI. So the stack isn't Python-free; it's Python-free *for our code*.
- **"The check failed" is not "YouTube says it's gone".** TubeArchivist marks a video inactive when its refresh fails for any reason, which most likely explains the **14,210 videos (≈ 1/3)** flagged unavailable while they're still on YouTube (found 2026-10-05: all their last refreshes stopped in 2025, in bursts such as 1,951 on 2025-11-10). The new rescan must keep separate states (available / removed or private, confirmed / check failed, will retry) and only show "Unavailable on YouTube" for the confirmed case. Your point 3 (rescan one video or channel, ids not worth rescanning) is part of the answer.
- **The media files stay where they are.** TubeArchivist stores them as `<channel_id>/<video_id>.<ext>` on the share. The new downloader writes the same layout: switching over is a database change, not a move of 40k files.
- **The dump is read-only.** Reading TubeArchivist's Elasticsearch uses searches / point-in-time scrolls only: no writes, no snapshot repository on its cluster, no settings change ("Do not edit any data on TubeArchivist" still holds). Its credentials go in `.env` only, like the API key.

## Compatible with the TubeArchivist Companion (added 2026-10-05; replaced 2026-10-14 by TonTuyau's own extension, see below)

The **TubeArchivist Companion** browser extension adds buttons on YouTube pages ("download this video / subscribe to this channel") that talk to TubeArchivist's API. The replacement keeps it working: **TonTuyau's admin API answers the requests the Companion makes, the way TubeArchivist does** (same paths, same `Authorization: Token …` header, same JSON shapes), so the extension only needs TonTuyau's URL and a token instead of TubeArchivist's.

- Only the subset the Companion uses is reproduced, not TubeArchivist's whole API. The exact list (ping, add to the download queue, subscribe, "already archived?" checks…) is read from the extension's source when this stage starts, and pinned by contract tests recorded from the real extension.
- The token is an admin credential: it belongs to the `tontuyau_admin` side (an API token an admin creates in the admin UI), never to anonymous browsing.
- Our own REST API (API_CONTRACT.md) stays as it is; the Companion-compatible endpoints live beside it, under TubeArchivist's paths.



| Stage | What runs | TubeArchivist |
|---|---|---|
| 1. Today (M0–M8) | TubeArchivist downloads; TonTuyau syncs and shows | the source; read-only |
| 2. LDAP + admin UI | the admin part, first read-only (what TubeArchivist has, status, runs) | still the source; read-only |
| 3. Own downloader, side by side | TonTuyau's downloader (yt-dlp) on a few channels, while TubeArchivist keeps the rest | still running; read-only |
| 3b. Companion | the Companion pointed at TonTuyau (its compatible endpoints feed TonTuyau's downloader) | still running for the rest; read-only |
| 4. Dump + switch-over | a last sync + the full dump of TubeArchivist's Elasticsearch; TonTuyau's downloader takes every subscription | turned off (kept as a backup until you're sure) |

The read-only rule on TubeArchivist holds until stage 4; afterwards TonTuyau writes only its own data.

## 2026-10-14: started, in steps (1.5, 1.6, 1.7)

The replacement starts with 1.5 (decided in chat, 2026-10-14):

- **1.5**: metadata only. API keys; yt-dlp (with Deno and ffmpeg) pinned in the image, through Ytdlp.NET; the YouTube cookie, sent by **TonTuyau's own browser extension** (Firefox and Chrome, its own repository) instead of the TubeArchivist Companion; each video's and channel's YouTube state (on YouTube, retry later, gone, not checked: a failed check is never "gone", and a video TubeArchivist marked unavailable is "retry later" until TonTuyau checks it); refreshing a video, a channel or a channel's videos; a channel's admin page. The public "Unavailable on YouTube" badge shows only what TonTuyau confirmed. Once TonTuyau refreshed a video, the TubeArchivist sync no longer changes its metadata.
- **1.6**: downloads, into the media folder TonTuyau reads (it then needs write access): add a video, add a channel, the YouTube incremental and full syncs, every channel of the library, all three tabs; the extension's "Add this video / channel". A video is downloaded once, whoever (TubeArchivist or TonTuyau) has it first.
- **1.7**: refreshing every video (resumable, rate-limited); the TubeArchivist sync becomes a full sync only, once at each startup and on demand (no more incremental, no schedule).

TonTuyau is meant to be shared: it can't assume which TubeArchivist version someone runs, or that their TubeArchivist stopped downloading.

### Decided on 2026-10-15: PostgreSQL becomes the library's only source of truth (first step of 1.6)

In 1.5, Elasticsearch is a derived copy, rebuilt from two sources: TubeArchivist (what it imported) and PostgreSQL
(what TonTuyau learned itself: `youtube_videos`, `youtube_channels`, `video_comments`). Two shortcuts remain: a refresh
starts from the Elasticsearch document (its TubeArchivist part is a copy), and the API reads everything from
Elasticsearch, detail pages included.

1.6 starts by moving the whole library into PostgreSQL: a row per video and channel (metadata, file, tracks, state),
the TubeArchivist import becoming one way to fill it, TonTuyau's downloads another. Elasticsearch then is only an
index for search and lists, rebuilt from PostgreSQL alone. The downloads need those rows anyway.

### Later: chapters in the player

1.5 shows a video's chapters in its description (always visible), since the player is the browser's own. A later
version puts them in the player itself, like YouTube (marks on the bar, the chapter's name), with TonTuyau's own
player controls.

## Open (later, not now)

- The exact list of TubeArchivist indices and fields to bring over (read on its exposed Elasticsearch when it's available).
- How the admin UI looks (a mockup reviewed live first, like M5a).
- Where yt-dlp runs (in the backend container, or its own worker container) and how it's kept up to date.
- Cookies / anti-bot handling for the rescans and downloads.
- Whether the TonTuyau sync keeps running during stage 3 (probably yes, for the channels TubeArchivist still handles).
