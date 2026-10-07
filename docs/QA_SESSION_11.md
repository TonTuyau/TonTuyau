# TonTuyau — Q&A session 11

> 2026-10-04. Questions from the QA10 round of M5 (search prefix, generated API types, automatic index rebuild). Notes: [M5-front.md, QA10 round](milestones/M5-front.md#qa10-round).
> Each says what is built today (the default). Write "ok" to keep it, or your choice, on the answer line under it.

---

**M5-12. A switch of its own for the automatic rebuild.** The startup rebuild runs only when the scheduler is on (`TONTUYAU_SYNC_SCHEDULER_ENABLED`): a backend that may sync by itself may also rebuild by itself, and the review stack (scheduler off) never calls TubeArchivist by itself.
  a) no separate switch (today)
  b) add `TONTUYAU_INDEX_AUTO_REBUILD` (default on): the startup rebuild needs both switches on
Default: a)
**Answer:** (after the explanation in chat: a rebuild re-reads the catalogue from TubeArchivist into a fresh index with the new layout, then switches the site to it) a), no separate switch.

**M5-13. The search box and the last word.** The API treats the last word as complete when the text ends with a space ("creme " doesn't find "crémeux"). The front trims the search box, so from the UI the last word is always also matched as the start of a word (a whole-word match still ranks first).
  a) keep trimming (today)
  b) send the text as typed, trailing space included (the page URL then keeps the space too)
Default: a)
**Answer:** a), keep trimming: "Creme should find cremeux". Follow-up: the last word also matches as the start of a word **in descriptions and tags too**, with the field weights of M5-14 (title above description) and a whole word still above a start-of-word match in the same field.

**M5-14. A start-of-word match in a title against a whole word in a description.** Within one field a whole word always ranks above a start-of-word match ("tarte": "Tarte aux pommes" before "Tartelettes"). Across fields, a title starting with the word can rank above a video that has the whole word only in its description, as the title counts three times more.
  a) as built: the title's weight wins
  b) any whole-word match ranks above every start-of-word match, whatever the field
Default: a)
**Answer:** a), the title's weight wins.

**M5-15. Saying "index rebuild running" on the readiness endpoint.** The readiness check knows (its description says "Index rebuild pending / running"), but `/health/ready` answers only `Healthy` or `Unhealthy`, the ASP.NET Core default.
  a) keep the plain answer (today); `tontuyau sync status` and the logs say it
  b) answer a small JSON with each check's status and description
Default: a)
**Answer:** a), keep the plain answer.
