# TonTuyau — Q&A session 02

> 2026-10-01. Round 2 answers (recorded inline in [QA_SESSION_01.md](QA_SESSION_01.md)) and what they change, plus round 3 questions (**answered 2026-10-01**; consequences in [QA_SESSION_03.md](QA_SESSION_03.md)).
> [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) has been updated to v3.

---

## Round 2: what the answers change

| # | Answer | Consequence |
|---|---|---|
| R2-1 | You install Docker + join the `docker` group | Reqnroll integration tests (Testcontainers ES) and image builds run on the laptop once it's there. Until then, M0's Reqnroll placeholder runs without containers |
| R2-2 | API-level Reqnroll for v1 | No Playwright in v1 |
| R2-3 | I write the `.feature` files, you review; English | `.feature` files are part of every milestone review |
| R2-4 | Anonymise fixtures | `tools/record-fixtures` rewrites names/titles/descriptions/tags/ids consistently, keeping structure and edge cases (empty arrays, nulls, `active:false`, mixed date formats) |
| R2-5 | Download a handful of real videos | Candidates picked, **waiting for your OK on the list → R3-2** |
| R2-6 | Images copied as-is | JPEG as in TA's cache |
| R2-7 | Full: Saturday night 1 AM Paris; incremental: every night 1 AM | Both are nightly, so **no daytime sync**: new downloads show up the next morning. Two details are ambiguous → R3-1 |
| R2-8 | CLI: `sync incremental`, `sync full`, `sync status`, `index rebuild` | |
| R2-9 | Inactive videos shown with a badge | `active` indexed; badge label in i18n |
| R2-10 | All 115 channels, subscription status ignored | |
| R2-11 | **Playlists out of v1** | Removed from API, ES indices, front pages and milestones. `playlist[]` ids on videos are still kept in the index (cheap, avoids a re-import later). Tell me if you'd rather drop them too |
| R2-12 | Subtitles out | |
| R2-13 | Codec check in the browser + message + download | |
| R2-14 | Node prod server + proxy, one origin | |
| R2-15 | 400 + ProblemDetails; broken TA items skipped + reported; repository/gateway naming | Skip-and-report is an input to the sync design session |
| R2-16 | Token-based themes, build-time, light/dark from system | |
| R2-17 | Browser language + switcher, `localStorage`, own typed dictionary, `Intl` | |
| R2-18 | Search: title, channel, tags, description; FR + EN analyzers; edge-ngram; filters type/channel/date/duration | |
| R2-19 | Home = latest published, sections per type; YouTube channel sorts | |
| R2-20 | Branch flow as proposed **+ an MD doc per milestone** | `docs/milestones/MN-<name>.md`, written on the milestone branch, part of the review (template below) |
| R2-21 | 6 cores, volumes on local VM disk | Compose: ES 4 GB heap / 8 GB limit, 3 CPUs; backend 1 GB / 2 CPUs; front 256 MB / 1 CPU |
| R2-22 | **TA page size raised to 100** | Verified: `page_size: 100` now. Full walk ≈ 450 requests. The code still reads `page_size` from each response and never assumes it |
| R2-23 | Keep the key for now | Regenerate before any public exposure (noted in README "Before going public") |
| R2-24 | ES 9.x, security on | `ELASTIC_PASSWORD` in `.env` |
| R2-25 | Tooling as proposed | |
| R2-26 | Nothing more | |

### New finding while checking R2-22

- TA v0.5.12 **rejects `sort=filesize`** (`"filesize" is not a valid choice`), though the source docstring lists it. Valid: `published`, `downloaded`, `views`, `likes`, `duration`. The TA client will only offer those, with a test that pins the list.
- TA serves media files at `<TA_URL>/youtube/<channel_id>/<video_id>.mp4` (with the token, `Content-Type: video/mp4`). That's what the fixture downloader will use.

### Milestone doc template (R2-20)

```markdown
# MN — <name>
Branch: milestone/mN-<name> · Features: feature/mN-a, feature/mN-b
## What was built
## How to check it (commands, expected output)
## Tests added (unit / Reqnroll features)
## Decisions taken during the milestone (and why)
## Deviations from the plan
## Known limitations / follow-ups
```

---

## Round 3: questions

**R3-1. Night schedule details.**
  a) "Saturday night 1 AM": **Saturday 01:00** (the night Friday→Saturday) or **Sunday 01:00** (the night Saturday→Sunday)?
  b) On that night both runs are due at 01:00. Run only the full one, or both one after the other? (How a full run relates to "new" videos is for the sync design session; this is only about the schedule.)
  c) OK that new downloads only appear the next morning? (An extra daytime incremental would cost almost nothing on TA.)
Default: a) Sunday 01:00; b) only the full run that night; c) yes, nightly only, as you said.
**Answer:** Move incremental to daytime if it doesn't cost much. Weekly full: the night Friday → Saturday, 1 AM.

**R3-2. Real videos to download** into `dev-data/youtube/` (git-ignored), from your TA, read-only `GET <TA_URL>/youtube/<channel_id>/<video_id>.mp4`. The smallest file per type/codec combination:

| Video id | Type | Video | Audio | Active | Size | Duration |
|---|---|---|---|---|---|---|
| F_61JlzUoZI | video | h264 | aac | yes | 0.4 MB | 1 s |
| aWDiP-ZaCfI | video | h264 | opus | yes | 0.4 MB | 10 s |
| dZqjMzlXXmI | video | vp9 | opus | yes | 0.5 MB | 11 s |
| mSoPTxdVTnw | video | av1 | opus | yes | 0.4 MB | 14 s |
| QF4xxljhWIA | video | h264 | opus | **no** | 0.2 MB | 3 s |
| CGN06utNWMA | short | av1 | opus | yes | 0.3 MB | 4 s |
| c5MEzCXA2sI | short | vp9 | aac | yes | 0.6 MB | 7 s |
| WkMFCS0IUf0 | stream | h264 | aac | yes | 10.6 MB | 3 min 47 |
| dp1thcnPbiM | stream | vp9 | opus | yes | 111.8 MB | 36 min |
| NsQQOtJ2iaA | stream | av1 | opus | yes | 61.5 MB | 11 min |

**Total ≈ 187 MB**, plus their thumbnails and their channels' thumb/banner/tvart (a few MB).
Optional: one real **4K AV1** video (`y07hZ0XvwUg`, 1.25 GB, 32 min) to test playback load and seeking in a big file.
Default: the 10 files + images, without the 4K one.
**Answer:** The 10 files + images + the 4K one.

**R3-3. Fixture anonymisation vs. dev media.** Committed test fixtures are anonymised (R2-4). The local dev stack (dev-data, git-ignored) uses your **real** metadata so the downloaded files match. OK?
Default: yes.
**Answer:** Yes.

**R3-4. Starting order.** I start **M0 (skeleton)** now, then M1 (TA client) and M2 (search store), while you prepare the sync design. OK, or do you want to review M0 before I go on to M1?
Default: go on; you review each milestone branch when its doc is ready.
**Answer:** The sync design gets its own file and a whole discussion. Process: I review a milestone, you merge, you start the next one, so we avoid merges/rebases for little details that could have been spotted before. (2026-10-01: M0 reviewed → merge and start M1.)
