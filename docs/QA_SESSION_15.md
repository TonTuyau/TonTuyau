# TonTuyau — Q&A session 15

> 2026-10-06. After **0.7.1** (the CSP, the generic healthcheck, the flaky scenarios; notes in [releases/1.0.0.md](releases/1.0.0.md)).
> Choices I made while building 0.7.1, plus your step after the merge. Each item has a default (what's built now); write "ok" or your choice on its answer line.

---

**Q15-1. What the app says while the library isn't usable yet.** With the generic `/health` (Q14-5), the front can start before Elasticsearch and the indices are usable, and Elasticsearch can go down later while everything else stays "healthy". Meanwhile every `/api` call answers **500** (ProblemDetails), and the app shows "The server ran into a problem — Try again in a moment. If it keeps happening, the server's log has the details." with a **Retry** button (checked: Retry works once the backend answers again). In the stack this window is short at startup (the backend starts after Elasticsearch is healthy and creates its indices in a second or two).
  a) as is
  b) the backend answers **503** with `Retry-After` when Elasticsearch or the indices aren't usable, and the app says "TonTuyau is starting or its search index is unavailable" and retries by itself (a contract change: a new status and a new error state)
Default: a)
**Answer:**

**Q15-2. `data:` images in the CSP.** The policy has `img-src 'self' data:` as you specified. Nothing in the app uses a `data:` image today.
  a) keep `data:` (room for an inline placeholder later)
  b) drop it: `img-src 'self'`, the strictest
Default: a)
**Answer:**

**Q15-3. Security headers on the backend itself.** The CSP and the other security headers are sent by the front server, also on the backend's answers it forwards. The backend's own port isn't published by the stack, so today nothing reaches it directly. Q14-5 says other clients may use the API: if its port is ever published, its answers would have no `nosniff`, no CSP, no `frame-ancestors`.
  a) as is (only through the front)
  b) the backend also sends `X-Content-Type-Options: nosniff` and, as a JSON API, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` on its own answers (the front keeps its own policy on what it serves)
Default: a)
**Answer:**

**Q15-4. Tagging v0.7.1.** As for 0.7.0 (Q14-8): after you merge `release/0.7.1` into `main` through the pull request, tag `v0.7.1` on that merge and push it (`git tag v0.7.1 <merge> && git push origin v0.7.1`); the workflow then publishes `0.7.1`, moves `0.7` and `latest`. I never push `main` or tags.
  a) you tag after the merge, as described
  b) something else
Default: a)
**Answer:**
