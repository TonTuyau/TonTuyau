# TonTuyau — REST API contract (v1.6)

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

## Rules

- **Read-only** library: every library endpoint is `GET` (and `HEAD` for media/art). No sync endpoint: the sync is CLI-only (Q5-4). The only `POST`s are logging in and out (v1.5), JSON only.
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
`type`: `video` | `short` | `stream`. `active: false` = unavailable on YouTube (TubeArchivist can no longer reach it), still archived ("Unavailable on YouTube" badge, R2-9). `thumbnailUrl` / `channel.thumbnailUrl` may be `null` when there's no image (then `thumbnails` is `[]`).

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
| `GET /api/channels` | page of `ChannelCard` | `q` (name, as-you-type, up to 100 characters), `sort` = `name` (default) \| `subscribers`, `cursor`, `size` |
| `GET /api/channels/{id}` | `ChannelDetail` | 404 if unknown |
| `GET /api/channels/{id}/videos` | page of `VideoCard` | `type` (default `video`), `sort` = `latest` \| `popular` \| `oldest`, `exclude` (a video id left out of the list and of `total`: the one being watched, for "More from <channel>"), `cursor`, `size` |
| `GET /api/search` | see below | `q` (required, 1–200 chars), `kind` = `all` (default) \| `videos` \| `channels`, filters for videos: `type`, `channel` (id), `published` = `today` \| `week` \| `month` \| `year` (published within the last 24 h / 7 / 30 / 365 days, computed by the backend on the first page and kept in the cursor so later pages use the same cut-off), `duration` = `short` (< 4 min) \| `medium` (4–20 min) \| `long` (> 20 min), `sort` = `relevance` (default) \| `latest` \| `popular`, `cursor`, `size` |
| `GET /api/search/suggest` | `{ "suggestions": Suggestion[] }` | `q` (required, 1–100 chars), `size` (default 8, max 20): **at most 3 channels** first, then video titles fill the rest, by prefix (as-you-type, accents ignored) |
| `GET /api/auth/me` | `{ "username": "admin" }`, or 401 when nobody is logged in | none: the session cookie says who |
| `POST /api/auth/login` | 204 and the session cookie (HttpOnly, SameSite=Strict, Secure): 8 hours renewed while in use, 30 days with `remember` | JSON body `{ "username", "password", "remember" }` (4 KB at most, 415 if not JSON); 401 with the same message for every refusal; 429 after 5 attempts in a minute from one address |
| `POST /api/auth/logout` | 204, the cookie deleted | no body |
| `GET /api/admin/dashboard` | `DashboardResponse`: `lastSync` (when the latest successful sync finished, or null), `totals` (`channels`, `videos`, `shorts`, `streams`, `gone`: videos no longer available on YouTube), `playTime` (`totalSeconds`, and per type `count`, `seconds`, `share` from 0 to 1, `averageSeconds`), `channels` (every channel, biggest first: `id`, `name`, `thumbnailUrl`, `thumbnails`, `counts`, `gone`, `total`) | none; logged in only (the session cookie), 401 otherwise |
| `GET /api/admin/dashboard/imports` | `ImportHistoryResponse`: the new videos of the finished sync runs of the period (never an index rebuild): `step` = `run` \| `day` \| `week` (from Monday) \| `month`, `timeZone` (the sync's, `TONTUYAU_TZ`: the days, weeks and months are its own), `total`, `bars` (oldest first, every step of the period, empty ones too: `start` = when the step starts, midnight in that zone, or when the run started; `total`; `byType` as `VideoCounts`; `notRecorded`: the new videos of runs of 1.1, which didn't record the types) | `period` = `day` (a bar per run of the last 24 h) \| `week` (a bar per day, the last 7 days) \| `month` (default: per day, the last 30 days) \| `year` (per week, the weeks of the last 365 days) \| `all` (per month, since the first import); 400 otherwise; logged in only, 401 otherwise |
| `GET /media/{id}` | the original file, `Accept-Ranges: bytes` (seeking), right `Content-Type` | 404 if unknown or not on the share; 416 on an unsatisfiable range |
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
