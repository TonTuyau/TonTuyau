# TonTuyau — REST API contract (v2.7)

> 2026-10-03. The shared contract between the backend (M4) and the frontend (M5a mockup, then M5/M6). Both sides build against this file; a change goes through you and is written here first.
> Decisions: [QA_SESSION_05.md](QA_SESSION_05.md), [QA_SESSION_06.md](QA_SESSION_06.md), [QA_SESSION_07.md](QA_SESSION_07.md).
>
> **v1.1 (2026-10-03)**: `VideoDetail.channel.subscribers` (M5a-2) · `exclude` on a channel's videos (M5a-3) · `kind=all` later pages give `"channels": []` (M5a-13) · suggestions: at most 3 channels (M4-Q2) · no TV art: `tvArtUrl` and the `tvart` image kind are gone (M5a-17).
>
> **v1.2 (2026-10-03)**: search date filter is a named period, `published=today|week|month|year`; `publishedAfter` / `publishedBefore` removed (M5a-1).
>
> **v1.3 (2026-10-03)**: `exclude` on `/api/videos` too, for the shorts player's library-wide sequence (M5a-18, [QA_SESSION_07.md](QA_SESSION_07.md)).
>
> **v1.4 (2026-10-04)**: resized images ([QA_SESSION_09.md](QA_SESSION_09.md)): every image URL comes with a list of sized versions (`thumbnails`, `banners`) for `srcset`; new art URLs `/art/videos/{id}/{width}` and `/art/channels/{id}/{kind}/{width}`.
>
> **v1.5 (2026-10-10, TonTuyau 1.2)**: logging in: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`. Browsing still needs no login.
>
> **v1.6 (2026-10-12, TonTuyau 1.2)**: the admin dashboard: `GET /api/admin/dashboard` and its import history `GET /api/admin/dashboard/imports`, logged-in accounts only (401 otherwise).
>
> **v1.7 (2026-10-13, TonTuyau 1.4)**: the admin's Tasks page: `GET /api/admin/tasks`, `GET /api/admin/runs`, `GET /api/admin/runs/{id}`, `GET /api/admin/runs/{id}/log` read the runs; `POST /api/admin/tasks/{task}/runs` starts a task, `POST /api/admin/runs/{id}/cancel` cancels a run. Logged-in accounts only (401 otherwise).
>
> **v1.8 (2026-10-15, TonTuyau 1.5)**: API keys, for programs: `GET /api/admin/api-keys`, `POST /api/admin/api-keys` (the secret given once), `POST /api/admin/api-keys/{id}/enable`, `POST /api/admin/api-keys/{id}/disable`, `DELETE /api/admin/api-keys/{id}`, managed by a logged-in admin. A key has a **kind** and opens only the endpoints that take it: `browserExtension` (the only kind in 1.5) opens `/api/extension/…`: `GET /api/extension/connection`, and the YouTube cookie `GET`/`PUT /api/extension/youtube-cookie`. Every other logged-in endpoint is an admin's session only. The admin's YouTube page: `GET /api/admin/youtube`, `POST /api/admin/youtube/cookie/check`, `DELETE /api/admin/youtube/cookie`; which yt-dlp runs: `GET`/`PUT /api/admin/youtube/ytdlp`, `GET /api/admin/youtube/ytdlp/versions`.
>
> **v1.9 (2026-10-15, TonTuyau 1.5)**: `active` on videos and channels is TonTuyau's own word: `false` only once TonTuyau itself found the video or channel gone from YouTube (removed, private, terminated). TubeArchivist's "unavailable" alone no longer makes it `false` (it's a check to retry). Same shape.
>
> **v1.10 (2026-10-15, TonTuyau 1.5)**: the refreshes on YouTube, three new tasks: `refreshVideo`, `refreshChannel`, `refreshChannelVideos`, started with `POST /api/admin/tasks/{task}/runs?target=<id>` (the video's or channel's YouTube id, one of the library). A run gains `target`, its counts `foundOnYouTube`, `goneFromYouTube`, `checkFailed`.
>
> **v1.11 (2026-10-15, TonTuyau 1.5)**: a refresh reads the videos' chapters, and their comments when the YouTube settings say so (`GET`/`PUT /api/admin/youtube/settings`: `comments` = `off` (the default) | `top` (up to `commentsMax`) | `all`): a run's counts gain `commentsRead`. (They're shown on the watch page next.)
>
> **v1.12 (2026-10-16, TonTuyau 1.5)**: a channel's admin page: `GET /api/admin/channels/{id}` and `GET /api/admin/channels/{id}/videos` (by state on YouTube or words, page by page). The dashboard's channels gain `retryLater` and `notChecked`.
>
> **v1.13 (2026-10-16, TonTuyau 1.5)**: the watch page's comments and chapters: `GET /api/videos/{id}/comments`, and `chapters` in `VideoDetail` (`startSeconds`, `endSeconds`, `title`).
>
> **v1.14 (2026-10-16, TonTuyau 1.5)**: a refresh YouTube refuses (a bot check, too many requests) is put on hold instead of reading on: its `state` is `onHold`, with `resumeAt` and `left`; `GET /api/admin/tasks` lists them in `onHold`; `POST /api/admin/runs/{id}/resume` resumes one now, and cancel cancels one at once. The YouTube settings gain `pauseSeconds` and `holdMinutes`.
>
> **v1.15 (2026-10-16, TonTuyau 1.5)**: `POST /api/admin/runs/{id}/hold` puts a refresh going on on hold by hand: it then has no `resumeAt`, and waits for resume.
>
> **v1.16 (2026-10-17, TonTuyau 1.5)**: two more states on YouTube, `private` (its owner made it private: read again at each refresh) and `unlisted` (on YouTube, not listed); `gone` no longer includes private videos, and a gone video isn't refreshed any more (`refreshVideo` on one: 409; a channel's refresh skips them). Videos gain `youTubeState`; the dashboard's channels gain `private` and `unlisted`, a channel's admin page counts them, its videos can be listed by them; a run's counts gain `unlistedOnYouTube` (of those found) and `privateOnYouTube`.
>
> **v2.9 (2026-10-21, C3)**: a video played in parts (HLS), so the player fetches about 30 s ahead instead of the whole file: `GET /media/{id}/hls/{session}/index.m3u8`, `…/init.mp4`, `…/{n}.m4s`, `DELETE /media/{id}/hls/{session}`; `session` is the viewing's, made by the player. 409 when the file can't be played in parts: the player plays `GET /media/{id}` as before.

> **v2.8 (2026-10-21, alpha.3)**: the dashboard's channels move to their own call, a page at a time: `GET /api/admin/dashboard/channels` (`q`, `sort`, `order`, `gone`, `cursor`, `size`); `GET /api/admin/dashboard` no longer has `channels`.

> **v2.7 (2026-10-21)**: "Refresh everything" ([PLAN_2.0.0.md](PLAN_2.0.0.md), B8): a task `refreshAll` (`POST /api/admin/tasks/refreshAll/runs?days=`), each channel then its videos read on YouTube, the least recently read first, at the refreshes' pace (held on a refusal); what was read in the last `days` days (0 to 365, 7 by default; 0: nothing) is skipped, what's gone never read again. `GET /api/admin/tasks` lists it after the syncs.
>
> **v2.6 (2026-10-21)**: "Add to TonTuyau" from the browser extension ([PLAN_2.0.0.md](PLAN_2.0.0.md), B7). The connection test gains `canAdd` (`true`: the endpoints below are there; absent before, read as no). `GET /api/extension/videos/{id}` and `GET /api/extension/channels/{id}` say where a YouTube video or channel stands: `state` = `absent` \| `fileMissing` (a video of the library without its file) \| `waiting` (its run waits, goes on or is on hold; `runId`) \| `inTonTuyau` (a video with its file, a channel subscribed to). `POST` on the same addresses adds it, 202 and the same answer (`waiting`, `runId`): the runs of `downloadVideo` and `addChannel` (a channel gets the YouTube settings' `defaultTabs`), through the queue, `trigger` `extension` (new) and `startedBy` the key; one already waiting gives its run; 409 when it's in TonTuyau already. A run's `trigger` gains `extension`.
>
> **v2.5 (2026-10-21)**: the download settings ([PLAN_2.0.0.md](PLAN_2.0.0.md), B6). The YouTube settings gain `downloadFormat` (what a download asks yt-dlp for, in its syntax, `bestvideo+bestaudio` by default; one line, 1 to 200 characters, trimmed, checked no further: a format that picks nothing fails the download, saying why) and `defaultTabs` (`videos`, `shorts`, `streams`: what a channel added gets when no `tabs` is named, the three by default; each one given, at least one true). `addChannel` without `tabs` takes `defaultTabs` (a run asked for shows the tabs it got). Saving the settings without them keeps them.
>
> **v2.4 (2026-10-20)**: the YouTube syncs ([PLAN_2.0.0.md](PLAN_2.0.0.md), B5). Two new tasks, `youTubeSync` (each subscribed channel's ticked tabs, newest first, down to the first video the library has; a channel never synced listed whole; by itself every `syncEveryHours`, `trigger` `scheduler`) and `youTubeFullSync` (every tab whole, what's missing downloaded, files missing included; by hand only): `POST /api/admin/tasks/{task}/runs`, no `target`; `youTubeFullSync?target=` a channel's id is that channel's full sync alone ("Download everything" on its admin page; 409 when TonTuyau isn't subscribed to it, 400 when it isn't a channel id). `GET /api/admin/tasks` lists them in `tasks` after `import`, each with `next` (when it runs by itself next, in the past when it's due now; null: by hand only). A run's counts gain `downloadFailed` (a sync: the videos listed it didn't download, noted each); `checkFailed` also counts a sync's tabs not listed. The YouTube settings gain `syncEveryHours` (0 to 168, 6 by default; 0: by hand only). Failed downloads are kept: `GET /api/admin/channels/{id}/failed-downloads` lists a channel's (`videoId`, `title`, `reason` as yt-dlp said it, `tries`, `skipped` once failed 3 times: the incremental sync leaves it, the full sync and "Download everything" still try it, `firstFailedAt`, `lastFailedAt`, `youTubeUrl`), the latest failed first; a video leaves the list once downloaded. Try again: `downloadVideo` of one, the new task `retryDownloads?target=` a channel's id of them all (409 when it has none).
>
> **v2.3 (2026-10-20)**: subscriptions ([PLAN_2.0.0.md](PLAN_2.0.0.md), B4). A new task `addChannel` (`POST /api/admin/tasks/addChannel/runs?target=&tabs=`): `target` a channel's id, its `@handle`, or a YouTube link to either; `tabs` = `videos,shorts,streams` or some of them (the three when absent); the run reads it on YouTube, adds it when it's new, and subscribes to it (nothing is downloaded: the YouTube syncs, B5, do); 409 when TonTuyau is subscribed to it already. A run gains `tabs` (`addChannel`: `videos`, `shorts`, `streams`; null otherwise), its counts `subscribed`, and while an `addChannel` run reads a handle its `target` is the handle, then the channel's id. `GET /api/admin/channels/{id}` gains `subscription` (`tabs`, `since`, `by`, `origin` = `tubeArchivist` \| `tonTuyau`; null: not subscribed); `PUT /api/admin/channels/{id}/subscription` subscribes or changes the tabs, `DELETE` unsubscribes. The import takes TubeArchivist's subscriptions with the channels it adds (the three tabs).
>
> **v2.2 (2026-10-20)**: "Add a video" ([PLAN_2.0.0.md](PLAN_2.0.0.md), B3): `POST /api/admin/tasks/downloadVideo/runs?target=` downloads a video into the library, through the queue; `target` is its id or a YouTube link to it (`watch?v=`, `youtu.be/`, `/shorts/`, `/live/`, `/embed/`), 400 otherwise. A video of the library is refused (409), unless its file is missing: then its file is downloaded, and the flag cleared.
>
> **v2.1 (2026-10-20)**: one run queue ([PLAN_2.0.0.md](PLAN_2.0.0.md), B2). Starting a task while another runs no longer answers 409: the run is `queued` (a new `state`) and starts by itself when its turn comes, first come first served, after the run going on and while no run is on hold. A run gains `queuedAt` (when it was asked for) and `position` (queued: its place in line, from 1; null otherwise); while queued, `startedAt` is `queuedAt`, then when it really started. `GET /api/admin/tasks` gains `queue` (the queued runs, first first). Cancel on a queued run cancels it at once: it doesn't start.
>
> **v2.0 (2026-10-18)**: the TubeArchivist import replaces the sync ([PLAN_2.0.0.md](PLAN_2.0.0.md), A2). Tasks: `import`, `indexRebuild` and the refreshes; `incremental`, `full`, `applyDeletions` are gone, and so are `schedule`, `next`, `waiting` (a task now has `task`, `lastRun`, `unavailable`), the run's `deletionsPlanned`, `deletionsBlocked`, `appliesTo` and the counts `deleted`, `kept`.

## Rules

- **Read-only** library: every library endpoint is `GET` (and `HEAD` for media/art). The import runs when the backend starts (v2.0) and from the CLI or the Tasks page; the Tasks page reads the runs (v1.7). The only `POST`s are logging in and out (v1.5, JSON only), starting a task or canceling a run from the Tasks page (v1.7, no body), and managing the API keys (v1.8, the one `DELETE` too).
- **Logged in** means the session cookie (`POST /api/auth/login`): an admin. Programs use an API key instead (v1.8), sent as `Authorization: Bearer tt_…`, named `"<name> (API key)"`; a key opens only the endpoints that take its kind (`browserExtension`: `/api/extension/…`, where an admin's session works too) and gets 403 `api-key-not-allowed` anywhere else. An unknown, malformed or disabled key gets 401 `api-key-refused` and `WWW-Authenticate: Bearer`, the same for each.
- **JSON**, camelCase property names, dates as ISO-8601 UTC (`2026-10-02T20:18:48Z`), durations in seconds.
- **The front never builds or guesses anything**: every URL it needs (thumbnails, banners, media, download) is given by the API, every count and label-worthy fact too (counts per type, inactive flag, playable MIME type…).
- **Lists** are paged with opaque cursors:
  ```json
  { "items": [ ... ], "nextCursor": "eyJ…" | null, "total": 44979 }
  ```
  `size` (default 24, 1–100) and `cursor` (from the previous `nextCursor`). `total` is exact. A cursor is only valid with the same filters and sort.
- **Errors** are RFC 9457 ProblemDetails (`application/problem+json`):
  ```json
  { "type": "https://tontuyau/errors/validation", "title": "Invalid request", "status": 400,
    "detail": "sort: must be one of latest, popular, oldest.", "errors": { "sort": ["must be one of latest, popular, oldest."] } }
  ```
  400 invalid parameter · 404 unknown id (or missing file/image) · 416 bad range · 500 without internals.
- **Version** (M7): every answer carries `X-TonTuyau-Version: 1.1.0` (the backend's version, the same as `tontuyau --version`). Informative only: the front doesn't read it.

## Shapes

### `ImageSize` (v1.4)

```json
{ "width": 360, "url": "/art/videos/y07hZ0XvwUg/360" }
```
Every image comes as its original URL **plus** a list of `ImageSize`, sorted by width ascending, **the original last** (with its real width). The front joins the list into `srcset` (`url width"w"`) and lets the browser pick; it never builds a URL. Only versions that exist are listed: before the sync has made them (or for an image smaller than a size), the list holds fewer entries, at least the original. No image: the URL is `null` and the list is `[]`.

Sizes made by the sync (Q9-2, Q9-3), WebP: video thumbnails 360 and 720 px wide · channel pictures 48, 96, 176, 320 px · banners 1280 px.

### `VideoCard` (lists)

```json
{
  "id": "y07hZ0XvwUg",
  "title": "HUILES : pourquoi certaines deviennent dangereuses à la cuisson",
  "type": "video",
  "published": "2026-04-24T15:01:48Z",
  "durationSeconds": 1914,
  "views": 74198,
  "active": true,
  "youTubeState": "onYouTube",
  "thumbnailUrl": "/art/videos/y07hZ0XvwUg",
  "thumbnails": [
    { "width": 360, "url": "/art/videos/y07hZ0XvwUg/360" },
    { "width": 720, "url": "/art/videos/y07hZ0XvwUg/720" },
    { "width": 1280, "url": "/art/videos/y07hZ0XvwUg" }
  ],
  "channel": {
    "id": "UCmwHXyZdXAFuE6-PLQTnGOg", "name": "Tom Cooks",
    "thumbnailUrl": "/art/channels/UCmwHXyZdXAFuE6-PLQTnGOg/thumb",
    "thumbnails": [ { "width": 48, "url": "/art/channels/UCmwHXyZdXAFuE6-PLQTnGOg/thumb/48" }, "…", { "width": 900, "url": "/art/channels/UCmwHXyZdXAFuE6-PLQTnGOg/thumb" } ]
  }
}
```
`type`: `video` | `short` | `stream`. `active: false` = gone from YouTube (YouTube says it was removed, or its account terminated), as TonTuyau itself found it (v1.9), still archived. TubeArchivist's "unavailable" alone keeps `active: true`. `youTubeState` (v1.16) = `notChecked` \| `onYouTube` \| `unlisted` \| `private` \| `retryLater` \| `gone`: TonTuyau's state of it on YouTube; visitors see a badge for `gone` ("Gone from YouTube"), `private` and `unlisted`. `thumbnailUrl` / `channel.thumbnailUrl` may be `null` when there's no image (then `thumbnails` is `[]`).

### `VideoDetail` (`/api/videos/{id}`)

`VideoCard` plus the embedded channel's subscriber count (`"channel": { "id", "name", "thumbnailUrl", "thumbnails", "subscribers": 224000 }`) and:
```json
{
  "description": "…",
  "downloaded": "2026-10-01T07:13:01Z",
  "likes": 3166,
  "tags": ["cuisine"],
  "categories": ["Howto & Style"],
  "fileSize": 1313668754,
  "mimeType": "video/mp4; codecs=\"av01.0.08M.08, opus\"",
  "tracks": [
    { "kind": "video", "codec": "av1", "width": 3840, "height": 2160, "bitrate": 5370710 },
    { "kind": "audio", "codec": "opus", "width": null, "height": null, "bitrate": 110978 }
  ],
  "mediaUrl": "/media/y07hZ0XvwUg",
  "downloadUrl": "/media/y07hZ0XvwUg/download",
  "mediaAvailable": true
}
```
- `mimeType`: what the front passes to `video.canPlayType()` / `MediaSource.isTypeSupported()` to decide whether to show the player or the "this browser can't play <codec>" message + download button (R2-13). Built by the backend from the tracks (container from the file extension, a representative codec string per codec family).
- `mediaAvailable: false` when the full sync found the file missing on the share: the front shows "file unavailable" instead of the player.
- `chapters` (v1.13): `[ { "startSeconds", "endSeconds", "title" } ]`, in order; empty until TonTuyau refreshed the video on YouTube, or when YouTube marks none.

### `ChannelCard` (lists)

```json
{
  "id": "UCmwHXyZdXAFuE6-PLQTnGOg",
  "name": "Tom Cooks",
  "subscribers": 224000,
  "thumbnailUrl": "/art/channels/UCmwHXyZdXAFuE6-PLQTnGOg/thumb",
  "thumbnails": [ "ImageSize…, original last" ],
  "videoCounts": { "videos": 120, "shorts": 14, "streams": 0 }
}
```

### `ChannelDetail` (`/api/channels/{id}`)

`ChannelCard` plus:
```json
{ "description": "…", "bannerUrl": "/art/channels/UCmw…/banner", "banners": [ { "width": 1280, "url": "/art/channels/UCmw…/banner/1280" }, { "width": 2560, "url": "/art/channels/UCmw…/banner" } ], "tags": ["Cuisine"], "active": true }
```
Tabs to show = the types whose `videoCounts` are > 0 (counted by the backend).

### `Suggestion`

```json
{ "text": "Tom Cooks", "kind": "channel", "id": "UCmwHXyZdXAFuE6-PLQTnGOg" }
```

## Endpoints

| Endpoint | Returns | Parameters |
|---|---|---|
| `GET /api/home` | `{ "sections": [ { "type": "video" \| "short" \| "stream", "items": VideoCard[] } ] }` | `size` per section (default 24). Latest **published** first (R2-19); sections in the order video, short, stream; empty sections omitted |
| `GET /api/videos` | page of `VideoCard` | `type`, `sort` = `latest` (published desc, default) \| `popular` (views desc) \| `oldest`, `exclude` (a video id left out of the list and of `total`: the short being watched, for the shorts player), `cursor`, `size` |
| `GET /api/videos/{id}` | `VideoDetail` | 404 if unknown |
| `GET /api/videos/{id}/comments` | `CommentsResponse` (v1.13): `readAt` (when TonTuyau read them on YouTube; null: never, then `items` is empty and the page shows no comments), `youTubeCount` (YouTube's count, or null), `kept` (how many TonTuyau keeps, answers included), `items` (threads: `comment` and its `replies`, each `id`, `text`, `author`, `published`, `likes`, `pinned`, `byUploader`, `hearted`), `nextCursor`, `total` (threads in all) | `sort` = `top` \| `newest` (default `top`: YouTube's order), `cursor`, `size` (threads, 1–100, default 20); 400 otherwise, 404 if the video is unknown |
| `GET /api/channels` | page of `ChannelCard` | `q` (name, as-you-type, up to 100 characters), `sort` = `name` (default) \| `subscribers`, `cursor`, `size` |
| `GET /api/channels/{id}` | `ChannelDetail` | 404 if unknown |
| `GET /api/channels/{id}/videos` | page of `VideoCard` | `type` (default `video`), `sort` = `latest` \| `popular` \| `oldest`, `exclude` (a video id left out of the list and of `total`: the one being watched, for "More from <channel>"), `cursor`, `size` |
| `GET /api/search` | see below | `q` (required, 1–200 chars), `kind` = `all` (default) \| `videos` \| `channels`, filters for videos: `type`, `channel` (id), `published` = `today` \| `week` \| `month` \| `year` (published within the last 24 h / 7 / 30 / 365 days, computed by the backend on the first page and kept in the cursor so later pages use the same cut-off), `duration` = `short` (< 4 min) \| `medium` (4–20 min) \| `long` (> 20 min), `sort` = `relevance` (default) \| `latest` \| `popular`, `cursor`, `size` |
| `GET /api/search/suggest` | `{ "suggestions": Suggestion[] }` | `q` (required, 1–100 chars), `size` (default 8, max 20): **at most 3 channels** first, then video titles fill the rest, by prefix (as-you-type, accents ignored) |
| `GET /api/auth/me` | `{ "username": "admin" }`, or 401 when nobody is logged in | none: the session cookie says who |
| `POST /api/auth/login` | 204 and the session cookie (HttpOnly, SameSite=Strict, Secure): 8 hours renewed while in use, 30 days with `remember` | JSON body `{ "username", "password", "remember" }` (4 KB at most, 415 if not JSON); 401 with the same message for every refusal; 429 after 5 attempts in a minute from one address |
| `POST /api/auth/logout` | 204, the cookie deleted | no body |
| `GET /api/admin/dashboard` | `DashboardResponse`: `lastSync` (when the latest successful sync finished, or null), `totals` (`channels`, `videos`, `shorts`, `streams`, `gone`: videos no longer available on YouTube), `playTime` (`totalSeconds`, and per type `count`, `seconds`, `share` from 0 to 1, `averageSeconds`) (its channels: their own call since v2.8) | none; logged in only (the session cookie), 401 otherwise |
| `GET /api/admin/dashboard/channels` | (v2.8) page of `ChannelTotals`: `id`, `name`, `thumbnailUrl`, `thumbnails`, `counts`, `gone`, `private`, `unlisted` (v1.16), `retryLater`, `notChecked` (v1.12), `total`; `total` of the page = the channels matching | `q` (words of the name, accents and case ignored, up to 100 characters), `sort` = `name` \| `videos` \| `shorts` \| `streams` \| `gone` \| `private` \| `unlisted` \| `retryLater` \| `notChecked` \| `total` (default), `order` = `asc` \| `desc` (default: `asc` for `name`, `desc` for a count; ties by name, then id), `gone` = `true` (only the channels with videos gone from YouTube) \| `false` (default), `cursor`, `size` (1–100, 24 by default); 400 otherwise; logged in only, 401 otherwise |
| `GET /api/admin/channels/{id}` | `AdminChannelResponse` (v1.12): `channel` (a `ChannelCard`), `youTube` (`state` = `notChecked` \| `onYouTube` \| `unlisted` \| `private` \| `retryLater` \| `gone` (v1.16), `checkedAt`, `reason`, `refreshedAt`), `videos` (how many of its videos are in each state: `notChecked`, `onYouTube`, `unlisted`, `private`, `retryLater`, `gone`), `subscription` (v2.3: `SubscriptionResponse`, null when TonTuyau isn't subscribed to it) | 400 if not a channel id, 404 if unknown; logged in only, 401 otherwise |
| `PUT /api/admin/channels/{id}/subscription` | 200 and the `SubscriptionResponse` (v2.3): `tabs` (`videos`, `shorts`, `streams`: which tabs the YouTube syncs download), `since`, `by` (the account; null when not an account: the import, the command line), `origin` = `tubeArchivist` (taken from it at the import) \| `tonTuyau`. Subscribes, or changes the tabs: then `since`, `by` and `origin` stay | JSON body `{ "videos", "shorts", "streams" }`, each `true` or `false`, required, at least one `true` (1 KB at most, 415 if not JSON); 400 otherwise, 404 if the channel is unknown; logged in only, 401 otherwise |
| `GET /api/admin/channels/{id}/failed-downloads` | `DownloadFailureResponse[]` (v2.4), the latest failed first: `videoId`, `title` (as its tab listed it; null when not given), `reason` (the last try's, as yt-dlp said it), `tries`, `skipped` (failed 3 times: the incremental sync leaves it), `firstFailedAt`, `lastFailedAt`, `youTubeUrl` | 400 if not a channel id, 404 if unknown; logged in only, 401 otherwise |
| `DELETE /api/admin/channels/{id}/subscription` | 204 (v2.3): unsubscribed, whether it was or not; the channel and its videos stay | 404 if the channel is unknown; logged in only, 401 otherwise |
| `GET /api/admin/channels/{id}/videos` | page of `AdminVideoResponse` (v1.12), latest published first: `video` (a `VideoCard`), `youTube` (`state`, `checkedAt`, `reason`; `refreshedAt` null here) | `state` = `notChecked` \| `onYouTube` \| `unlisted` \| `private` \| `retryLater` \| `gone` (every state when absent), `q` (words of the title, tags or description, 1–100 characters), `cursor`, `size` (1–100, default 24); 400 otherwise, 404 if the channel is unknown; logged in only, 401 otherwise |
| `GET /api/admin/dashboard/imports` | `ImportHistoryResponse`: the new videos of the finished sync runs of the period (never an index rebuild): `step` = `run` \| `day` \| `week` (from Monday) \| `month`, `timeZone` (the sync's, `TONTUYAU_TZ`: the days, weeks and months are its own), `total`, `bars` (oldest first, every step of the period, empty ones too: `start` = when the step starts, midnight in that zone, or when the run started; `total`; `byType` as `VideoCounts`; `notRecorded`: the new videos of runs of 1.1, which didn't record the types) | `period` = `day` (a bar per run of the last 24 h) \| `week` (a bar per day, the last 7 days) \| `month` (default: per day, the last 30 days) \| `year` (per week, the weeks of the last 365 days) \| `all` (per month, since the first import); 400 otherwise; logged in only, 401 otherwise |
| `GET /api/admin/tasks` | `TasksResponse`: `lock` (a run going on, in any process: `task`, `trigger`, `since`; null when none: then any task can start), `runningHere` (the id of the run going on in this backend, which can be canceled; or null), `tasks` (v2.0): `import`, then (v2.4) `youTubeSync` and `youTubeFullSync`, each with `task`, `lastRun` (a `RunResponse`, or null), `unavailable` (why it can't start: TubeArchivist's settings aren't there; null when it can), `next` (v2.4: when it runs by itself next, in the past when due now; null: by hand only); `onHold` (v1.14: the refreshes on hold, a `RunResponse` each, the one resuming first first); `queue` (v2.1: the runs waiting their turn, a `RunResponse` each with its `position`, first first; it waits while `lock` is set or `onHold` isn't empty) | none; logged in only, 401 otherwise |
| `GET /api/admin/runs` | page of `RunResponse`, latest first: `id`, `task` = `import` \| `indexRebuild` \| `refreshVideo` \| `refreshChannel` \| `refreshChannelVideos` (v2.0) \| `downloadVideo` (v2.2) \| `addChannel` (v2.3) \| `youTubeSync` \| `youTubeFullSync` \| `retryDownloads` (v2.4) \| `refreshAll` (v2.7), `trigger` = `scheduler` \| `cli` \| `startup` \| `admin` (the Tasks page) \| `extension` (v2.6: the browser extension), `startedBy` (for `admin`: the account; for `extension`: the key, `"Firefox, laptop (API key)"`; null otherwise), `state` = `running` \| `succeeded` \| `failed` \| `canceled` \| `onHold` (v1.14: a refresh YouTube refused, waiting to resume; the lock is free meanwhile) \| `queued` (v2.1: waiting its turn), `startedAt` (queued: when it was asked for), `queuedAt` (v2.1: when it was asked for), `position` (v2.1: queued, its place in line from 1; null otherwise), `finishedAt` (on hold: when it stopped), `error`, `counts` (`videosRead`, `channelsRead`, `created`, `updated`, `unchanged`, `rejected`, `corrected`, `artCopied`, `artFailed`, `artResized`, `artResizeFailed`, `mediaMissing`, `foundOnYouTube`, `unlistedOnYouTube` (v1.16: of those found, the unlisted ones), `goneFromYouTube`, `privateOnYouTube` (v1.16), `checkFailed`, `commentsRead`, `subscribed` (v2.3: the channels subscribed to, by an import or `addChannel`)), `target` (a refresh: the video's or channel's id; `addChannel`, v2.3: its id, or its handle until the run read the id), `tabs` (v2.3: `addChannel`, the tabs it subscribes to; null otherwise), `resumeAt` (v1.14: on hold, when it resumes by itself; null otherwise, and when put on hold by hand, v1.15: it waits for resume), `left` (v1.14: on hold, the videos and channel it has left to read; 0 otherwise), `indexRebuild`, `notes`, `cancellable` (going on in this backend, on hold, or queued) | `task` = `import` \| `indexRebuild` \| `refreshVideo` \| `refreshChannel` \| `refreshChannelVideos` \| `downloadVideo` \| `addChannel` \| `youTubeSync` \| `youTubeFullSync` \| `retryDownloads` \| `refreshAll` (default: every task), `size` (default 20, 1–100), `before` = the previous page's `nextCursor` (the id of its last run); 400 otherwise; logged in only, 401 otherwise |
| `GET /api/admin/runs/{id}` | `RunResponse` | 404 if unknown; logged in only, 401 otherwise |
| `GET /api/admin/runs/{id}/log` | `RunLogResponse`: `lines` (oldest first: `id`, `at`, `level` = `information` \| `warning` \| `error`, `message`), `complete` (true: no line will come after these, the run is over; while false, ask again with `after` = the last line's `id`). A run from before TonTuyau 1.4 has no line, and is complete | `after` (default 0: from the first line), `size` (default 500, 1–2000); 400 otherwise; 404 if the run is unknown; logged in only, 401 otherwise |
| `POST /api/admin/tasks/{task}/runs` | 202 and the `RunResponse` of the run just asked for (v2.1: `queued`, it starts in the backend's background when its turn comes, at once when nothing else runs; `Location`: `/api/admin/runs/{id}`), `trigger` `admin`, `startedBy` the account. `import` (v2.0) adds what TubeArchivist has and TonTuyau doesn't; the refreshes (v1.10) read their `target` on YouTube | `task` = `import` \| `refreshVideo` \| `refreshChannel` \| `refreshChannelVideos` \| `downloadVideo` (v2.2) \| `addChannel` (v2.3) \| `youTubeSync` \| `youTubeFullSync` \| `retryDownloads` (v2.4) \| `refreshAll` (v2.7), 400 otherwise (`indexRebuild` too: the command line only); `target` (query) = a video's id for `refreshVideo`, a channel's for the two others, a video's id or a YouTube link to it for `downloadVideo` (v2.2), a channel's id, `@handle` or a YouTube link to either for `addChannel` (v2.3), required for them; for `youTubeFullSync` (v2.4) a subscribed channel's id, optional (that channel alone); for `retryDownloads` (v2.4) a channel's id, required; refused (400) for the other tasks; `days` (query, v2.7, `refreshAll` only, 0 to 365, 7 when absent; 400 otherwise); `tabs` (query, v2.3, `addChannel` only, 400 otherwise) = `videos,shorts,streams` or some of them, when absent the YouTube settings' `defaultTabs` (v2.5; before: the three); no body. 409 (v2.1: not when a run is going on, the run is queued) for `import` without TubeArchivist's settings, a `target` that isn't one of the library, `refreshVideo` on a video gone from YouTube (v1.16: it isn't refreshed any more), `downloadVideo` on a video of the library that has its file (v2.2), or `addChannel` on a channel TonTuyau is subscribed to (v2.3: its admin page changes its tabs); logged in only, 401 otherwise |
| `POST /api/admin/runs/{id}/cancel` | 202 and the `RunResponse` of the run asked to stop (it ends `canceled` soon after, between two requests; its log says who asked). A run on hold (v1.14) is `canceled` at once, and won't resume; a queued run (v2.1) too, and won't start | no body. 404 if unknown; 409 when it's over, goes on in another process (the command line: stop it there), or, on hold, while another run holds the lock; logged in only, 401 otherwise |
| `POST /api/admin/runs/{id}/hold` | 202 and the `RunResponse` of the refresh asked to go on hold (v1.15): it stops at once (the item it was reading is read again when it resumes), lets go of the lock, and ends `onHold` with no `resumeAt`: only resume goes on with it | no body. 404 if unknown; 409 when it isn't a refresh, isn't going on, or goes on in another process; logged in only, 401 otherwise |
| `POST /api/admin/runs/{id}/resume` | 202 and the `RunResponse` of the run on hold resumed now (v1.14): the same run goes on where it stopped, `running`, its notes say who resumed it | no body. 404 if unknown; 409 when it isn't on hold, or a run is going on (in any process); logged in only, 401 otherwise |
| `GET /api/admin/api-keys` | `ApiKeyResponse[]`, the latest created first: `id`, `name`, `prefix` (the secret's first 7 characters, `tt_` and 4: to tell keys apart), `kind` = `browserExtension`, `enabled`, `createdAt`, `createdBy` (the account), `lastUsedAt` (to the minute; null if never used). Never the secret | none; an admin's session only, 401 otherwise (403 for a key) |
| `POST /api/admin/api-keys` | 201 and `{ "key": ApiKeyResponse, "secret": "tt_…" }`: the secret, `tt_` and 40 letters or digits, is given this once (only its hash is kept) | JSON body `{ "name", "kind" }` (4 KB at most, 415 if not JSON): `name` 1 to 60 characters once trimmed, no control characters; `kind` = `browserExtension`; 400 otherwise; 409 when a key has the name already, whatever the case; an admin's session only |
| `POST /api/admin/api-keys/{id}/enable`, `/disable` | `ApiKeyResponse`: a disabled key is refused from its next request on | no body. 404 if unknown; an admin's session only |
| `DELETE /api/admin/api-keys/{id}` | 204: the key is gone for good | 404 if unknown; an admin's session only |
| `GET /api/extension/youtube-cookie` | `{ "cookie": YouTubeCookieResponse \| null }`: the cookie TonTuyau signs in to YouTube with, never its content: `receivedAt`, `receivedBy` (`"<key name> (API key)"` or the account), `state` = `works` (the account's Liked videos could be read with it) \| `refused` (signed out, expired…) \| `unchecked` (not checked, or the check couldn't tell: a bot check, the network), `checkedAt`, `reason` (what YouTube said, unless it works) | a `browserExtension` key, or an admin's session |
| `PUT /api/extension/youtube-cookie` | `YouTubeCookieResponse`: the cookie sent replaces the one there was, and is checked at once (a few seconds) | JSON body `{ "cookies": "<a Netscape cookie file: 7 fields a line, by tabs>" }` (128 KB at most): only its `youtube.com` cookies are kept; 400 about `cookies` when it holds none; a `browserExtension` key, or an admin's session |
| `GET /api/admin/youtube` | `YouTubeResponse`: `cookie` (a `YouTubeCookieResponse`, or null), `ytdlp`, `deno` (the versions in the image; null when one can't be run) | none; an admin's session only |
| `POST /api/admin/youtube/cookie/check` | `YouTubeCookieResponse`: the cookie tried against YouTube again | no body. 404 when there's none; an admin's session only |
| `DELETE /api/admin/youtube/cookie` | 204: YouTube is read signed out until the extension sends another | 404 when there's none; an admin's session only |
| `GET /api/admin/youtube/ytdlp` | `YtdlpStateResponse`: `choice` (`kind` = `latest` (the latest official release, kept up to date: the default) \| `official` \| `nightly`, `version` (null for `latest`), `chosenBy`, `chosenAt`), `running` (the version running now; null when yt-dlp can't be run), `bundled` (whether that's the image's copy), `waiting` (a version ready, waiting for the task going on to end; or null), `checkedAt`, `error` (why the last check or install failed; or null) | none; an admin's session only |
| `GET /api/admin/youtube/ytdlp/versions` | `{ "official": [ { "version", "publishedAt" } ], "nightly": [ … ] }`, the latest first: every official release with a Linux program, the nightly builds of the last 2 months (from GitHub, kept an hour) | none; 502 `github-unreachable` when GitHub can't be reached; an admin's session only |
| `PUT /api/admin/youtube/ytdlp` | 202 and the `YtdlpStateResponse`: the version is downloaded, checked against its release's checksums and tried in the background, then used from the next task | JSON body `{ "kind", "version" }`: `kind` = `latest` \| `official` \| `nightly`; `version` one of the versions offered for that kind (none for `latest`); 400 otherwise; 502 when GitHub can't be reached; an admin's session only |
| `GET /api/admin/youtube/settings` | `YouTubeSettingsResponse`: `comments` (which comments a refresh reads with each video: `off`, the default; `top`, YouTube's top ones, answers included, up to `commentsMax`; `all`), `commentsMax` (500 by default, kept while another choice is made), `pauseSeconds` (v1.14: the pause between two videos of a refresh, about this many seconds, from half to one and a half; 10 by default, 0 for none), `holdMinutes` (v1.14: how long a refresh YouTube refused stays on hold before it resumes by itself; 60 by default), `syncEveryHours` (v2.4: how often the incremental YouTube sync runs by itself; 6 by default, 0: by hand only), `downloadFormat` (v2.5: yt-dlp's format a download asks for, `bestvideo+bestaudio` by default), `defaultTabs` (v2.5: `videos`, `shorts`, `streams`, the tabs a channel added gets when none is named; the three by default), `changedBy`, `changedAt` (null while they're the defaults) (v1.11) | none; an admin's session only |
| `PUT /api/admin/youtube/settings` | the `YouTubeSettingsResponse` saved: applied from the next refresh (v1.11) | JSON `{ "comments": "top", "commentsMax": 500 }`: `comments` required, `commentsMax` 1 or more, `pauseSeconds` 0 or more, `holdMinutes` 1 or more, `syncEveryHours` (v2.4) 0 to 168, `downloadFormat` (v2.5) one line of 1 to 200 characters once trimmed, `defaultTabs` (v2.5) `{ "videos", "shorts", "streams" }` each `true` or `false`, at least one `true` (each absent: the one kept); 400 otherwise; an admin's session only |
| `GET /api/extension/connection` | `{ "version": "1.5.0", "name": "Firefox, laptop (API key)", "canAdd": true }`: the browser extension's connection test (TonTuyau's version, and as whom; v2.6: `canAdd`, it takes "Add to TonTuyau") | a `browserExtension` key, or an admin's session; 401 otherwise |
| `GET /api/extension/videos/{id}` | `PumpItemResponse` (v2.6): `state` = `absent` \| `fileMissing` \| `waiting` \| `inTonTuyau`, `runId` (while `waiting`: the run adding it; null otherwise) | `id` a YouTube video id, 400 otherwise; a `browserExtension` key, or an admin's session; 401 otherwise |
| `POST /api/extension/videos/{id}` | 202 and the `PumpItemResponse`, `waiting` with its `runId` (v2.6): the video downloaded through the queue, as `downloadVideo` (`trigger` `extension`, `startedBy` the key); one already waiting: its run | no body; 400 when `id` isn't a video id, 409 when TonTuyau has it with its file; a `browserExtension` key, or an admin's session; 401 otherwise |
| `GET /api/extension/channels/{id}` | `PumpItemResponse` (v2.6): `inTonTuyau` once TonTuyau is subscribed to it | `id` a YouTube channel id (`UC…`), 400 otherwise; a `browserExtension` key, or an admin's session; 401 otherwise |
| `POST /api/extension/channels/{id}` | 202 and the `PumpItemResponse`, `waiting` with its `runId` (v2.6): subscribed to with the YouTube settings' `defaultTabs`, added when it's new, as `addChannel`; one already waiting: its run | no body; 400 when `id` isn't a channel id, 409 when TonTuyau is subscribed to it; a `browserExtension` key, or an admin's session; 401 otherwise |
| `GET /media/{id}` | the original file, `Accept-Ranges: bytes` (seeking), right `Content-Type` | 404 if unknown or not on the share; 416 on an unsatisfiable range |
| `GET /media/{id}/hls/{session}/index.m3u8` | (v2.9) the HLS playlist (version 7, VOD) of the video's parts, keyframe to keyframe, about 6 s each, after the init segment `init.mp4` (`#EXT-X-MAP`); the keyframe times are read from the file's own index (Matroska Cues, MP4 `stss`) at its first play or download, and kept; `Cache-Control: private, no-cache` | `session` = 16 to 64 letters, digits, `-` or `_` (the viewing's: one ffmpeg each), 400 otherwise; 404 as above; 409 when the file can't be played in parts (no index, a codec MP4 can't hold): play `GET /media/{id}` |
| `GET /media/{id}/hls/{session}/init.mp4` | (v2.9) the init segment (`ftyp`, `moov`), the same for every viewing of the file | 400, 404, 409 as above |
| `GET /media/{id}/hls/{session}/{n}.m4s` | (v2.9) part `n` (from 0): fragmented MP4 (`moof`, `mdat`) copied out of the file by ffmpeg when asked for (no re-encoding, nothing written to disk), the file's own timestamps; one ffmpeg per viewing goes on with the next parts, at most 2 ahead of the furthest asked for; a part far from it (a seek) starts it again from there; at most 4 viewings copying at once (past it, the least recently asked stops, and starts again when asked) | 400, 409 as above; 404 for a part the playlist hasn't |
| `DELETE /media/{id}/hls/{session}` | (v2.9) 204: the viewer left, its ffmpeg stops now (else after a minute without anything asked) | 400 as above |
| `GET /media/{id}/download` | the same file as an attachment: `Content-Disposition: attachment; filename="<title>.<ext>"; filename*=UTF-8''<title>.<ext>` (characters forbidden in file names replaced, Q5-3) | 404 as above |
| `GET /art/videos/{id}` | the thumbnail (JPEG/PNG/WebP as copied) with `Cache-Control` + `ETag` | 404 if none |
| `GET /art/videos/{id}/{width}` | a resized thumbnail (WebP), same caching | `width` = `360` \| `720`; 404 if not made (yet) or any other width |
| `GET /art/channels/{id}/{kind}` | `kind` = `thumb` \| `banner` | 404 if none (any other kind too) |
| `GET /art/channels/{id}/{kind}/{width}` | a resized channel picture or banner (WebP), same caching | `thumb`: `48` \| `96` \| `176` \| `320`; `banner`: `1280`; 404 otherwise or if not made (yet) |

`/api/search` answers, depending on `kind`:
- `videos`: page of `VideoCard`
- `channels`: page of `ChannelCard`
- `all`: `{ "channels": ChannelCard[] (up to 3 best matches on the first page; `[]` on later pages, never absent), "videos": page of VideoCard }`

Search matches title, channel name, tags and description (French analysis with accent folding + English sub-field, M2), title boosted. Every word must be found (M4-Q1); the last word, unless the text ends with a space, may also be the start of a word in any of these fields ("creme b" finds "Crème brûlée"; "onct" finds a description saying "onctueux"; QA10 M5-1, QA11 M5-13). In one field a whole-word match ranks above a start-of-word one; across fields the weights stay: title, then channel name, then tags, then description.

## Not in v1

Playlists, subtitles, comments, SponsorBlock, related videos, watch state, sync status over HTTP.
