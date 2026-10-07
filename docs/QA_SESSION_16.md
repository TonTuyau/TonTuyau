# TonTuyau — Q&A session 16

> 2026-10-07. After **0.7.3** (progress for command-line syncs, the grids on very wide screens, the watch page's side list, the README for a folder of videos; notes in [releases/1.0.0.md](releases/1.0.0.md)).
> Choices I made while building 0.7.3, and one request. Each item has a default (what's built now); write "ok" or your choice on its answer line.

---

**Q16-1. In a terminal: one line, or one line per phase?** With `docker exec -it`, the progress is one line rewritten in place, as you asked; when a phase ends, its last counts disappear under the next phase's line (the report has the totals).
  a) as is: one line for the whole run
  b) one line per phase: the line is updated in place while the phase runs, then kept, and the next phase starts a new line (about 6 lines for a full sync, like a summary of the steps)
Default: a)
**Answer:** Asked whether it still matters given Q16-2 (explained in chat, 2026-10-07): open.

**Q16-2. How often the terminal line changes.** Both modes print at each phase change and at most every 10 s. Plain lines need that limit (logs); a line rewritten in place costs nothing on screen.
  a) every 10 s in both modes
  b) every second in a terminal (the counts visibly move), every 10 s for plain lines
Default: a)
**Answer:** a), every 10 s in both modes.

**Q16-3. A heartbeat during a long step.** A line is printed when the sync reports something new (per page, per image, per deletion…), so during one long step with nothing to report (a slow TubeArchivist answer, the index switch of a rebuild, the deletion planning), nothing prints until it ends. On your library these steps take seconds, so it shouldn't show.
  a) as is
  b) a timer re-prints the current line every 10 s even when nothing moved (the time keeps going up, so you can tell it's alive)
Default: a)
**Answer:** b), a heartbeat: the current line is re-printed every 10 s even when nothing moved. → 1.1.0

**Q16-4. Warnings in the middle of the terminal line.** The CLI still shows warnings and errors (e.g. "Couldn't copy art …: retried at the next run"). In a terminal such a message lands right after the progress text, on the same line, then the progress continues on the next line. Plain-lines mode (and so `| tee`) isn't affected: the message is simply a line between two progress lines.
  a) accept it
  b) the CLI ends the progress line before each log message (the log goes through the progress, a bit more code in the CLI host)
Default: a)
**Answer:** a), accept it.

**Q16-5. A look at a real run.** I didn't run anything against your TubeArchivist; the progress was checked through the tests and their fake TubeArchivist. At your next full sync on the VM, after upgrading to 0.7.3, could you run it as `docker exec tontuyau-backend tontuyau sync full | tee sync-0.7.3.log` and send me the file? I'd check the number of lines (I expect about 130 for a first full sync, far fewer for a later one), the phases and the counts against the report.
  a) yes, at the next full sync
  b) not needed
Default: a)
**Answer:** Done: the VM's full sync log was sent (27 min 22 s, 177 lines in all).

**Q16-6. How many columns on very wide screens.** The video grids now stop at 6 columns and the shorts grids at 10; past that the cards grow (at 3840 px: video cards about 600 px wide, short cards about 360 px wide and 640 px tall). Below about 2,000 px nothing changed.
  a) 6 and 10
  b) more columns, smaller cards (e.g. 8 and 12)
  c) fewer, bigger cards (e.g. 5 and 8)
Default: a)
**Answer:** a), 6 and 10.

**Q16-7. The 1284 px pages are now centred on very wide screens.** The channel, channels and search pages (and the watch page) keep their width, but they used to be centred in the old 2200 px area, so on a 3840 px screen they sat on the left; now they're centred in the whole width, like YouTube.
  a) centred in the whole width
  b) back where they were
Default: a)
**Answer:** a), centred in the whole width.

**Q16-8. Tagging v0.7.3.** As before: after you merge `release/0.7.3` into `main` through the pull request, the tag `v0.7.3` goes on that merge (`git tag v0.7.3 <merge> && git push origin v0.7.3`); the workflow then publishes `0.7.3` and moves `0.7` and `latest`. I never push `main` or tags unless you ask.
  a) you tag after the merge
  b) I tag it after you tell me it's merged (as for v0.7.1 and v0.7.2)
Default: b)
**Answer:** b), I tag after you tell me it's merged.
