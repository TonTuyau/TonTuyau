# TonTuyau — Q&A session 08

> 2026-10-04. Front performance, after your Lighthouse runs on the M5a mockup. Decided in chat; recorded here.

## Findings

| Lighthouse item | What it was | Outcome |
|---|---|---|
| Minification, unused JavaScript, long dependency tree, Speed Index 16.6 s | Measured on the **dev server** (≈ 200 unminified modules). The production build is one minified 41 KB (gzip) script | Measure on the production build (`tontuyau-mockup-prod`, port 5181): mobile 99, desktop 100 |
| LCP image lazy-loaded | The first row of thumbnails had `loading="lazy"` | **Fixed in M5a** (`feature/m5a-lcp-eager`): the page's first list loads its first 4 thumbnails eagerly with `fetchpriority="high"`. Mobile LCP 2.4 s → 1.8 s |
| Render-blocking CSS (6 KB) | ≈ 150 ms on simulated slow 4G only | Kept (LAN app) |
| **Image delivery** | Channel pictures are 900×900 shown at 36 px; real video thumbnails are 1280×720 (≈ 95 KB each, ≈ 2.5 MB on Home) | **Q8-1** |
| LCP request not discoverable in the initial document | The first thumbnail's URL comes from `/api/home`, read by the app after it loads (single-page app) | **Q8-2** |

## Decisions

**Q8-1. Smaller images.** Where are the small versions made?
  a) during the sync, when art is copied (≈ +1 GB disk; served as plain files)
  b) on first request, cached on disk
**Answer:** a), during the sync, "so we have them every time, it's way easier". → M5: the sync writes resized variants next to the originals (existing art converted once); contract v1.4 gives the sized URLs so the browser picks one (`srcset`), the front builds nothing. Sizes and format are proposed when M5 starts.

**Q8-2. "LCP request discoverability".**
  a) accept it (a few ms on the LAN; desktop score 100)
  b) preload `/api/home` from the HTML
  c) server-side rendering
**Answer:** a), keep it that way.
