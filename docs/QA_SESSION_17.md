# TonTuyau — Q&A session 17

> 2026-10-07. After **1.1.0** (no `data:` in the CSP, plain progress lines with a heartbeat, clickable timecodes, the Firefox shorts stall, the shorts overlay; notes in [releases/1.1.0.md](releases/1.1.0.md)).
> Choices I made while building 1.1.0, one per item. Each item has a default (what's built now); write "ok" or your choice on its answer line.

---

**Q17-1. What the CSP check looks at.** `check.sh` now fails on a `data:` URL in the built page and in its stylesheets, and the build never inlines an asset as one. It doesn't read the scripts: a `data:` image created in code (none today) would only show as a blocked image in the browser's console.
  a) as is: the page and the stylesheets
  b) also scan the built scripts for `data:image/` and `data:font/`
Default: a)
**Answer:**

**Q17-2. The rhythm of the progress lines with the heartbeat.** A line is now printed every 10 s while a step runs (the latest state at that moment), and at once at each phase change. With updates every few seconds this gives the same number of lines as before, but each one lands on the 10 s mark and may show the state of a fraction of a second earlier (e.g. `[00:10] … 8 channels` rather than `[00:12] … 12 channels`).
  a) as is: a line every 10 s, on the mark
  b) the heartbeat only after 10 s with no update at all (with steady updates the lines are exactly 1.0.0's; the heartbeat then starts 10 s after the last update instead of the last line)
Default: a)
**Answer:**

**Q17-3. Opening a link with `&t=`.** A middle click on a timecode, or a shared `/watch?v=<id>&t=83`, opens the watch page with the video positioned at 1:23 but paused (the watch page never starts a video by itself, and browsers often block autoplay with sound). A plain click on a timecode, on the page itself, seeks and plays, and leaves the address as it was.
  a) as is: positioned, paused
  b) also start playing (when the browser allows it)
Default: a)
**Answer:**

**Q17-4. The shorts wheel: one short per gesture.** A gesture is a run of wheel events with no pause over 250 ms: one notch, a trackpad swipe with its momentum, or a wheel spun without stopping, moves exactly one short; to move several, pause a quarter of a second between notches (or use ↓ / the buttons). On your deployment (Firefox, PC and mobile), I'd welcome a look once 1.1.0 is running: here the stall came from the shorts left behind still downloading (reproduced over HTTP/1.1, as the front server speaks it); if a short still sticks, a note of the browser and whether the proxy speaks HTTP/1.1 or HTTP/2 would help.
  a) as is: one per gesture
  b) one per notch, with a minimum delay between two moves (a spun wheel moves several shorts; a trackpad swipe may too)
Default: a)
**Answer:**

**Q17-5. The shorts overlay and the browser's controls.** The title overlay now sits on the video's bottom edge, where the browser also draws its own controls (play, time, volume). So that they stay readable, the overlay fades out while they may be showing: the mouse over the video, the video paused, or 3 s after a touch on a touch screen.
  a) as is: the overlay fades out for the controls
  b) the overlay always shown, the controls drawn under it (darker)
  c) the overlay always shown, its text kept 48 px above the bottom (the gradient still reaches the edge, the controls under its darkest part)
Default: a)
**Answer:**
