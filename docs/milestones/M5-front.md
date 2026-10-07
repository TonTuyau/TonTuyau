# M5: The real front (browse + search)

Branch: `milestone/m5-front` (from `main` at `a7d354d`, `main` merged again at `64062c3` for contract v1.4) · Features: `feature/m5-real-api`, `feature/m5-states`, `feature/m5-player`, `feature/m5-real-data`, `feature/m5-docs`, then `feature/m5-resized-art` (see [Resized art](#resized-art-qa09-contract-v14))

Spec: the approved M5a mockup ([M5a-front-mockup.md](M5a-front-mockup.md), [M5a-review.md](M5a-review.md)), [API_CONTRACT.md](../API_CONTRACT.md) v1.3, your M5 additions in [QA_SESSION_06.md](../QA_SESSION_06.md) (player that fits the video, small / large player), [QA_SESSION_08.md](../QA_SESSION_08.md). Resized art (QA09, contract v1.4) comes after this pass, on its own branch.

Open questions for you: [QA_SESSION_10.md](../QA_SESSION_10.md). Choices I made without asking are marked **(to confirm)** below; each one is also a question there.

## Scope of this pass

1. The app on the M4 backend instead of the mock (the mock kept for `dev:mockup`, tests and offline work).
2. Real loading, error, not-found, empty and network-down states on every page.
3. The player: fits the video's shape, small / large player, the M5a codec rules kept.
4. A click-through on the real library (trial index), with fixes.

Not in this pass: the production Node server (M7), generated API types (QA10). Resized art (QA09) came right after, on its own branch: [Resized art](#resized-art-qa09-contract-v14).

## What was built

**Real API (`feature/m5-real-api`)**
- **The mockup's client already was the real one.** `src/lib/api/client.ts` always called `/api/...` on the page's origin; `pnpm run dev` proxies `/api`, `/media`, `/art` to `TONTUYAU_BACKEND_URL` (default `http://localhost:5080`), `pnpm run dev:mockup` answers them from the mock. No code path depends on the mode. Clicking through the real library found no page that only worked with the mock.
- **Where the mock differed from the backend**, now fixed in the mock:
  - A malformed id (`/api/videos/nope`, `/api/channels/UCnope`, `exclude=x`, `channel=nope`) answers **400**, like the backend (M4-Q7); it was a 404. Three synthetic channel ids from M5a aren't in YouTube's format (`UCmock-cuisine-margaux0`, `UCmock-atelier-bois-fer`, `UCmock-chroniques-eleonore`); the mock still accepts them so the M5a review URLs keep working.
  - Text limits are measured on the trimmed text, and `/api/channels?q=` takes at most 100 characters, like the backend (the contract gives no limit for that `q`: QA10).
- **Text boxes can't hold more than the API takes** (`src/lib/api/limits.ts`): the top search box and "Search this channel" take 200 characters, the channel filters 100; no suggestion request is sent above 100 characters (the search itself still works).
- **Contract-conformance test** (`src/lib/api/contract.test.ts`): it drives the whole app (every route, with valid, default and hand-edited unknown values, the "Show more" paging, the top search box, the search page's channel filter) against a fake backend that records every request. Each request is checked against a table of the contract: the endpoint exists, every parameter is one the endpoint takes, enum values are allowed ones, sizes are in range, ids have the right format, text is trimmed and within its limit, nothing is sent empty or twice, cursors are exactly ones the API sent, and `kind=channels` carries no video filter and no sort. The table itself is checked against `docs/API_CONTRACT.md` (every endpoint, parameter and value it lists must be in the document), and the test fails if an endpoint of the table was never exercised. Mutation check: sending the video filters with `kind=channels` is caught (4 violations reported).

**States (`feature/m5-states`)**
- **What a failure means** (`src/lib/errors.ts`), from the HTTP status and whether the answer was the backend's own ProblemDetails:

| Case | Title (EN / FR) | Retry |
|---|---|---|
| network error, browser offline | You're offline / Vous êtes hors ligne | yes, and by itself when the browser is back online |
| network error, browser online; or a bare 5xx without ProblemDetails (Vite's proxy answers an empty 502 when the backend is down) | Can't reach the TonTuyau server / Impossible de joindre le serveur TonTuyau | yes, and by itself on `online` |
| 400 | This request isn't valid / Cette requête n'est pas valide | no |
| 404 | Not found / Introuvable | no |
| 500 (ProblemDetails) | The server ran into a problem / Le serveur a rencontré un problème | yes |
| anything else | Something went wrong / Une erreur s'est produite | yes |

  Each has a translated hint, and the backend's ProblemDetails stays visible as a small technical line ("Error 500 · Elasticsearch is not reachable."). **(to confirm: the "bare 5xx = server unreachable" rule and the automatic retry)**
- **Not-found pages.** Unknown routes keep the not-found page. On the watch, shorts and channel pages, an unknown id (404) shows "This video isn't available" / "This channel isn't available" and a malformed one (400) "This video link isn't valid" / "This channel link isn't valid", each with a hint, the tab title, a "Go to Home" button and no Retry. **(to confirm: two wordings)**
- **Empty states** were all there already (Home, feeds, Channels, a channel without videos, an empty tab, search without results); new: "No other videos from this channel" beside a video, and the search page's channel filter says "Couldn't load the channels" when its request fails instead of "No channel found".
- **Mock scenarios** on `/__mock`: `down` (a bare 502, what the dev proxy answers when the backend is stopped) and `offline` (the connection is dropped).

**Player (`feature/m5-player`)**
- **It fits the video's shape.** The box takes the ratio of the API's video track (`VideoDetail.tracks`), so vertical, square, 4:3 and cinema-wide videos have no black bars (`src/lib/videoRatio.ts`). Without a sized video track it starts 16:9 (9:16 for a short) and takes the file's own `videoWidth` / `videoHeight` once its metadata has loaded. When both exist, the API wins **(to confirm, QA10)**. The box is as wide as the column allows without being taller than a cap, so a vertical video gets narrower instead of taller: the cap leaves room for the title in the small player and takes the viewport in the large one (`--player-max-height-small`, `--player-max-height-large` in the theme). Messages (file unavailable, can't play) keep the plain 16:9 box. The shorts player's frame takes the short's shape too (34 real shorts are 2:3, 8 are square).
- **Small / large player**, like YouTube's default and theater views: a button in the top-right corner of the player (shown on hover or focus, always on touch screens), remembered in `localStorage` (`tontuyau.playerSize`, default small). Large puts the player across the page with the title and the channel's other videos below. Below 1312 px the watch page is one column and both sizes look the same, so the button isn't shown **(to confirm)**.
- **A file the API lists but the share doesn't have** (`mediaAvailable: true`, 404 when played): the media element can't tell a missing file from a decode failure, so on its `error` event the player asks the server (`HEAD` on the API's `mediaUrl`). A 404 shows "File unavailable" with "The video file can't be found on the share right now.", and the watch and shorts pages hide their download links; anything else keeps "The browser couldn't play this file" + Download.
- The M5a codec rules are unchanged: "probably" plays; "maybe" plays with the notice and the download; "" shows the message and the download.
- **Mock**: 4:3 and 2.35:1 test patterns, picked from the video track; new edge cases `mock4by3000`, `mockVertic0`, `mockWide000`, `mockNoVTrk0` (no video track), `mockGoneFil` (404 when played). The earlier fixtures are unchanged (the generator appends).

**Real-data fixes (`feature/m5-real-data`)**
- Real videos carry up to 76 tags: they pushed the description five lines down. The watch page now shows one line of tags while the description is collapsed and all of them once expanded **(to confirm)**.
- Codec names for E-AC-3 and AC-3 (7 real files), Vorbis and FLAC in the can't-play message and the file details.

## How to check it

```bash
cd frontend
PORT=5180 pnpm run dev:mockup       # mock API, as in M5a (see "Port note" below)
pnpm run dev                        # real mode: proxies /api /media /art to TONTUYAU_BACKEND_URL (default :5080)
pnpm run lint && pnpm run check && pnpm run test && pnpm run build
../scripts/check.sh
```

New mockup URLs (the M5a ones all still work):

| What | URL |
|---|---|
| Vertical video (not a short), 4:3, 2.35:1 | `/watch?v=mockVertic0`, `/watch?v=mock4by3000`, `/watch?v=mockWide000` |
| No video track: starts 16:9, then the file's 4:3 | `/watch?v=mockNoVTrk0` |
| File listed but missing on the share | `/watch?v=mockGoneFil` |
| Small / large player | any watch page ≥ 1313 px wide: the button at the player's top right |
| Malformed id (400) / unknown id (404) | `/watch?v=nope`, `/watch?v=unknownVid1`, `/channel/UCnope`, `/channel/UCunknownunknownunknown1` |
| Backend down, network down | `/__mock?scenario=down`, `/__mock?scenario=offline` (then `/__mock?scenario=normal`) |

**Port note.** Your mockup server was started with `--port 5180`, but it listens on **5173**: with pnpm 12 the `--` is passed through to Vite, which then ignores the options after it. `PORT=5180 pnpm run dev:mockup` works. I left it running and used 5175 for the real-mode front.

## Real-data check

2026-10-04, the trial index (prefix `trial`, Elasticsearch 9.5.4 in `tontuyau-trial-es`), the backend API from this branch in Development (`TONTUYAU_SYNC_SCHEDULER_ENABLED=false`, placeholder TubeArchivist settings so nothing could reach it), art from `dev-data/trial/art`, no media share. Front: `pnpm run dev` on port 5175, Chromium 152, my own browser tab. TubeArchivist was never called; no sync was run.

| Check | Result |
|---|---|
| Home, feeds, Channels (filter, both sorts, paging), channel pages (3 tabs, sorts, "search this channel"), watch, shorts (↑ ↓), search (all kinds, every filter, chips, paging), suggestions | all work on real data; no console errors; no request outside the contract |
| Search speed (backend, 10 queries) | 16–120 ms; slowest "minecraft" (1,881 videos + 3 channels) 120 ms; suggestions 35–75 ms |
| Very long titles | the real maximum is 100 characters (YouTube's limit): 2 lines on cards, wraps on the watch page. The mock's `mockLongTtl` (250 characters) is harsher |
| Huge numbers | 481 M views, 12 M likes, 33.8 M subscribers: compact formatting holds ("481M views", "481 M de vues") |
| Missing images | none: every video and channel has its art (`thumbnailUrl` never `null` on Home or Channels). But the **Channels page loads 48 × 900×900 originals (~100 KB each)**: avatars visibly pop in. That's what QA09's resized art fixes |
| Codecs (44,979 videos) | video: H.264 21,845, AV1 16,086, VP9 7,044; audio: Opus 39,314, AAC 5,658, **E-AC-3 7**; 4 files have no video track (audio only). Chromium answers "probably" for every representative string the backend sends except E-AC-3 ("", "can't play" message + download): see QA10 |
| Shapes (video tracks) | videos: 37,101 × 16:9, 380 wider (up to 2.33:1), 229 × 4:3, 160 square, 123 in between, 65 vertical; shorts: 5,908 × 9:16, 34 × 2:3, 8 square; streams: 961 × 16:9, 2 vertical, 4 other. All now play without black bars |
| No media share | every video's file answers 404: the player shows "File unavailable" (HEAD check), downloads hidden, on watch and shorts |
| Channels with no shorts / no streams | 42 / 72 of 116: only the tabs with videos show; one channel (ARTE Info) has nothing: "This channel has no videos yet" |
| 400 / 404 / backend stopped | `/watch?v=nope` → "This video link isn't valid"; an unknown channel id → "This channel isn't available"; backend stopped → "Can't reach the TonTuyau server · Error 502", Retry brings Home back once it's up |
| Tags | up to 76 per video: fixed (one line while collapsed) |
| Search for a word still being typed | "creme b" shows 8 suggestions, but Enter finds **nothing**: the search needs whole words (M4-Q1). See QA10 |

Seen but left as is:
- Before play, the poster is the video's thumbnail, which YouTube makes 16:9 even for vertical and 4:3 videos, so the poster shows bars inside a vertical box until the video starts. `object-fit` would crop the video too.
- Shorts' thumbnails in search rows are 16:9 crops (that's what TubeArchivist stores).

## Tests

| Kind | What | Count |
|---|---|---|
| Vitest, contract | conformance of every request the pages make, table ↔ document, the checker's own refusals, text box limits | 4 |
| Vitest, states | `errorKind` matrix (statuses × ProblemDetails or not × online), retry rules, `missingResource`; `ErrorState` (server error + technical line + Retry, no Retry on 400, bare 502, offline + auto retry on `online`, no auto retry for a server error, French); watch / channel / shorts not-found (404), bad link (400), server error keeps Retry; "no other videos"; client `fromServer` (assertions added to existing tests) | 25 |
| Vitest, player | ratio from the API track (4 shapes), file metadata fallback, API wins over the file, plain box for messages, size button (both ways, absent without a handler or in shorts, French), missing file (HEAD 404 → unavailable without download, 206 or network failure → "couldn't play"); watch page small/large (toggle, saved, restored), download dropped on a missing file; shorts frame shape; `videoRatio`; the player size among the localStorage preferences (assertions added to the existing test) | 20 |
| Vitest, mock | malformed ids → 400, fixture ids valid (the three legacy ids listed), trimmed text limits; media sample per shape, missing-on-share | 6 |
| Vitest, theme | the new component is scanned for raw values like the others | 1 |

Total: **312 frontend tests** (256 at the start of M5), all passing; backend unchanged, **3,326**, all passing (`./scripts/check.sh` before each merge into the milestone branch). Lint, Prettier, `svelte-check --fail-on-warnings` and the build are clean.

## Decisions taken during the milestone

All marked **(to confirm)** above, and asked in [QA_SESSION_10.md](../QA_SESSION_10.md):
- a bare 5xx (no ProblemDetails) means "server unreachable"; a connection problem retries by itself when the browser is back online;
- a malformed id gets its own wording ("this link isn't valid"), separate from "isn't available";
- the API's ratio wins over the file's when both exist;
- no size button below 1312 px;
- one line of tags while the description is collapsed.

Also:
- The mock keeps the three M5a channel ids that aren't in YouTube's format (review URLs), and refuses every other malformed id with a 400.
- The front's text limits mirror the API's (`maxlength`), rather than trimming what the user typed.
- The player's missing-file check is a `HEAD` on the URL the API gave: asking the server about its own file, nothing built or guessed.

## Deviations from the plan

- **API types stay hand-written** (the plan says `openapi-typescript`): the conformance test checks what the front sends, the mock is typed by the same file, and generating them needs the backend's OpenAPI document at build time. QA10 asks.

## Known limitations / follow-ups

- Real playback of AV1 / VP9 / H.264 files wasn't possible here (no media share on the laptop); only `canPlayType` was checked on the real `mimeType`s. Worth a look on the NAS stack.
- jsdom has no layout: the player's shapes, the large layout, the one-line tags and phone widths were checked by hand in Chromium (mock and real data, 1440 px and 375 px), not by tests.
- The scroll position isn't restored after back / forward (unchanged from M5a).

## Resized art (QA09, contract v1.4)

Branch `feature/m5-resized-art`, after your QA09 answers (WebP q80; thumbnails 360 / 720 px; channel pictures 48 / 96 / 176 / 320 px; banners 1280 px; made by the sync whenever missing; contract v1.4 lists; SkiaSharp). Backend and front.

**Sync** (`TonTuyau.Application/Sync/SyncService.cs`, `TonTuyau.Infrastructure/Storage/SkiaArtResizer.cs`)
- After each page of videos or channels (and after art copied by a retry), the sync makes the missing versions of every image it handled, whether the original was just copied or already local, so the **next full run converts the existing art once** and every later run only handles new images. No new CLI command.
- Several images at once: `Parallel.ForEachAsync` bounded by the new setting **`TONTUYAU_ART_RESIZE_PARALLELISM`** (default: the machine's core count; 1–64; README and `.env.example` updated).
- SkiaSharp 4.153.1 with `SkiaSharp.NativeAssets.Linux.NoDependencies`. **One decode per image** for all its widths (the header alone when every version exists); Mitchell cubic sampler; WebP quality 80; the target has no colour space, so no ICC, EXIF or XMP chunk is written (tested by reading the WebP's RIFF chunks of a JPEG carrying EXIF). Height = `max(1, round(h × w′ / w))`, halves away from zero (`ArtSizes.ResizedHeight`). **Never upscaled**: only widths strictly below the original's (`ArtSizes.Targets`). Each file is written to a temp file and renamed.
- Versions sit next to their original: `videos/a/<id>.360.webp`, `channels/<id>_thumb.48.webp`. Deleting an image (the sync's deletions) deletes its versions; TV art gets none.
- A failed resize (an image that can't be decoded, a write that fails) is **logged as an error and never stops the run**; the versions that exist are still recorded, the rest is tried again next run.
- The run report counts **versions made / failed** (`SyncCounts.ArtResized`, `ArtResizeFailed`), shown by `tontuyau sync status` next to art copied / failed.

**Store**: the sync records, per image, the original's width and the widths that exist, in the documents (`thumbnail_art` on videos; `thumb_art`, `banner_art` on channels; `{ "width": 1280, "sizes": [360, 720] }`). They're bookkeeping, like `seen_in_run`: not in the content hash, kept when the content is rewritten, and left out of the JSON when unset. The index definitions changed (new fields, `"type": "object", "enabled": false`), so their hash changed: after upgrading, the indices need a rebuild (the M2 versioned-index mechanism: a full run into a new generation, then the alias switch). Since the QA10 round **it happens by itself at startup** ([below](#automatic-index-rebuild-qa10-m5-10)); meanwhile the API describes the images from the disk (see below).

**API**
- Cards and pages list each image's versions as the contract says: `thumbnails` on `VideoCard` / `VideoDetail` and on every embedded channel, `thumbnails` on `ChannelCard` / `ChannelDetail`, `banners` on `ChannelDetail`; sorted by width, the original last with its real width; `[]` without an image.
- The lists come from the store: **no disk access per card** once the sync has recorded the images. An image not recorded yet (an index from before resized art, i.e. until the rebuild) is described from the art store (the original's header + which version files exist) **(to confirm: this transition fallback)**. Measured on the trial index (not rebuilt, so every card took the fallback): `/api/channels?size=100` and `/api/videos?size=100` 50 ms warm.
- `GET /art/videos/{id}/{width}` (360, 720) and `GET /art/channels/{id}/{kind}/{width}` (thumb 48 / 96 / 176 / 320, banner 1280): `image/webp`, same `Cache-Control` + `ETag` + 304 as the originals. Any other width (`361`, `0360`, `abc`, a thumb width on a banner…) or a version not made (yet) is a 404; a malformed id is a 400 first, as before.

**Front**: `src/lib/srcset.ts` joins each list into `srcset` (`url 360w, …`) and gives each layout its `sizes`: grid cards `(max-width: 600px) 100vw, 400px`, side column `168px`, search rows `(max-width: 600px) 100vw, 500px`, shorts cards `(max-width: 600px) 50vw, 200px`, avatars their size (24 / 36 / 88 / 160 px), banner `(max-width: 1284px) 100vw, 1284px`. `src` stays the original (the fallback); no list, no `srcset`. The first row keeps `loading="eager"` + `fetchpriority="high"`. The player's poster stays the original. **Mock**: fixtures carry the lists (the mock draws its art at any width, so every listed version exists), the real-channels layer serves its originals for each listed width, any other width is a 404 like the backend; new edge case `mockOrigOnl` (versions not made yet: only the original listed).

**Real data** (trial art, 4.3 GB of originals: 44,979 thumbnails, 116 channel pictures, 116 banners), through the sync's own `IArtResizer` (a small harness with TonTuyau's services; no TubeArchivist, no Elasticsearch), 6 threads, laptop shared with other work:

| | Result |
|---|---|
| Versions made | **82,032** (44,977 × 360 px, 36,475 × 720 px, 116 × each channel size, 116 banners), **0 failed** |
| Not made (original too narrow) | 8,506: 8,502 thumbnails of 640 / 480 px have no 720 px version, 2 thumbnails ≤ 360 px have none |
| Time | **417 s (7 min)**; a second pass with nothing to make: **1.8 s** |
| Peak memory | 164 MB |
| Disk | **+1.97 GB** (4.29 → 6.26 GB): 360 px 14 KB avg (637 MB), 720 px 36 KB avg (1.33 GB), channel pictures 1 / 2.4 / 5.3 / 11.3 KB, banners 32 KB |
| In the browser (1440 px, pixel ratio 1) | Channels page: the 24 avatars loaded weigh **51 KB instead of 1,317 KB** (96 px versions); Home's first row: **139 KB instead of 441 KB** (720 px versions for 400 px cards). The browser picks per pixel ratio: at 2×, 400 px cards take the 1280 px original |
| Edges | no light line on the bottom row (checked by eye on samples, and by a test on solid images of 1280/1281 × 720/718/423) |

The versions were written into `dev-data/trial/art` (local, git-ignored). The trial index wasn't rebuilt (that needs TubeArchivist): the API described the images from the disk, as it would before an upgrade's rebuild.

**Tests**

| Kind | What | Count |
|---|---|---|
| Unit, Application | `ArtSizes`: never at or above the original (16 widths × 3 subjects), the targets per original width, sorted / once / positive, QA09's sizes, height cases (rounding, halves, minimum 1), height within half a pixel (7 × 8 × 7), positive sizes; `LibraryPaths.OwnerOf`; `CardBuilder` (recorded used without the disk, fallback, none, non-art paths); the parallelism setting (bounds, range, read from its variable, default) | +485 |
| Unit, Infrastructure | versions next to their originals, path rules, 360 + 720 px WebP with the right size, never upscaled (4 widths), existing versions kept, no original, not an image (missing count, nothing left behind), bottom row and right column not blended (2 × 3 sizes), no metadata, no temp file, `Describe`, delete removes versions only, parallel use; art records are bookkeeping (JSON, hash), mapping both ways | +24 |
| Unit, Api | sizes listed smallest first, original last, URLs; no image = `[]`; nothing at or above the original; banner versions; JSON field order with `thumbnails` | +6 |
| Reqnroll | `Sync.feature`: versions of every image made and recorded (66), never at or above the original (480 / 360 / 120 px), existing art converted at the next full run and only once, an image that isn't one (2 failed, run succeeds, the rest recorded), art copied by a retry; `Media.feature`: WebP + caching, 304, 5 channel sizes, 9 widths that don't exist, not made yet, narrower original, malformed ids; `Browse.feature`: recorded versions on the watch page, in lists, the disk fallback, a channel's banners, field lists | +29 |
| Vitest | `srcset` / `sizes` (join, empty, every layout), cards (3 layouts, avatar, first row eager, no list), short card, channel banner + avatar; mock lists, edge cases, sized art | +14 |

Totals after this branch: **3,870 backend tests** (3,326 before: +544) and **326 frontend tests** (312 before), all passing (`./scripts/check.sh`).

The real JPEGs that the integration tests now need (the sync resizes them) are made by `Support/TestImages.cs`; the fake TubeArchivist serves them and can serve a narrower image or a broken one. **Found on the way**: the Testcontainers Elasticsearch node reached its default limit of 1,000 shards (every scenario keeps its 4 indices, and the new scenarios crossed ~250): its `cluster.max_shards_per_node` is now 4,000.

**Mutation checks** (work committed first; each mutation built, the relevant suites run, then restored with `git checkout`)

| # | Mutation | Caught by |
|---|---|---|
| 1 | A version at the original's width is allowed (`<=`) | 8 Application + 2 Infrastructure tests |
| 2 | No width filter (upscaling) | 36 Application + 5 Infrastructure tests |
| 3 | Height truncated instead of rounded | 120 Application tests + "360 and 720 px … same shape" |
| 4 | Height rounded up (ceiling) | 77 Application tests (the height matrix). The edge test doesn't catch it: Skia stretches the whole source into the target, so one extra row isn't a visible line |
| 5 | The API lists widths at or above the original | `Sizes_are_sorted_with_the_original_last_and_nothing_at_or_above_it` |
| 6 | The sync doesn't record what it made | 5 `Sync.feature` scenarios |
| 7 | Art copied by a retry isn't resized | "Art copied by a retry gets its versions too" |
| 8 | The sized art endpoint takes any width at or above an allowed one | 8 `Media.feature` cases |

**Decisions (to confirm)**, also in [QA_SESSION_10.md](../QA_SESSION_10.md):
- the disk fallback for images the sync hasn't recorded yet (only until the rebuild after upgrading);
- `sizes` uses the cards' upper bound (400 px), so a 300–360 px card at pixel ratio 1 still takes the 720 px version.

Everything else follows QA09 as answered. Contract: no change (v1.4 as written on `main`).

## QA10 round

Your answers in [QA_SESSION_10.md](../QA_SESSION_10.md). Kept as built: M5-2 a) (raw E-AC-3 name), M5-5 a) to M5-9 a), M5-11 a). Questions from this round: [QA_SESSION_11.md](../QA_SESSION_11.md).

### Search: the last word as the start of a word (M5-1 b), `feature/m5-search-last-word-prefix`)

- The last word of a search, **unless the text ends with a space**, matches either as a whole word in any field (as before) or as the start of a word in a title or a channel name ("creme b" finds "Crème brûlée"; "chocol" finds "Mousse au chocolat"). The other words stay whole words, and every word must still be found (AND, M4-Q1), each in any field. Accents and case are folded. Channel searches (`kind=channels`, the channels above `kind=all`) do the same on names.
- How: `SearchText` (Application, `Store/StoreQueries.cs`) splits the text into whole words and the word being typed; `SearchTextQueries` (Infrastructure) builds a `cross_fields` AND query for the whole words and, for the last word, a `bool` of the same whole-word query OR a `match` on the `.autocomplete` sub-fields (the M2 edge n-grams, weight ×0.5 of the field's). A text with nothing searchable (`-`, `!`) matches nothing.
- **Relevance**: a whole word is also one of its own n-grams, so a whole-word match scores the whole-word clause *and* the start-of-word one: it always ranks above a start-only match in the same field (scenario "Tarte aux pommes" above "Tartelettes au citron", the latter being 90,000 times more viewed).
- Cursors: the fingerprint tells "creme b" (typing) from "creme b " (complete): a cursor of one can't continue the other.
- The front trims the search box, so from the UI the last word is always "being typed" (QA11).
- Found while testing: a field weight written `field^3` is refused inside a `match` query (it only works in `multi_match`): the weights went on the queries instead (the unit test that reads the query caught it).
- **Contract** (M5-3 a), same branch): `/api/channels` `q` "(name, as-you-type, up to 100 characters)", and the search paragraph says how the last word matches. Wording only, no version bump.

Tests: Reqnroll `Search.feature` +16 (prefixes with accents and case ×5, one word, other words stay whole, trailing spaces ×3, AND kept, whole word above start-of-word, paging with cursors, cursors of "c" and "c " don't mix, channels and `kind=all`, nothing searchable); unit: `SearchText` (17), query shapes (5), fingerprints. Mutations (work committed first):

| # | Mutation | Caught by |
|---|---|---|
| S1 | No start-of-word clause | 9 scenarios |
| S2 | A trailing space ignored (the last word always a prefix) | 4 scenarios |
| S3 | Every word matched as a prefix (`bool_prefix`) | 4 scenarios ("other words stay whole", trailing spaces) |
| S4 | No whole-word alternative for the last word | 12 scenarios (elisions, stemming, tags, …) |
| S5 | Start-of-word weighted ×20 instead of ×0.5 | **survives, by design**: the whole-word match also matches the start-of-word clause, so its lead doesn't depend on the weight (QA11 asks about matches in different fields) |

### API types generated from OpenAPI (M5-4 b), `feature/m5-openapi-types`)

- `frontend/src/lib/api/openapi.json`: a committed copy of `/openapi/v1.json`. `pnpm run api:types` makes `schema.d.ts` from it (`openapi-typescript` 7.13); `pnpm run api:openapi` copies the running backend's document first (`TONTUYAU_BACKEND_URL`), then regenerates.
- `src/lib/api/types.ts` is now names for the generated types (`VideoCard = components['schemas']['VideoCard']`…), and the parameter types (`VideoSort`, `SearchKind`, `VideoListParams`…) come from the document's `paths`. Front conveniences, marked: the generic `Page<T>` (checked equal to `PageOfVideoCard` / `PageOfChannelCard`), `cursor: null` on a first page, `q` required in `SearchParams`, `type` required for a channel's videos. The mock and the conformance test are typed through them.
- Checks: **Reqnroll** "The front's copy of the OpenAPI document is what the backend serves" (JSON-equal; on a difference it says how to refresh, and saves the served document); **Vitest** `schema.test.ts`: `schema.d.ts` is exactly what the generator makes from the copy; `types.test.ts`: compile-time checks of the names and enums (`pnpm run check` fails otherwise).
- **What the OpenAPI document was missing, fixed in the backend** (`TonTuyau.Api/OpenApi`):
  1. Query parameters had no allowed values (only descriptions): now enums for `type`, `sort` (per endpoint), `kind`, `published`, `duration`; `minimum` / `maximum` for `size` (20 for suggestions); `minLength` / `maxLength` for `q`; id `pattern`s for `exclude` and `channel` (`ParameterRules.cs`, from the validators' own `ApiValues` tables).
  2. Enums had no `"type": "string"`.
  3. ProblemDetails had `title` and `status` optional and nullable, the other members nullable, and no `traceId`: now `title` and `status` required, the rest optional strings, `traceId` listed.
  4. Every typed `200` also listed `text/plain` and `text/json`: JSON only now.
  5. `SearchAllResponse` had its nested shapes written inline (the ASP.NET Core 10 limitation of M4): its members now point to `ChannelCard` and `PageOfVideoCard`.
  6. The document carried the requesting host as `servers`: removed (relative URLs), so it reads the same from anywhere.
  Nullability (`thumbnailUrl`, `nextCursor`, track sizes) and the `ImageSize` lists were already right.
- Also on this branch: `SyncSchedulerTests.Runs_each_sync_when_it_is_due` failed once under load (it stepped a fake clock minute by minute and raced the scheduler's continuations): it now waits until the scheduler waits, then moves the clock to that exact time.

### Automatic index rebuild (M5-10 a), `feature/m5-auto-rebuild`)

Your choice: a) at startup. The disk fallback for images not recorded yet stays.
- **At startup** (`TonTuyau.Api/Hosting/IndexAutoRebuild.cs`, a background service): once Elasticsearch answers, it compares each live index's recorded definition hash with this version's (M2 mechanism). If one differs, it starts `index rebuild` in the background: startup isn't held up, readiness stays up, users read the current generation until the alias switch. The readiness check's description says "Index rebuild pending" / "running" (the default `/health/ready` answer only shows the status word: QA11).
- **It's a sync like the others**: `SyncService.RebuildAsync` (same lock, TubeArchivist read-only through the guard, format guard, deletion limits, every safety of a full run). Runs record their trigger: the new `startup`.
- **Fails or TubeArchivist unreachable**: logged as an error, users keep the old generation. **Every sync re-checks under the lock**: while the definitions differ, a scheduled or CLI `sync incremental|full` rebuilds instead (run note "Index rebuild instead of the incremental sync…", `SyncRun.IndexRebuild`), until a rebuild succeeds. A run already holding the lock when the backend starts did the check itself.
- **CLI**: `tontuyau sync incremental|full` prints "Index rebuild pending (the index definitions changed): this run rebuilt the indices instead of the sync asked for." before its report; `tontuyau sync status` prints "Index rebuild : pending …"; `tontuyau index rebuild` is unchanged.
- **Scheduler off** (`TONTUYAU_SYNC_SCHEDULER_ENABLED=false`): the startup rebuild is never started (so the review stack never calls TubeArchivist by itself); a warning says it's pending and how to run it. **No separate switch** (`TONTUYAU_INDEX_AUTO_REBUILD`): the scheduler setting already says "this backend may sync by itself", and one switch can't be set inconsistently. QA11 asks.
- README: a new "Upgrading" section ("after upgrading, the index rebuilds itself at startup").

**Two real bugs found by these scenarios, fixed on the branch:**
1. **The scheduler crashed on a schedule more than ~49 days away** (`Task.Delay` refuses longer waits: e.g. a yearly cron): the background service threw, which stops the host. It now waits at most a day at a time and looks again.
2. **An open circuit breaker escaped the sync**: after many TubeArchivist failures in a row, the HTTP resilience pipeline throws `BrokenCircuitException`, which the gateway didn't map: the run stayed "running" in the history, and a scheduled run would have stopped the scheduler (and the host). It's now a `TubeArchivistUnavailableException` like any unreachable TubeArchivist (unit test added).

Tests: Reqnroll `IndexAutoRebuild.feature` (6): matching hashes → nothing at startup; changed hash → startup rebuild, both indices on generation 2, data intact; TubeArchivist unreachable → failed startup rebuild, old generation kept, then the next scheduled sync rebuilds instead and the one after is a plain incremental run; scheduler off → nothing started; CLI sync while pending rebuilds and says so; status says pending. Unit: circuit-open mapping. Mutations (work committed first):

| # | Mutation | Caught by |
|---|---|---|
| R1 | The hash comparison ignored (never pending) | 3 scenarios |
| R2 | The hash comparison inverted | 5 scenarios |
| R3 | The scheduler-off rule removed | "With the scheduler off, the backend never starts the rebuild by itself" |

### Totals

After this round: **3,918 backend tests** (3,870 before: +48, of which Reqnroll 289 → 313) and **330 frontend tests** (326 before: `schema.test.ts`, `types.test.ts` ×3), all passing with `./scripts/check.sh` before each merge.

Also on the auto-rebuild branch: each Reqnroll scenario now deletes its own indices afterwards. The test Elasticsearch had accumulated ~1,200 indices over a run, and its cluster state became slow enough that a mapping update timed out (429) once; the run is also faster (3.5 min).

## QA11 round

Your answers in [QA_SESSION_11.md](../QA_SESSION_11.md): M5-12 a) (no separate switch), M5-14 a) (the title's weight wins), M5-15 a) kept as built. M5-13: **the word being typed matches the start of a word in descriptions and tags too**, `feature/m5-prefix-all-fields`.

- "creme" now finds a video whose description says "crémeux" (your example), "onct" one saying "onctueux", "desse" one tagged "dessert". The search box still trims (M5-13 a): from the UI the last word is always also a start of word); the API still treats a trailing space as "complete".
- **Weights** (unchanged, M5-14): start-of-word clauses keep each field's whole-word weight × 0.5: **title 3 > channel name 2 > tags 1.5 > description 1**. Tags sit between the channel name and the description, as for whole words. In one field a whole word still ranks above a start of word (it matches both clauses); a title start ranks above a description start.
- **Technique**: `index_prefixes` (1–5 characters) rather than edge n-grams or a plain `prefix` query. Descriptions get a sub-field `description.prefix` (folded, not stemmed: a start must match the written word, not its stem; `index_options: docs`, no norms, since a start-of-word match is constant-score); `tags` (already folded) gets `index_prefixes` itself. The query is a `match_bool_prefix` (analysed, so accents fold): a start of 1–5 characters is one term lookup in the indexed prefixes (profiled: `description.prefix._index_prefix:cr`, 0.3 ms for 9,329 documents), a longer one a term-dictionary prefix, which is selective at that length (`cremeu*`, 0.9 ms). Edge n-grams up to 20 characters on descriptions of up to 5,000 characters would have cost far more space; a plain `prefix` on "c" walks every term starting with c. Titles and names keep their `.autocomplete` n-grams. Same for channel searches (their tags and descriptions).
- The mappings changed (videos and channels): the indices **rebuild themselves at the next start** (M5-10).

**Real data** (no TubeArchivist): the trial documents copied with `_reindex` (23 s) on `tontuyau-trial-es` into temporary indices with the new mapping, and the backend of this branch pointed at them (scheduler off: it logged that a rebuild was pending and called nothing). Then deleted, container stopped.

| | Result |
|---|---|
| Index size (videos, 44,979 documents, both force-merged to one segment) | **77.5 MB → 105.5 MB (+28 MB, +36 %)**; channels 195 → 268 KB |
| Typical searches, 30 runs each after warm-up (p95, whole API request) | `kind=videos`: "c" 12 ms, "cr" 12 ms, "la" 14 ms, "recette" 10 ms, "minecraft" 12 ms, "creme b" 11 ms, "bob l" 12 ms, a 10-word query 5 ms. `kind=all` (what the page asks: videos + the channels above): "c" 39 ms, "cr" 32 ms, "la" 32 ms, the others 22–25 ms |
| 193 distinct queries, once each (80 words cut to 1–4 letters, 77 "word + start of word", 36 six-word queries from real titles and descriptions) | `kind=videos` p50 8.6 ms, **p95 10.6 ms**, max 18 ms; `kind=all` p50 18 ms, **p95 30 ms**, max 38 ms |

All well under the 200 ms target. ("c" matches 41,438 of 44,979 videos: the paging stays cheap because only one page is fetched.)

**Tests**: Reqnroll `Search.feature` +8: "creme" finds "crémeux", a start of a description word (4 cases: "onct", "ONCT", "riz crém", "lait CREM", starts that no stemming reaches), a start of a tag, a title start above a description start (against a 90,000× more viewed video), a whole description word above a description start; unit: the query shape (tags and `description.prefix` as `match_bool_prefix` with weights 0.75 and 0.5), the mappings carry `index_prefixes` (videos and channels). The first description scenario used "cremeu": the description-prefix mutation survived it, because the French stemmer already reduces "crémeux" to "cremeu" (a whole-word match), so the scenario now uses starts beyond stemming.

| # | Mutation | Caught by |
|---|---|---|
| P1 | No description start-of-word clause | 6 scenarios |
| P2 | No tag start-of-word clause | "The word being typed may start a tag" |
| P3 | Description start weighted ×40 (above titles) | 2 scenarios ("A start of word in a title ranks above one in a description", "A match in the title ranks above a match in the description") |
| P4 | Description matched as whole terms instead of starts | 2 scenarios |

**Found by a check.sh run, fixed on the branch**: an automatic-rebuild scenario failed once with "alias … doesn't exist". Its step read the run history *before* restarting, which started a backend with the scheduler on, whose startup rebuild the restart then cancelled. That exposed a real hazard: a rebuild cancelled (or failing) **after** the read-alias switch went to the abort path, which deletes the new generation, the one users had just been switched to. Now the completion isn't cancellable (`CancellationToken.None`), and a failure to delete the previous generation after the switch is logged as an error instead of aborting (the old indices stay, without aliases). The step counts the runs before the settings change.

One more check.sh run failed once on a "first sync" step that failed within half a second, the first scenario of the run on a fresh test Elasticsearch, while the backend unit tests ran next to it. It didn't come back in two full Reqnroll runs, three runs of the feature and the final check.sh; the step now prints the run's error if it happens again.

Totals after this round: **3,928 backend tests** (3,918 before: Reqnroll +8, unit +2) and **330 frontend tests** (unchanged), all passing (`./scripts/check.sh`).
