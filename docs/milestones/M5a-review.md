# M5a live review log

Your remarks during the live review of the mockup (http://localhost:5180), and what was done with each.

| # | Remark | Done | Status |
|---|---|---|---|
| 1 | Language switch: native <select> looks bad, needs a better dropdown style | Custom YouTube-like menu (button + popover, check on current, Esc/outside close, keyboard + ARIA), themed tokens, light/dark + Vitest | Done |
| 2 | Use TubeArchivist's real channel names and profile pictures | Local-only "real channels" layer from git-ignored dev-data/mockup (116 channels, per-type counts, avatars/banners via mock /art); synthetic fallback; nothing real committed (R2-4); toggle on /__mock | Done |
| 3 | A settings menu holding the language option and a dark mode option | One YouTube-style settings menu (gear/⋮) with Appearance (device / dark / light) and Language (EN / FR) submenus, replacing the standalone language switch; keyboard + ARIA | Done |
| 4 | A dark mode | Full dark token set; "Use device theme" (default) follows the system, Dark/Light forced via data-theme; kept in localStorage; applied before first paint (no white flash) | Done |
| 5 | Left menu collapsed by default | YouTube mini rail by default; hamburger expands (overlay drawer on narrow screens); toggle remembered, default stays collapsed | Done |
| 6 | Badge "Removed from YouTube" says more than we know (active:false = yt-dlp can no longer fetch it: deleted, private, region-blocked, taken down) | "Unavailable on YouTube" / "Indisponible sur YouTube", i18n key, tooltip, tests, docs | Done |

## QA06 round

Your answers in [QA_SESSION_06.md](../QA_SESSION_06.md) (2026-10-03), applied on branch `milestone/m5a-front-mockup`. Details: [M5a-front-mockup.md, QA06 round](M5a-front-mockup.md#qa06-round-contract-v11).

| # | Remark | Done | Status |
|---|---|---|---|
| 7 | Contract v1.1 (M5a-2, M5a-3, M5a-13, M5a-17, M4-Q2): channel subscribers on the watch page, `exclude` for "More from", `channels: []` on later pages, at most 3 channel suggestions, no TV art | Types and mock follow v1.1; "224K subscribers" / "224 k abonnés" under the channel name; "More from" sends `exclude=<id>`, no "Now playing" and no client filtering; mock `exclude` (items + `total` + cursor key), suggestion cap, `[]` tested; `tvArtUrl` / `tvart` removed everywhere (fixtures regenerated, real layer ignores its TV art) + Vitest | Done |
| 8 | Shorts: YouTube-like `/shorts` vertical player, with an option to turn it off (M5a-5) | `/shorts/<id>` player: ↑ ↓ buttons, wheel, arrow keys, swipe; sequence from the API (the short, then its channel's shorts via `exclude`); "⋮ → Shorts player: Vertical / Normal" kept in localStorage; short cards follow the setting; both modes tested | Done (new defaults M5a-18 to 20) |
| 9 | Links in descriptions (M5a-7) | http(s) URLs linkified in video and channel descriptions, new tab with `noopener noreferrer`; DOM text and `<a>` only (markup in the data stays text); timestamps stay plain; edge case `mockLinks00` + Vitest (escaping included) | Done |
| 10 | "Lives" in French for streams (M5a-6) | Already the French label everywhere; pinned by a test | Done |
| 11 | Bottom bar on phones, like YouTube (M5a-15) | ≤ 600 px: fixed bottom bar with Home, Shorts, Channels, Videos, Streams instead of the mini rail; tablet and desktop unchanged; follows the viewport live + Vitest | Done (M5a-21) |
| 12 | Still open: upload-date presets (M5a-1), picking a suggestion (M5a-4), feed pages (M5a-9), the "maybe" playability notice (M5a-16) | Answered after the chat: items 13 to 16 | Done |

## QA06 final answers

Your answers after the chat explanations (contract v1.2, merged from `main` at `22d6fea`).

| # | Remark | Done | Status |
|---|---|---|---|
| 13 | Upload date: a named period on the API, no date range (M5a-1, contract v1.2) | Search sends `published=today\|week\|month\|year` as chosen, no date computed in the front; `publishedAfter` / `publishedBefore` removed from types, mock and UI; mock computes the cut-off on the first page and keeps it in the cursor (a later page with a later clock keeps the same set) + Vitest | Done |
| 14 | Picking a suggestion behaves like YouTube (M5a-4) | Its text goes into the box and `/results?q=<text>` opens, for channel and video suggestions, keyboard or mouse + Vitest | Done |
| 15 | Keep the feed pages (M5a-9) | No change; M5a-20 settled: "Shorts" in the guide / bottom bar opens `/feed/shorts` | Done |
| 16 | "maybe": play it and say it may not work, with the download (M5a-16) | Notice + Download under the player (over the video in the shorts player), EN + FR text as given; "probably" no notice; "no" and decode errors unchanged; mock `mockMaybe00` (`video/mp4; codecs="avc1"`, "maybe" in Chromium 152) + Vitest with a stubbed `canPlayType` | Done |
| 17 | UI preferences stay in the browser (localStorage), never on the server | Checked: language, theme, guide, shorts player; new test: no request, exactly the four keys, all read back after a reload (fresh modules), defaults on empty storage | Done |

## QA07

Your answers in [QA_SESSION_07.md](../QA_SESSION_07.md) (contract v1.3, merged from `main` at `e176049`).

| # | Remark | Done | Status |
|---|---|---|---|
| 18 | Shorts player: all shorts of the library, latest first (M5a-18 b, contract v1.3) | Sequence = the opened short, then `/api/videos?type=short&sort=latest&exclude=<id>` with the cursor; mock `exclude` on `/api/videos` (items, `total`, cursor key) + Vitest | Done |
| 19 | Back leaves the shorts player (M5a-19 a) | No change (URL replaced at each short) | Done |
| 20 | No ☰ on phones: the bottom bar is the only navigation (M5a-21 b) | ≤ 600 px: no menu button, the drawer can't open, a drawer open while the window shrinks to phone width closes; tablet / desktop unchanged + Vitest | Done |
| 21 | Links in video and channel descriptions (M5a-22 a) | No change | Done |

