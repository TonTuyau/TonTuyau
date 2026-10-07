# TonTuyau — Q&A session 06

> 2026-10-03, night. The open questions of both parallel milestones in one place: **M4** (REST API, branch `milestone/m4-api`, notes in `~/Documents/TonTuyau-m4/docs/milestones/M4-api.md`) and **M5a** (live mockup, branch `milestone/m5a-front-mockup`, notes in `~/Documents/TonTuyau-m5a/docs/milestones/M5a-front-mockup.md`).
> Each one says what is built today (the default). Write "ok" to keep it, or your choice. Questions in part A change the API contract: answer those first, M4 gets adjusted right after.

Answered 2026-10-03 (morning); consequences: contract v1.1, M4 and M5a updates, M5 additions (end of file).

Status when written: both milestones finished, checks pass (M4: 2,393 backend tests, re-run independently; M5a: 182 frontend tests). Nothing merged into `main`.

---

## A. Questions that change the API contract

**M5a-1. Upload-date filter.** The contract has `publishedAfter` / `publishedBefore` (dates). The mockup's YouTube presets (Today, This week, This month, This year) are converted to `publishedAfter = now − period` in the front.
  a) keep the conversion in the front (it's translating a choice, not inferring data)
  b) the API takes a named period, `published=today|week|month|year` (contract change)
Default: a)
**Answer:** (after the explanation in chat) b), and no date range at all: the API takes `published=today|week|month|year`; `publishedAfter` / `publishedBefore` are gone. → contract v1.2.

**M5a-2. Channel subscribers on the watch page** (YouTube shows them under the channel name). `VideoDetail.channel` only has id, name, thumbnail.
  a) leave them out
  b) add `subscribers` to `VideoDetail.channel` (contract change)
Default: a)
**Answer:** Add the subscribers. → contract v1.1: `VideoDetail.channel.subscribers`.

**M5a-3. "More from <channel>" on the watch page** can list the video being watched (marked "Now playing"): removing it in the front would be client-side filtering.
  a) keep it, marked
  b) the API takes `exclude=<id>` on `/api/channels/{id}/videos` (contract change)
Default: a)
**Answer:** Filter in the backend: give the current video's id to the API. → contract v1.1: `exclude=<videoId>` on `/api/channels/{id}/videos`.

**M5a-13. `kind=all` on later search pages.** The contract says "top channels on the first page only". Write in the contract that later pages return `"channels": []` (not a missing field)?
Default: yes, `[]`
**Answer:** `[]` on later pages. → contract v1.1.

**M5a-17. `tvArtUrl`** is used nowhere in the mockup (YouTube has no place for TV art). Drop it from the contract, or keep it for a later TV layout?
Default: keep
**Answer:** Drop it, we won't use it. → contract v1.1: no `tvArtUrl`, no `tvart` art kind.

**M4-Q2. Suggestions.** The contract says "channels first, then video titles": typing one letter can fill all 8 suggestions with channels.
  a) literal (today)
  b) at most 3 channels, video titles fill the rest (contract wording change)
Default: a)
**Answer:** Alternative: at most 3 channels, titles fill the rest. → contract v1.1.

**M4-Q4. Image caching.** Images are cached one day, then re-checked cheaply (ETag → 304).
  a) keep it
  b) versioned image URLs (`/art/videos/<id>?v=<hash>`) cached for a year (contract change)
Default: a)
**Answer:** Default.

**M4-Q5. AV1 playability check.** `mimeType` uses one AV1 string (level 4.0 = up to 1080p). A browser may say it can play a 4K AV1 file it can only decode slowly in software (stutter).
  a) keep the single representative string per codec (today)
  b) derive the level from the video's resolution (e.g. 4K → `av01.0.12M.08`) so the browser answers for the real file
Default: a)
**Answer:** Default.

**M4-Q6. Elasticsearch down.**
  a) 500 (today)
  b) 503 + `Retry-After` (contract change), so the page can say "try again in a minute"
Default: a)
**Answer:** Default.

---

## B. API behaviour (no contract change)

**M4-Q1. Search matching.** Every word must match (AND across fields): a typo or an extra word gives nothing.
  a) AND (precise, today)
  b) "most words" (75 % of the words must match): more forgiving, noisier
Default: a)
**Answer:** Default.

**M4-Q3. Download names.** Characters forbidden in file names become `_` ("HUILES _ pourquoi…"), like Chrome does. Alternatives: `:` → ` -`, or look-alike Unicode characters in the UTF-8 name only.
Default: `_`
**Answer:** Default.

**M4-Q7. Malformed ids** (`/api/videos/abc`) answer 400, not 404.
Default: 400
**Answer:** Default.

---

## C. Mockup display choices

**M5a-4. Picking a suggestion** opens the channel's page or the video directly (YouTube instead searches for the suggestion's text). Default: opens it. **Answer:** (after the explanation in chat) b), like YouTube: picking a suggestion puts its text in the search box and shows the results.

**M5a-5. Shorts** play on the same watch page with a vertical player (not a YouTube-style `/shorts/` swipe player). Default: same page. **Answer:** YouTube-like `/shorts` vertical player, **with an option to disable it** (people who don't like it get the normal player).

**M5a-6. "Streams" in French**: "Lives", "En direct" (YouTube's tab) or "Diffusions"? Default: "Lives". **Answer:** "Lives" for now.

**M5a-7. Links and timestamps in descriptions** stay plain text (not clickable). Default: plain text. **Answer:** Linkify URLs.

**M5a-8. Channel handle** (`@name`) isn't shown (not in the contract). Default: not shown. **Answer:** Not shown.

**M5a-9. Feed pages** (`/feed/videos|shorts|streams`, the whole library per type) behind the sidebar's Videos / Shorts / Streams. Default: kept. **Answer:** Keep them.

**M5a-10. Channel default tab**: the first type with videos (YouTube's "Home" tab can't be filled). Default: first non-empty. **Answer:** First non-empty tab.

**M5a-11. Result counts** ("1,234 results", "116 channels") are shown; YouTube shows none. Default: shown. **Answer:** Keep.

**M5a-12. Shorts on Home**: a grid of vertical cards, or one horizontal shelf like YouTube's? Default: grid. **Answer:** Grid for now.

**M5a-14. No sort options for channel search results** (the contract's search sorts are for videos). Default: none. **Answer:** No sort for channels.

**M5a-15. Mini rail on phones** (your item #5 says collapsed = rail at every width): it takes 72 px of a 375 px screen. Keep it, hide it on phones (hamburger → drawer only), or a YouTube-style bottom bar? Default: rail kept. **Answer:** Bottom bar on phones, like YouTube.

**M5a-16. `canPlayType` "maybe" counts as playable** (browsers answer "maybe" for types they handle; a decode error still falls back to the message + download). Default: yes. **Answer:** Try to play it, and tell the user it may not work, e.g. "This video format may be incompatible with your browser. You can also download the video." (with the download button).

---

## Also confirmed

- UI preferences (language, theme, sidebar, shorts player) are stored locally in the browser (localStorage), never on the server.

## Added by you for M5 (the real frontend)

- The video player adapts to the video's aspect ratio (no black bars around a vertical or 4:3 video).
- An option for a small or a large player (YouTube's default / theater views).
