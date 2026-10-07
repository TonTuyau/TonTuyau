# TonTuyau — Q&A session 05

> 2026-10-03. Before M4 (REST API) and M5a (live mockup).

| # | Question | Answer | Consequence |
|---|---|---|---|
| Q5-1 | M4 or M5a first? | **Both at once**: one agent on M4, another on the M5a mockup | The shared contract is written first ([API_CONTRACT.md](API_CONTRACT.md)); each agent works in its own git worktree and milestone branch (`milestone/m4-api`, `milestone/m5a-front-mockup`) |
| Q5-2 | Search suggestions in the search box? | **Yes** (44k videos) | `GET /api/search/suggest?q=`: channels then video titles, by prefix, accents ignored |
| Q5-3 | Download file name | **The real title**, no ids | `Content-Disposition: attachment; filename="<title>.<ext>"` (+ UTF-8 `filename*`), forbidden characters replaced |
| Q5-4 | `/api/sync/status` on the LAN? | **CLI only** | No sync endpoint over HTTP at all; `tontuyau sync status` stays the only way |
