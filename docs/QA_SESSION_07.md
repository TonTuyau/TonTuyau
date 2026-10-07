# TonTuyau — Q&A session 07

> 2026-10-03. Questions raised while applying QA06 to the M5a mockup (shorts player, phone bottom bar, links). Each says what the mockup does today (the default); try it at http://localhost:5180 and write "ok" or your choice on the **Answer:** line.

Answered 2026-10-03.

---

**M5a-18. Which short comes next in the vertical player.** You open a short from Home; pressing ↓ (or scrolling) shows another one. Today the next ones are **that short's channel's other shorts**, latest first.
  a) same channel's shorts (today): the API already supports it (`/api/channels/{id}/videos?type=short&exclude=<id>`)
  b) all shorts of the library, latest first, like Home's Shorts section: needs `exclude` on `/api/videos` too (small contract change)
Default: a)
**Answer:** b), all shorts of the library, latest first. → contract v1.3: `exclude` on `/api/videos`.

**M5a-19. The browser's Back button in the shorts player.** You open a short, then scroll through 5 more.
  a) Back leaves the player and returns where you came from (today: the URL is replaced at each short)
  b) Back goes to the previous short (one history entry per short; 5 Backs to leave)
Default: a)
**Answer:** a), Back leaves the player.

**M5a-20. "Shorts" in the side menu / bottom bar** opens the `/feed/shorts` grid. **Already settled** by your QA06 answer to M5a-9 (keep the feed pages): nothing to answer.

**M5a-21. Phone layout (≤ 600 px).** Today the bottom bar has five entries (Home, Shorts, Channels, Videos, Streams), and the ☰ button at the top left still opens the side menu. YouTube's phone app has no ☰ (everything is in the bottom bar).
  a) keep both (today)
  b) remove ☰ on phones: the bottom bar is the only navigation
Default: a)
**Answer:** b), no ☰ on phones: the bottom bar is the only navigation.

**M5a-22. Clickable links in channel descriptions.** You asked for links in video descriptions; the mockup also makes them clickable in a channel's description (channel page, "About").
  a) both (today)
  b) video descriptions only
Default: a)
**Answer:** a), links in both video and channel descriptions.
