# M5a: Front mockup (live review)

Branch: `milestone/m5a-front-mockup` · Features: `feature/m5a-shell`, `feature/m5a-mock-api`, `feature/m5a-pages`, `feature/m5a-tests`, `feature/m5a-real-channels`, `feature/m5a-settings`, `feature/m5a-docs`; QA06 round: `feature/m5a-contract-v1.1`, `feature/m5a-shorts-player`, `feature/m5a-linkify`, `feature/m5a-fr-lives`, `feature/m5a-bottom-nav`, `feature/m5a-qa06-docs`; QA06 final answers: `feature/m5a-published-period`, `feature/m5a-suggestion-search`, `feature/m5a-maybe-notice`, `feature/m5a-preferences-persist`, `feature/m5a-qa06-final-docs`; QA07: `feature/m5a-shorts-library`, `feature/m5a-phone-no-hamburger`, `feature/m5a-qa07-docs`

Spec: [API_CONTRACT.md](../API_CONTRACT.md) (v1.3). The mockup only uses what the contract gives. Decisions: R2-9, R2-13, R2-16 to R2-19 ([QA_SESSION_01.md](../QA_SESSION_01.md)), Q5-2 and Q5-3 ([QA_SESSION_05.md](../QA_SESSION_05.md)), your QA06 answers ([QA_SESSION_06.md](../QA_SESSION_06.md), see [QA06 round](#qa06-round-contract-v11)). Built in parallel with M4: it runs against a mock API now and should run against the backend later with no code change.

Once you approve a page, it becomes the spec for M5/M6. Review items already handled during the live review: #1 language menu (since folded into #3), #2 real channels, #3 settings menu, #4 selectable dark theme, #5 collapsed guide, #6 "Unavailable on YouTube" wording. Every choice that I made without asking is listed under [Open questions](#open-questions), with the default I took.

## What was built

**Shell** (`frontend/src/`)
- **Top bar**: hamburger, logo, search box with suggestions, and the settings menu ("⋮"). At phone width the search box opens over the whole bar, and there is no hamburger (QA07, M5a-21).
- **Left guide** (review item #5): Home, Shorts, Channels / Explore: Videos, Streams. By default it is **collapsed** on every page and at every width: YouTube's mini rail, with icons and small labels. On wide screens (≥ 1024 px) the hamburger expands it to the full guide and the choice is remembered (`localStorage` `tontuyau.guide`). On narrow screens the hamburger opens the full guide as an overlay drawer (closed by Esc, a click on the scrim, or a navigation).
- **Router** (`lib/router/`): History API, YouTube-like URLs (`/watch?v=`, `/results?q=`). In-app `<a href>` clicks become navigations. Back/forward work and deep links work. Links to `/media`, `/api`, `/art`, other origins, new tabs, downloads and ctrl/cmd-clicks are left to the browser. The tab title follows the page.
- **i18n** (`lib/i18n/`): our own typed dictionary (`en.ts` is the reference; `fr.ts` is typed `Dictionary`, so a missing or extra key is a compile error; `t('unknown')` is one too). The initial language comes from the browser (`fr-CA` → `fr`, otherwise English). The choice is kept in `localStorage` (every access in try/catch) and also sets `<html lang>`. Plurals use `Intl.PluralRules`. Numbers, dates, ages and sizes use `Intl` (`lib/format.ts`).
- **Settings menu** (review items #1 and #3, `components/SettingsMenu.svelte`): a "⋮" button opening a themed popover with two rows, "Appearance: <current>" and "Language: <current>". Each opens its list in place (Use device theme / Dark theme / Light theme; English / Français), with a back arrow and a check on the current choice. It follows the ARIA menu button pattern: Enter/Space/↓ ↑ open; ↑ ↓ Home End move; → or Enter opens a list, ← or the back arrow returns; Esc, Tab or a click outside close it; focus goes back to the button. Choosing closes the menu.
- **Theme** (`theme/youtube.css`, review item #4): every colour, font, size, radius, spacing, z-index and shadow is a CSS custom property. Every theme-dependent colour is written once as `light-dark(<light>, <dark>)`, so dark mode covers everything that uses the tokens (page, top bar, guide, cards, menus, inputs, skeletons, badges, errors; the player area is black in both). "Use device theme" (the default) is `color-scheme: light dark`, which follows `prefers-color-scheme`. Dark and Light set `data-theme="dark|light"` on `<html>`, which forces `color-scheme`. The choice is kept in `localStorage` (`tontuyau.theme`, try/catch). An inline script in `index.html` applies it before the first paint, so a forced dark theme doesn't flash white. `theme/theme.css` is the one-line switch (`@import './youtube.css'`). Components only use `var(--…)`, and a test enforces it.
- **API client** (`lib/api/`): types hand-written from the contract. It calls `/api/...` on the page's origin, leaves out empty parameters, and turns ProblemDetails into `ApiError` (`status`, `title`, `detail`, `errors`). It also handles non-JSON errors (a proxy page) and network failures (status 0). `Remote` / `Pager` (`lib/remote.svelte.ts`) handle loading, errors, retry, abort on parameter change, and cursor paging: items are appended in API order, and `total` and `nextCursor` are used exactly as sent.

**Pages** (all states: skeleton while loading, empty, ProblemDetails error with Retry)

| Page | URL | API calls |
|---|---|---|
| Home | `/` | `/api/home`: sections in the API's order, shorts in the vertical format, "See all" → feed |
| Feed | `/feed/videos\|shorts\|streams?sort=` | `/api/videos?type=&sort=`: the whole library of one type (sidebar entries) |
| Channels | `/channels?q=&sort=` | `/api/channels?q=&sort=`. Filter as you type (debounced, written to the URL with `replaceState`), name/subscribers chips, total from the API, infinite scroll + "Show more" |
| Channel | `/channel/<id>[/videos\|shorts\|streams]?sort=` | `/api/channels/{id}`: banner, avatar, name, subscribers and the counts > 0, expandable description, "Channel unavailable on YouTube" badge. Tabs = types with `videoCounts` > 0. `/api/channels/{id}/videos?type=&sort=`: latest/popular/oldest. "Search this channel" → `/results?q=&channel=<id>` |
| Watch | `/watch?v=<id>` | `/api/videos/{id}`: player, or "file unavailable", or "This browser can't play AV1 + Opus" + Download. Title, inactive badge, channel, likes, Download · size, views + date, `#tags` (links to a search), "…more" shows categories + file details (MIME type, tracks, archive date). Side column: `/api/channels/{id}/videos?type=<same type>&sort=latest&size=12&exclude=<id>` (v1.1) |
| Search | `/results?q=&type=&duration=&uploaded=&channel=&sort=` | `/api/search`: `kind=all` with no type (top channels + videos), `kind=videos` with a video type, `kind=channels` for "Channel". YouTube-style filters panel, removable filter chips, "Clear all", paging. Upload date: `uploaded=<period>` in the page URL, sent as `published=today\|week\|month\|year` (v1.2) |
| Not found | anything else | none |

**Codec check (R2-13, M5a-16)** (`lib/codec.ts`, `components/Player.svelte`): the player shows only when `video.canPlayType(mimeType)` answers "probably" or "maybe", or when `MediaSource.isTypeSupported(mimeType)` is true. With "maybe", a notice under the player (over the top of the video in the shorts player) says the format may be incompatible, with the Download button. Otherwise the page shows "This browser can't play <codecs>", with the codecs named from `tracks` (av1 → AV1, hevc → HEVC (H.265)…), and the Download button. If the browser accepts the type but then fails to decode (`error` event), the same message is shown with "couldn't play this file". `mediaAvailable: false` shows "File unavailable" with no player and no download.

**Inactive badge (R2-9, review item #6)**: `active: false` shows "Unavailable on YouTube" / "Indisponible sur YouTube" on cards and the watch page, and "Channel unavailable on YouTube" on a channel page. The wording is neutral because TubeArchivist's flag only means yt-dlp can no longer get the video: it may be deleted, private, region-blocked or taken down, and we don't know which. The tooltip, also shown as text on the watch and channel pages, says "TubeArchivist can no longer reach this video on YouTube; this is the archived copy."

**Mock API** (`frontend/mock/`, dev server only, never in the app bundle)
- `vite --mode mockup` adds a middleware that answers `/api`, `/art`, `/media` and `/__mock`. The app code is identical in both modes. Without the mode, Vite proxies `/api`, `/media`, `/art` to `TONTUYAU_BACKEND_URL` (default `http://localhost:5080`) as before.
- `handler.ts` follows the contract: shapes from the same TypeScript types as the app, per-type counts, channel join, sorts, filters, accent-insensitive search/suggest (simple substring/prefix match), opaque cursors tied to their filters + sort (another sort → 400), ProblemDetails 400 (with `errors`) / 404 / 405.
- **Fixtures** (`mock/fixtures/*.json`, committed, synthetic): 11 fictional channels and 179 videos. Edit them by hand (the dev server reloads them), or regenerate with `pnpm run mock:fixtures` (seeded generator `mock/generate-fixtures.ts`).
- **Images**: SVG gradients with initials, generated per id. **Media**: two tiny synthetic test-pattern MP4s (H.264/AAC, 12 s landscape and 8 s portrait), served with range requests and the Q5-3 `Content-Disposition` (`<title>.mp4` + UTF-8 `filename*`).
- **Real-channels layer** (review item #2, local only): when `dev-data/mockup/` exists (git-ignored, never committed, R2-4), or the folder in `TONTUYAU_MOCK_REAL_DIR`, the mock uses the real channel names, subscribers, descriptions, tags and pictures. `/art/channels/{id}/thumb|banner` is served from `channel-art/`, and `videoCounts` comes from `channel-video-counts.json`. Videos stay synthetic: up to 30 copies of the synthetic videos per channel and type (enough to page), plus the edge-case videos under their readable ids. The Vite console prints `[mock] channels: real channels from … (116 channels, 5052 synthetic videos)` or `[mock] channels: synthetic channels (…)`.
- **`/__mock`** (open it in the browser): channels real / synthetic, scenario (normal / empty library / every `/api` call fails with a 500), response delay (0 / 300 ms default / 1.5 s / 5 s, to see the loading states).

## How to check it

```bash
cd frontend
pnpm install
pnpm run dev:mockup                 # http://localhost:5173 (mock API, hot reload)
pnpm run dev:mockup --port 5180     # or PORT=5180 pnpm run dev:mockup
pnpm run dev                        # real mode: proxies /api /media /art to TONTUYAU_BACKEND_URL (M4)

pnpm run lint && pnpm run check && pnpm run test && pnpm run build
../scripts/check.sh                 # everything, front and back (Docker + free RAM for the backend part)
```

### URLs for the live review

With the real-channels layer present, the synthetic channel ids (`UCmock-…`) only exist after `/__mock?channels=synthetic`. The edge-case videos work in both modes.

| What | URL |
|---|---|
| Home | `/` |
| Feeds | `/feed/videos`, `/feed/shorts?sort=popular`, `/feed/streams?sort=oldest` |
| Channels, filter, sort, paging | `/channels`, `/channels?q=eleo` (accents ignored), `/channels?sort=subscribers`, `/channels?q=zzz` (no match) |
| Channel: all three tabs | `/channel/UCmock-pixelforge-gaming`, `/channel/UCmock-pixelforge-gaming/streams?sort=popular` |
| Channel: shorts only (no Videos tab) | `/channel/UCmock-minute-meca000000` |
| Channel: streams only | `/channel/UCmock-direct-dimanche00` |
| Channel: no videos at all | `/channel/UCmock-archives-attente0` |
| Channel: unavailable on YouTube (`active: false`), no banner | `/channel/UCmock-quiet-trails00000` |
| Channel: no avatar | `/channel/UCmock-code-et-cafe00000` |
| Channel: huge subscriber count | `/channel/UCmock-megastar-hits0000` |
| Channel: unknown id (404) | `/channel/UCnope` |
| Watch: plays (test pattern) | `/watch?v=mockFresh01` ("2 hours ago" at the fixtures' sync time; older as days pass) |
| Watch: unavailable on YouTube (`active: false`) | `/watch?v=mockInactiv` |
| Watch: file unavailable | `/watch?v=mockNoFile0` |
| Watch: HEVC (plays or not, depending on the browser) | `/watch?v=mockHevc000` |
| Watch: VVC (no browser plays it) | `/watch?v=mockVvc0000` |
| Watch: very long title + long description | `/watch?v=mockLongTtl` |
| Watch: 3.5 billion views | `/watch?v=mockHugeVws` |
| Watch: no description, no tags | `/watch?v=mockNoDescr` |
| Watch: no thumbnail / thumbnail 404 | `/watch?v=mockNoThumb`, `/watch?v=mockArt404x` (placeholder on the cards; see them in `/channel/UCmock-atelier-bois-fer`) |
| Shorts player (vertical, ↑ ↓ / wheel / buttons) | `/shorts/mockShort01`, `/shorts/mockInactSh` (removed short); "⋮" → Shorts player → Normal sends them to `/watch` |
| Watch: a short on the watch page, a removed short | `/watch?v=mockShort01`, `/watch?v=mockInactSh` |
| Watch: links in the description (and markup shown as text) | `/watch?v=mockLinks00` |
| Watch: a 6-hour stream | `/watch?v=mockStream1` |
| Watch: unknown id (404 detail), no id | `/watch?v=nope`, `/watch` |
| Search: channels + videos | `/results?q=cuisine` |
| Search: accents ignored | `/results?q=creme` |
| Search: filters | `/results?q=castle&type=stream&duration=long`, `/results?q=a&uploaded=year&sort=popular`, `/results?q=a&channel=UCmock-orbital-mechanics` |
| Search: channels only | `/results?q=a&type=channel` |
| Search: no results / no query | `/results?q=zzzz`, `/results` |
| Suggestions | type `pix`, `cui` or `tom` in the top search box: ↓ ↑ Enter Esc; picking one searches its text |
| Watch: canPlayType "maybe" (notice + Download under the player, in Chromium) | `/watch?v=mockMaybe00` |
| Empty library, errors, slow API | `/__mock?scenario=empty`, `/__mock?scenario=error`, `/__mock?delay=5000` (then `/__mock?scenario=normal&delay=300`) |
| Not found | `/nothing/here` |
| Settings | "⋮" in the top bar: Appearance (device / dark / light), Language (English / Français), Shorts player (vertical / normal) |
| Phone width | any page at ≤ 600 px: one column, bottom navigation bar (no mini rail, no hamburger, no drawer), search opens over the bar |

## Tests added

| Kind | What | Count |
|---|---|---|
| Vitest, pages | Home (API section and item order kept, only the sections sent, loading → empty, ProblemDetails detail + Retry), Channels (API order, API `total` ≠ items, `q`/`sort` sent, typing → URL + request, cursor paging, error), Channel (tabs only from `videoCounts`, tab type + sort sent, banner/avatar URLs as given, inactive badge, no content → no list request, 404), Watch (facts, likes, download URL, tags, side list with "Now playing", expand details, 404 detail, no id → no call), Search (`kind=all` / `videos` / `channels` parameters, every filter sent, channel chip named by the API, no results, no query → no call, filter links toggle), Feed | 26 |
| Vitest, components | Player codec decision (probe called with the API MIME type, unsupported → message + download, real `canPlayType` mocked, unavailable, decode error), VideoCard / ShortCard (fields formatted, URLs as given, badge only for `active: false` with its tooltip, French badge, placeholders, French), SearchBox (API order, debounce, ↓ ↑ wrap, Enter on channel / video, Esc, Enter without a pick), SettingsMenu (main rows with current values, Appearance → Dark applied + saved + focus back, language entirely by keyboard, back arrow and ←, Esc and outside click, French labels) | 23 |
| Vitest, lib | router (push/replace, back/forward, link interception and the links left to the browser), routes (matching table, href builders, round trip), i18n (same keys and placeholders FR/EN, missing/unknown key = type error via `@ts-expect-error`, browser language, saved choice, storage that throws, plurals FR/EN), theme preference (default = no `data-theme`, forced dark/light, saved and read back, storage that throws, the pre-paint script in `index.html`), codec, format, search filters, API client (parameters, escaping, ProblemDetails, non-JSON error, network error) | 68 |
| Vitest, theme | no raw colour / font / px in any component's `<style>`, every token used is defined, every theme-dependent colour has light and dark values, `data-theme` overrides | 34 |
| Vitest, mock | the mock answers like the contract (fixtures cover the edge cases, home order, cursor walk without duplicates, wrong-sort cursor → 400, invalid parameter → ProblemDetails, 404, counts, accent folding, `kind=all` channels on the first page only, filters, suggest order, art 404 cases), the real-channels layer on a throwaway folder (mapping, counts, paging, edge ids, art path safety) | 25 |
| App shell | brand/search/nav, only the current page's request, not-found without a call, guide collapsed by default, hamburger expands + remembered, drawer on narrow screens | 6 |

Total: **182 frontend tests, all passing** at the end of the milestone; **226** after the QA06 round (mock `exclude`, suggestion cap, `channels: []`, no TV art, watch-page subscribers and `exclude`, shorts player and its setting, card links, linkify and escaping, French "Lives", bottom bar); **245** after the final answers (named `published` period and its cursor cut-off, suggestions searching their text, the "maybe" notice, preferences across a reload); **250** after QA07 (`exclude` on the mock's `/api/videos`, the library-wide shorts sequence, no hamburger or drawer on phones). Lint, Prettier, `svelte-check --fail-on-warnings` and the build are clean.

## Decisions taken during the milestone

- **Mock at the HTTP level, in the Vite dev server** (not an in-browser fetch patch): the browser sees real requests, status codes, `Content-Type: application/problem+json`, range requests and download headers, and the app bundle contains no mock code.
- **Own router** (≈ 100 lines) rather than a library: the History API, a route table, and the URLs in one place (`href.*`).
- **URL = state** for every filter, sort and tab, so deep links, reloads and back/forward all work. As-you-type filters use `replaceState`, so typing doesn't fill the history.
- **Lists keep showing the previous results while a new filter loads** (dimmed) on the Channels page, so typing doesn't flash a skeleton on each key.
- **Languages in the menu are labelled in their own language** (English / Français), as YouTube does.
- **`light-dark()` for the dark theme** rather than a second block of dark tokens under a media query and again under `[data-theme=dark]`: each colour is written once, and swapping the theme stays a one-file job. It needs Chrome 123+, Firefox 120+ or Safari 17.5+ (2024 browsers).
- Placeholder art for `thumbnailUrl: null` and for an image that fails to load: a neutral box with an icon (thumbnails), a person icon (avatars). Nothing is derived from the name.

## Deviations from the plan

- **API types are hand-written** from the contract (`src/lib/api/types.ts`), since M4's OpenAPI document isn't there yet. The plan says `openapi-typescript`: that switch is for M5.
- **Feed pages** (`/feed/videos|shorts|streams`) weren't in the page list. They reuse `/api/videos` and give the sidebar's Shorts / Videos / Streams entries somewhere to go (see the open questions).
- No deviation from the contract: every endpoint, parameter and shape is used as written. (The `publishedAfter` presets that were converted in the front are gone since v1.2: the front sends the period's name.)

## Known limitations / follow-ups

- The mock's search is a plain accent-insensitive substring match with a crude relevance. Ordering and matching will differ from Elasticsearch's French analysis.
- The mock media is the same test pattern for every video, whatever `mimeType` says. So "playable" in the mockup means "the browser accepts the declared type". The real file is only tested against M4.
- In real-channels mode, a channel's list `total` (≤ 30 synthetic cards) differs from its tab count (the real number). That's by design, since only the counts are real.
- Ages ("2 hours ago") are relative to now, so the fixtures age as days pass.
- After back/forward, the scroll position isn't restored (the page reloads its data).
- Older browsers without `light-dark()` (before 2024) would get no colours at all.
- Tests run in jsdom: no layout, so line clamping, overflow and the responsive layout were checked by hand in the browser (no horizontal scroll at 375 px on any page), not by tests.

## QA06 round (contract v1.1)

Your answers in [QA_SESSION_06.md](../QA_SESSION_06.md), applied on top of the milestone. Review log: [M5a-review.md](M5a-review.md) items 7 to 12.

- **Contract v1.1** (`docs/API_CONTRACT.md`, copied from the main checkout): `VideoDetail.channel.subscribers` is shown under the channel name on the watch page ("224K subscribers" / "224 k abonnés"). "More from <channel>" sends `exclude=<current id>` and shows the list as the API returns it; the "Now playing" marking is gone. The mock implements `exclude` (left out of the items and of `total`, part of the cursor's key), caps suggestions at 3 channels (video titles fill the rest), answers `"channels": []` on later `kind=all` pages, and has no TV art any more (no `tvArtUrl`, no `tvart` image kind, the real layer's TV art pictures are ignored).
- **Shorts player** (M5a-5): `/shorts/<id>`, YouTube-like. The short fills the height, starts by itself and loops, with the channel, title, views and the inactive badge over the video, and likes, Download and "Open in the normal player" beside it. ↑ ↓ buttons, the mouse wheel (one short per gesture), the arrow keys (not while typing or in a menu) and a swipe on touch screens move through the sequence. The sequence comes from the API: the short you opened, then its channel's other shorts, latest first (`/api/channels/{id}/videos?type=short&sort=latest&exclude=<id>`, next page loaded before the end); since QA07, every short of the library (`/api/videos?type=short&sort=latest&exclude=<id>`). Moving replaces the URL, so Back leaves the player. A video the API says isn't a short goes to the watch page. The setting "⋮ → Shorts player: Vertical / Normal" (`localStorage` `tontuyau.shortsPlayer`, like the theme; default Vertical) decides where short cards go: `/shorts/<id>` or `/watch?v=<id>`. With Normal, `/shorts/<id>` itself goes to the watch page.
- **Links in descriptions** (M5a-7): http(s) URLs in video and channel descriptions are links (`target="_blank" rel="noopener noreferrer"`). `lib/linkify.ts` cuts the text into text and link segments that the template renders as text nodes and `<a>` elements: nothing from the data is ever parsed as HTML. Sentence punctuation and unbalanced closing brackets stay out of the link. Timestamps, bare domains (`www.…`) and other schemes stay plain text. New edge case: `mockLinks00`.
- **"Lives"** in French (M5a-6): already the label everywhere; a test now pins it.
- **Phones** (M5a-15): at ≤ 600 px (the theme's existing phone breakpoint), a fixed YouTube-style bottom bar with the guide's five destinations replaces the mini rail. The hamburger still opened the full guide as a drawer until QA07, which removed it on phones. Tablets and desktops are unchanged. The destinations live in `lib/navItems.ts`, shared by the guide and the bar.

Your final answers (QA06 after the chat, contract v1.2), applied next; review log items 13 to 17:

- **Upload date as a named period** (M5a-1, v1.2): the search sends `published=today|week|month|year` as chosen; the front computes no date, and `publishedAfter` / `publishedBefore` are gone from the types, the mock and the UI. There is no custom range. The mock computes the cut-off (24 h / 7 / 30 / 365 days) on the first page and keeps it in the cursor, so later pages use the same one. The page URL keeps `uploaded=<period>`.
- **Picking a suggestion** (M5a-4): like YouTube, its text goes into the search box and `/results?q=<text>` opens, for channel and video suggestions alike (keyboard or mouse). The "Channel" / "Video" marker from the API stays on each line.
- **Feed pages** (M5a-9): kept as they are. M5a-20 is settled: "Shorts" in the guide and the bottom bar opens `/feed/shorts`.
- **"maybe" notice** (M5a-16): `lib/codec.ts` now answers `probably` / `maybe` / `no`. "maybe" plays the video with a notice and the download: "This video format may be incompatible with your browser. You can also download the video." / "Le format de cette vidéo est peut-être incompatible avec votre navigateur. Vous pouvez aussi télécharger la vidéo." "probably" shows no notice. "no" and decode errors keep the existing messages (a decode error replaces the notice). New edge case `mockMaybe00` with `video/mp4; codecs="avc1"` (no profile): checked in Chromium 152, `canPlayType` answers "maybe" and MSE says no, and the file plays. Tests stub `canPlayType`.
- **Preferences are localStorage-only** (confirmed): language, theme, guide and shorts player each had a save/read-back test; a new test checks that choosing all four makes no request, writes exactly the four `tontuyau.*` keys, and that fresh modules (a reload) read all four back, with the defaults on empty storage. The theme is also applied before the first paint (`index.html`, already tested).

Choices I made without asking in this round (defaults, easy to change):

**QA07** ([QA_SESSION_07.md](../QA_SESSION_07.md), contract v1.3) answered these; review log items 18 to 21:

- **M5a-18 b)**: the vertical player goes through the **whole library**: the opened short, then `/api/videos?type=short&sort=latest&exclude=<opened id>`, paged with the cursor. The mock's `/api/videos` takes `exclude` like the channel endpoint (left out of the items and of `total`, part of the cursor's key).
- **M5a-19 a)**: unchanged, the URL is replaced, so Back leaves the player.
- **M5a-21 b)**: no ☰ on phones (≤ 600 px). The bottom bar is the only navigation there: the top bar has no menu button and the drawer can't open; shrinking the window to phone width closes an open drawer. Tablets (drawer) and desktops (expanded guide) are unchanged.
- **M5a-22 a)**: unchanged, links in both video and channel descriptions.

The defaults as they were before QA07:

| # | Question | Default taken |
|---|---|---|
| M5a-18 | **Which shorts come next** in the vertical player. Only the channel endpoint has `exclude`, so the sequence is the opened short, then its channel's other shorts. A library-wide sequence (all shorts, latest first, like Home's section) would need `exclude` on `/api/videos` too | Same channel's shorts |
| M5a-19 | **History while moving through shorts**: replace the URL (Back leaves the player) or push one entry per short (Back goes to the previous short)? | Replace |
| M5a-20 | **Sidebar / bottom bar "Shorts"** opens `/feed/shorts` (the grid). **Settled** with M5a-9: keep it | Feed grid |
| M5a-21 | **Bottom bar**: five entries (all of the guide's), and the hamburger + drawer kept on phones. YouTube's phone layout has no hamburger | Five entries, hamburger kept |
| M5a-22 | **Links in channel descriptions** too, not only video descriptions | Both |

## Open questions

Each has the default the mockup showed at the end of the milestone. QA06 answered all of them (see [QA06 round](#qa06-round-contract-v11)); the newer ones (M5a-18 to M5a-22) are listed there, M5a-20 settled, the others answered in [QA_SESSION_07.md](../QA_SESSION_07.md).

| # | Question | Default taken |
|---|---|---|
| M5a-1 | **Upload-date filter**: the contract has `publishedAfter` / `publishedBefore` (ISO dates). The YouTube-style presets (Today, This week, This month, This year) are converted into `publishedAfter = now − period` in the front (`lib/searchFilters.ts`, marked `TODO(contract)`). Is that acceptable as "translating a choice", or should the API take a named period (`published=today\|week\|month\|year`)? A custom date range (`publishedBefore`) isn't offered | Front converts the preset; no custom range |
| M5a-2 | **Channel subscribers on the watch page**: YouTube shows them under the channel name; `VideoDetail.channel` only has id, name, thumbnail. Add `subscribers` to the embedded channel (contract change), or fetch `/api/channels/{id}`, or leave them out? | Left out |
| M5a-3 | **Watch page side column**: "More from <channel>" = `/api/channels/{id}/videos?type=<same>&sort=latest&size=12`. The current video can appear in it (marked "Now playing"), because removing it would be client-side filtering. Keep it, drop it, or ask for an `exclude=<id>` parameter? | Kept, current video marked |
| M5a-4 | **Picking a suggestion**: ours carry `kind` + `id`, so a channel opens its page and a video opens its watch page. YouTube instead searches for the suggestion's text | Opens the channel / video |
| M5a-5 | **Shorts playback**: the same `/watch?v=` page with a vertical player, or a YouTube-style `/shorts/<id>` swipe player? | Same watch page, vertical player |
| M5a-6 | **"Streams" in French**: "Lives" (common usage), "En direct" (YouTube's channel tab), or "Diffusions"? | "Lives" |
| M5a-7 | **Description links and timestamps** aren't made clickable (that would be parsing the text). Linkify URLs (a display concern) or keep plain text? | Plain text with line breaks |
| M5a-8 | **Channel handle** (`@name`): YouTube shows it; the contract has none | Not shown |
| M5a-9 | **Feed pages and sidebar**: keep `/feed/videos|shorts|streams` (whole library per type, from `/api/videos`) behind the Shorts / Videos / Streams entries? | Kept |
| M5a-10 | **Channel default tab** when the URL has none: the first type with a count > 0 (Videos, Shorts, Streams). YouTube has a "Home" tab we can't fill | First non-empty tab |
| M5a-11 | **Result counts**: "1,234 results" (search, feeds) and "116 channels" come from `total`. YouTube shows no counts. Keep? | Shown |
| M5a-12 | **Home shorts**: all the API's shorts (24) in a vertical-card grid, or one horizontal shelf like YouTube's? | Grid |
| M5a-13 | **`kind=all`, next pages**: the contract says "channels: first page only". The mockup expects `"channels": []` (not an absent field) on later pages; I'd write that in the contract | `[]` on later pages |
| M5a-14 | **Sort for `kind=channels`**: the contract's search `sort` is for videos; the mockup hides the sort options for "Channel" results (the API's order) | No sort for channel results |
| M5a-15 | **Mini rail at phone width** (review item #5 says collapsed = mini rail at every width): it takes 72 px of a 375 px screen. YouTube's phone layout has no rail (a bottom bar instead). Keep the rail on phones, hide it there (hamburger → drawer only), or a bottom bar? | Rail on phones too |
| M5a-16 | **`canPlayType` = "maybe"** counts as playable (with full codec strings, browsers answer "maybe" for types they handle). Too lenient? The decode-error fallback catches the rest | "maybe" = playable |
| M5a-17 | **`tvArtUrl`** isn't used anywhere (YouTube has no place for it). Drop it from the contract, or keep it for a later TV layout? | Unused, kept in the contract |
