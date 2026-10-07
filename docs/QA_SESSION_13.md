# TonTuyau — Q&A session 13

> 2026-10-06. Before **M7**: Docker images, the compose stack, images published on **GHCR**. The repo is now on GitHub (private, `Eclegv/TonTuyau`).
> Already decided: 3 containers `tontuyau-es`, `tontuyau-backend`, `tontuyau-frontend` (Q13); the front is a small Node server that serves the built files and forwards `/api`, `/media`, `/art` to the backend, one origin (R2-14); limits ES 4 GB heap / 8 GB / 3 CPU, backend 1 GB / 2 CPU, front 256 MB / 1 CPU, volumes on the VM's local disk (R2-21); LAN only (Q14); the sync is CLI-only, `docker exec tontuyau-backend tontuyau …` (Q5-4).
> Each question has a proposal; write "ok" or your choice on its **Answer:** line.
>
> Answered 2026-10-06.

---

**Q13-1. Where the images are built and pushed to GHCR.**
  a) **GitHub Actions**: on every push to `main` (and on version tags) GitHub builds both images and pushes them to GHCR with the repo's own token. Nothing to install or log in on the laptop; uses the private repo's free Actions minutes (2,000 / month; a build ≈ 5–8 min)
  b) from the laptop: `docker build` + `docker push`, after you log Docker in to GHCR with a token you create (`write:packages`)
Proposal: a)
**Answer:** a), GitHub Actions.

**Q13-2. Run the tests on GitHub too.** The same workflow can run `scripts/check.sh` (backend tests with their Elasticsearch container, front tests) before building, and refuse to publish images when a test fails. ≈ 6 more minutes per push.
  a) yes: test, then build and push only if green
  b) no: build and push only (tests stay on the laptop)
Proposal: a)
**Answer:** a), test first; images are published only when everything passes.

**Q13-3. Who can pull the images.** The repo is private; a GHCR image can be private or public on its own.
  a) **private**: your VM logs in to GHCR once (`docker login ghcr.io` with a read-only token you create) before pulling
  b) public: anyone can pull the images (the code stays private, but an image contains the built app, readable by anyone who pulls it)
Proposal: a)
**Answer:** b), **public** images: the repo itself is planned to become public.

**Q13-4. Image names and tags.** Proposal: `ghcr.io/eclegv/tontuyau-backend` and `ghcr.io/eclegv/tontuyau-frontend`, each tagged:
- `main` + the commit (`sha-ac316e8`) on every push to `main`;
- `0.1.0`, `0.1` and `latest` when you create a version tag (`v0.1.0`). First version: **0.1.0** (pre-1.0: the API and the index can still change).
The compose file on the VM uses `latest` by default, or a pinned version.
**Answer:** **Versioning: one minor version per milestone/step** until 1.0: M5+M6 done → **0.6.0 now**; M7 → 0.7.0; **M8 done → 1.0.0**. Names and tags as proposed (`main` + `sha-…` on each push to `main`; `X.Y.Z`, `X.Y`, `latest` on a `vX.Y.Z` tag).

**Q13-5. Processor architecture.** Your VM is x86-64.
  a) `amd64` only (faster builds)
  b) `amd64` + `arm64` (also runs on a Raspberry Pi / ARM server; builds about twice as long)
Proposal: a)
**Answer:** a), `amd64` only.

**Q13-6. Base images.**
- Backend: Microsoft's `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian slim), running as a **non-root** user, with the `tontuyau` command on the PATH so `docker exec tontuyau-backend tontuyau sync status` works. (The "chiseled" images, without a shell, are smaller but harder to debug and need care with SkiaSharp's native library: not now.)
- Front: `node:24-slim`, non-root, only the built files + the small server (no dev dependencies).
**Answer:** As proposed.

**Q13-7. The video share (NFS, read-only).**
  a) compose mounts the NFS share itself (an `nfs` volume in the compose file: server address + path in `.env`), **read-only**: nothing to configure on the VM besides Docker
  b) you mount the share on the VM (fstab), and compose bind-mounts that folder read-only
Proposal: a)
**Answer:** Neither: the media is **a volume given to the container**, so the stack doesn't care where it comes from (NFS, a local folder, anything Docker can mount). Compose mounts that volume **read-only** at the media root; the README shows how to create it (e.g. an NFS volume, or a bind to a local folder).

**Q13-8. Port on the VM.** The front container is the only one reachable from the LAN (Elasticsearch and the backend stay inside Docker's network).
  a) port **80** (http://<vm-ip>/)
  b) port 8080 (or another, set in `.env`)
Proposal: a) by default, changeable in `.env`
**Answer:** a), port 80 by default, changeable in `.env`.

**Q13-9. The Node server in the front container.** Serves the built files (long cache for hashed files, no cache for `index.html`, every unknown path → `index.html` for the router) and forwards `/api`, `/media`, `/art` to the backend, streaming (video seeking with `Range`).
  a) plain Node (`node:http`, no dependency), ~150 lines, fully tested
  b) a small framework (Fastify + its static and proxy plugins): less code of ours, more dependencies to keep updated
Proposal: a)
**Answer:** a), plain Node, no dependency.

**Q13-10. Updating on the VM.**
  a) by hand: `docker compose pull && docker compose up -d` (documented in the README)
  b) automatic (Watchtower checks GHCR and restarts on a new image)
Proposal: a) (an update can trigger the automatic index rebuild: better when you're watching)
**Answer:** a), by hand.
