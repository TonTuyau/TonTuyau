# TonTuyau — Q&A session 09

> 2026-10-04. Start of M5 (the real front, browse + search). How the sync makes the smaller images you chose in Q8-1. Measured on your real art (trial copy: 44,979 video thumbnails, 116 channels).
> Write "ok" to keep the proposal, or your choice, on each **Answer:** line.

What the art is today:
- video thumbnails: mostly 1280×720 JPEG (≈ 98 KB average; some 640×360 and 480×270), 4.1 GB in total;
- channel pictures: 900×900 JPEG (≈ 100 KB), shown at 24 / 36 / 88 / 160 px;
- banners: ≈ 2560×424 JPEG, shown at page width.

What the front shows: grid cards 300–400 px wide, watch-page side list 168 px, shorts cards 200 px. Screens with a pixel ratio of 2 need twice that.

---

**Q9-1. Format of the small versions.** Same quality setting (80), measured on 99 real thumbnails:

| width | JPEG | WebP |
|---|---|---|
| 360 px | 16 KB | 13 KB |
| 720 px | 49 KB | 34 KB |

  a) WebP: ≈ 30 % smaller, every current browser reads it (the originals stay as they are)
  b) JPEG: same format as the originals
Proposal: a)
**Answer:** a), WebP.

**Q9-2. Video thumbnail sizes.**
  a) 360 and 720 px wide, plus the original: sharp on every screen. Disk: ≈ +2.1 GB in WebP for 45k videos (the originals take 4.1 GB)
  b) 480 px only, plus the original: ≈ +1 GB, a bit soft on high-density screens
Proposal: a)
**Answer:** a), 360 and 720 px plus the original.

**Q9-3. Channel pictures and banners.**
- Channel pictures: 48, 96, 176 and 320 px (≈ 40 KB per channel for the four, against 100 KB for the original alone).
- Banners: 1280 px wide, plus the original.
Proposal: as listed
**Answer:** ok, as listed.

**Q9-4. Existing art.** The sync only copies art that is missing locally. Proposal: the sync also makes the small versions whenever they're missing, so the **next full sync** converts your 45k existing thumbnails once (≈ 10 minutes on 6 cores, measured in Q9-6; first estimate said 5), and from then on each new thumbnail gets its versions when it's copied. No new CLI command.
  a) as proposed
  b) a separate command, `tontuyau art resize`, run once by hand
Proposal: a)
**Answer:** a), the next full sync makes the missing versions; no new command.

**Q9-5. What the API gives (contract v1.4).** The front needs the sizes to let the browser pick (`srcset`); it never builds an image URL itself.
  a) a list next to the current URL: `"thumbnails": [ { "width": 360, "url": "/art/videos/<id>/360" }, { "width": 720, "url": … }, { "width": 1280, "url": "/art/videos/<id>" } ]` (same for channel pictures and banners). `thumbnailUrl` stays (the original, used as the player's poster)
  b) one ready-made `srcset` string per image: `"thumbnailSrcset": "/art/videos/<id>/360 360w, …"`
Proposal: a) (plain data, easy to test; the front only joins it)
**Answer:** a), a list of `{ width, url }` per image (contract v1.4).

**Q9-6. Image library on the backend.** Needs a free licence compatible with MIT.
  a) SkiaSharp (MIT, Google's Skia, used by .NET MAUI)
  b) NetVips (MIT wrapper around libvips, LGPL; the fastest and lightest on memory)
  (ImageSharp is out: its licence is not free for every use.)
Proposal: a)
**Answer:** a), SkiaSharp, after a comparison on 299 real thumbnails (360 + 720 px WebP, q 80, laptop busy with other work):

| | SkiaSharp | NetVips |
|---|---|---|
| 1 thread | 67 ms/image | 55 ms/image |
| 6 threads | 13.8 ms/image | 14.3 ms/image |
| peak memory (6 threads) | 110 MB | 300 MB |
| output (both widths) | 23 KB avg | 22 KB avg |
| native library | 12 MB | 18 MB |
| licence | MIT/BSD only | MIT + bundled LGPL libvips |

Same quality to the eye. Implementation note: compute the resized height carefully (rounding) so no edge line appears, decode once per image for all widths.
