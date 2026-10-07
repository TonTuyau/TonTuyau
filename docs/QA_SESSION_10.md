# TonTuyau — Q&A session 10

> 2026-10-04. Questions from M5 (the real front: real API, states, player, real-data check). Notes: [M5-front.md](milestones/M5-front.md).
> Each says what is built today (the default). Write "ok" to keep it, or your choice, on the **Answer:** line.
>
> Answered 2026-10-04.

---

**M5-1. Searching for a word still being typed.** You type "creme b": the suggestions show 8 titles ("CRÈME BRÛLÉE À LA CHICORÉE…"), because suggestions match the start of words. You press Enter: **no results**, because the search needs every word whole (your M4-Q1 answer: AND).
  a) keep it (today): the search matches whole words only
  b) the last word of the search also matches as the start of a word ("creme b" finds "crème brûlée"), the others stay whole; a backend change, no contract change
Default: a)
**Answer:** b), the last word also matches as the start of a word.

**M5-2. Videos with Dolby audio (E-AC-3, 7 files).** The backend doesn't know the codec, so `mimeType` carries its raw name (`eac3`), which no browser recognises: the page says "This browser can't play H.264 + E-AC-3 (Dolby Digital Plus)" and offers the download.
  a) keep it (today)
  b) the backend sends the standard codec string (`ec-3`, and `ac-3` for AC-3), so a browser that does play Dolby audio (Safari, Edge on Windows) gets the player; Chromium and Firefox on Linux still say no. A line in the backend's codec table
Default: a)
**Answer:** a), keep it.

**M5-3. Length of the channel filter.** The contract gives no limit for `/api/channels?q=`; the backend refuses more than 100 characters, and the front's channel filter boxes now stop at 100.
  a) write it in the contract: "`q` (name, as-you-type, up to 100 characters)" (wording only, no version bump)
  b) leave the contract as it is
Default: a)
**Answer:** a), write the 100-character limit in the contract.

**M5-4. API types.** The plan says the front's types are generated from the backend's OpenAPI document (`openapi-typescript`). They're still hand-written from the contract (as in M5a); the new conformance test checks every request the pages make, and the mock is typed by the same file.
  a) keep them hand-written (today)
  b) generate them from `/openapi/v1.json`, with a copy of the document committed and a test that it matches the running backend
Default: a)
**Answer:** b), generate the types from `/openapi/v1.json` (committed copy + a test that it matches the backend).

**M5-5. A server that doesn't answer.** When the answer isn't the backend's own ProblemDetails (the dev proxy answers an empty 502 when the backend is stopped), the page says "Can't reach the TonTuyau server" instead of "The server ran into a problem". When the connection itself failed, the page also tries again by itself as soon as the browser says it's back online.
  a) as built
  b) no automatic retry (only the Retry button)
Default: a)
**Answer:** a), as built.

**M5-6. Two wordings for a wrong video or channel link.** An unknown id (404) says "This video isn't available"; a malformed one (400, e.g. `/watch?v=nope`) says "This video link isn't valid". Same for channels.
  a) two wordings (today)
  b) one: "This video isn't available" for both
Default: a)
**Answer:** a), two wordings.

**M5-7. The player's shape when the API and the file disagree.** The box takes the API's video track size. A phone video stored rotated, or an anamorphic one, could report one size in the API and another in the browser once loaded. Today the API wins (your rule: facts come from the API); the browser's size is only used when the API has no video track.
  a) the API wins (today)
  b) once the file's metadata has loaded, the browser's size wins (it's what is actually shown)
Default: a)
**Answer:** a), the API wins.

**M5-8. The small / large player button below 1312 px.** Under that width the watch page is one column (player, details, then the other videos), so both sizes look the same and the button isn't shown.
  a) no button there (today)
  b) show it anyway; large would then let the player use the full height of the screen
Default: a)
**Answer:** a), no button below 1312 px.

**M5-9. Tags on the watch page.** Real videos carry up to 76 tags; all of them above the description pushed it five lines down. Now one line of tags shows while the description is collapsed, and all of them after "…more" (YouTube shows three hashtags).
  a) one line, all after "…more" (today)
  b) tags only after "…more"
Default: a)
**Answer:** a), one line, all after "…more".

---

Resized art (QA09, contract v1.4), built after the questions above:

**M5-10. Images the sync hasn't recorded yet.** After upgrading, the indices must be rebuilt (`tontuyau index rebuild`: new fields). Until the rebuild has run, the documents don't know their images' versions; the API then reads each image's header and looks for its version files on disk (≈ 50 ms for 100 cards on the trial index), so pictures and `srcset` keep working meanwhile. After the rebuild the API never touches the disk for a card.
  a) keep this fallback (today)
  b) no fallback: until the rebuild, the API lists only `[]` (no images)
Default: a)
**Answer:** a), keep the fallback; and make the rebuild automatic when it's needed (options in chat). Automatic rebuild: a), at startup (2026-10-04).

**M5-11. `sizes` of the grid cards.** Home, feeds and channel grids say "400 px wide" (the cards are 300–400 px). At pixel ratio 1 the browser then takes the 720 px version (36 KB) even for a 310 px card; at ratio 2 it takes the original.
  a) keep 400 px (today): always sharp
  b) 360 px: the 360 px version (14 KB) for cards up to 360 px, a little soft on 400 px cards
Default: a)
**Answer:** a), keep 400 px.
