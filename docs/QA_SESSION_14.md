# TonTuyau — Q&A session 14

> 2026-10-06. After **M7** (Docker images, the compose stack, GHCR through GitHub Actions; notes in [milestones/M7-docker.md](milestones/M7-docker.md)).
> These are the choices I made while building M7 without asking, plus what's left for you to do. Each item has a default (what's built now); write "ok" or your choice on its answer line.

---

**Q14-1. The backend's base image is Ubuntu, not Debian.** QA13-6 said `mcr.microsoft.com/dotnet/aspnet:10.0` "(Debian slim)", but since .NET 10 that tag is **Ubuntu 24.04** ("noble"), and Microsoft publishes **no Debian image for .NET 10** at all (the variants are Ubuntu 24.04 / 26.04, their "chiseled" shell-less versions, Alpine and Azure Linux). It works: tzdata, ICU, bash for the healthcheck, and SkiaSharp resizes in it (checked in the container). Alpine would need SkiaSharp's musl build; chiseled was set aside in QA13-6.
  a) keep `aspnet:10.0` (Ubuntu 24.04), as named in QA13
  b) pin `aspnet:10.0-noble` explicitly, so a future default change (Ubuntu 26.04) doesn't arrive silently
Default: a)
**Answer:** a), Ubuntu 24.04 (`aspnet:10.0`).

**Q14-2. The version on every backend answer.** Besides `tontuyau --version`, the image labels and the settings menu, the backend adds `X-TonTuyau-Version: 0.7.0` to **every** answer (one middleware line; the API contract has a one-line note about it, informative only). The other option was a field on `/health`, which would change its plain "Healthy" body.
  a) keep the header on every answer
  b) only on `/health`
Default: a)
**Answer:** a), the version header on every answer.

**Q14-3. Security headers of the front server.** It sends `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` (the LAN address never leaks to YouTube links in descriptions) and `X-Frame-Options: SAMEORIGIN`, and adds them to the backend's answers when missing. **No Content-Security-Policy**: `index.html` has a small inline script (the theme before the first paint) that a CSP would have to allow by hash, kept in sync with the build.
  a) as is; a CSP with the "Before going public" work
  b) add a CSP now
Default: a)
**Answer:** b) **act as if it were already public**: add a Content-Security-Policy now (in 0.7.1).

**Q14-4. No compression.** The app is 171 KB (50 KB gzipped), cached for a year after the first visit, on the LAN: the front server doesn't gzip or brotli. Videos and WebP images don't compress anyway.
  a) no compression
  b) precompress `.js` / `.css` / `.html` at build time (gzip + brotli) and serve them by `Accept-Encoding`
Default: a)
**Answer:** a), no compression.

**Q14-5. The backend's healthcheck asks `/health/ready`** (Elasticsearch usable and TonTuyau's indices there), so the front starts only once the backend can answer. If Elasticsearch goes down later, the backend shows "unhealthy" in `docker compose ps` (Docker doesn't restart it for that; it recovers by itself when Elasticsearch is back).
  a) `/health/ready`
  b) `/health` (the process answers, nothing more)
Default: a)
**Answer:** b), the compose healthcheck asks the generic `/health`: other clients may use this API, so a generic health is better (in 0.7.1).

**Q14-6. What `.env` can change in the stack.** The paths inside the backend container (`/youtube`, `/data/art`, `/data/logs/sync`) and Elasticsearch's address are fixed by the compose file: the `TONTUYAU_MEDIA_ROOT`, `TONTUYAU_ART_ROOT`, `TONTUYAU_SYNC_LOG_ROOT`, `TONTUYAU_ES_URL` lines of `.env` are only for running the backend outside Docker. The resource limits (R2-21) are fixed in the compose file too, not in `.env` (a smaller machine uses an override file, as I did on the laptop: [M7 notes](milestones/M7-docker.md#local-end-to-end-check)).
  a) as is
  b) also expose the Elasticsearch heap and memory limit in `.env`
Default: a)
**Answer:** a).

**Q14-7. Elasticsearch on localhost for development.** The M0 compose file published Elasticsearch on `127.0.0.1:9200` for `dotnet run`. The stack no longer publishes it; [`docker/compose.dev.yml`](../docker/compose.dev.yml) adds it back for development (`… -f docker/docker-compose.yml -f docker/compose.dev.yml up -d tontuyau-es`, in the README).
  a) as is (an override file)
  b) a Compose profile instead
Default: a)
**Answer:** a).

**Q14-8. Tagging v0.7.0.** `latest` on GHCR only exists once a `vX.Y.Z` tag is pushed, and the compose file uses `latest` by default. After you review and merge M7 into `main`: tag `v0.7.0` on that merge and push it (`git tag v0.7.0 <merge> && git push origin v0.7.0`); the workflow then publishes `0.7.0`, `0.7` and `latest`. I never push `main` or tags.
  a) you tag after the merge, as described
  b) something else
Default: a)
**Answer:** Asked why both `0.7.0` and `0.7` (explained in chat: `0.7` is a moving tag, always the newest 0.7.x; `0.7.0` never moves). Kept as proposed.

**Q14-9. The images become public: your one-time step.** GHCR creates a new package as **private**, and only the package's owner can change that (a workflow token can't). After the first push from `main` (or the `v0.7.0` tag), for **each** of `tontuyau-backend` and `tontuyau-frontend`: github.com → your profile → **Packages** → the package → **Package settings** → **Danger Zone → Change visibility → Public**. While there, check **"Repository source"** / **"Manage Actions access"** shows `Eclegv/TonTuyau` (the images carry the `org.opencontainers.image.source` label, so GitHub should link them by itself; if not, "Connect repository"). Also in the README ("Images on GHCR").
Default: you do it once after the first publish
**Answer:** You make both packages public once the images are published.

**Q14-10. Two flaky backend scenarios.** In eight runs of `check.sh` during M7, two failed once each, then passed on the rerun (and every other time), in scenarios M7 doesn't touch:
- `Sync.feature`, "A channel with more than the window of one type: imported as far as possible, never cleaned": its second full sync said "Alias …-channels doesn't exist (indices not created yet)" right after a first sync had succeeded;
- `Cli.feature`, "Status shows the watermark, the lock, the indices and the last runs": `tontuyau sync status` printed no "Index videos" line after a first sync.

Both look like the scenario's indices not being visible for a moment, probably Elasticsearch slow under load (the laptop was swapping: 9 GB of swap in use). CI on GitHub passed. Worth a look, but outside M7.
  a) a separate fix after M7 (start by running those two scenarios in a loop under load)
  b) look now, in M7
Default: a)
**Answer:** M7 is merged: fix it separately now (0.7.1).
