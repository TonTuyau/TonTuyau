# TonTuyau 2.2.0 — plan (draft)

## Storage on the dashboard

A zone on the admin dashboard about the videos folder's disk, a column for each channel's size, and a threshold under
which the downloads can wait. **A mockup for now** (`work/2.2.0-storage-mockup`): the front shows it, the mockup's
admin side answers it (`/__mock`: log in as admin, then "Disk": roomy, almost full, not saying); the backend doesn't
give it yet.

### Decided (2026-10-23)

1. **Only the videos folder's disk**: TonTuyau's own data (images, database, index) isn't shown.
2. **No cache needed**: the videos' sizes are in PostgreSQL already (a sum by type, by channel, at each load), the
   disk's free space is one system call.
3. **A threshold, a setting** (the YouTube page, "Downloading" → "Disk space"): "Warn under [50] GB free" (0: never),
   and a switch "Below it, don't run the downloads" (on by default).
4. **A channel's size**: its total in the dashboard's table (sortable); by type on its admin page.

### What the mockup shows

- **"Storage"**, across the dashboard under the play time and the import history:
  - the free space of the videos folder's disk, big ("412 GB free", "of 1 TB, on the videos folder's disk");
  - the disk as a bar: TonTuyau's videos by type (the types' colours), the disk's other files (grey: not TonTuyau's,
    e.g. what TubeArchivist left), the free space (outlined);
  - each part with its size, its share of the disk ("< 0.1%" for a part too small), the types with their files' count;
  - under the threshold, a warning: "Under 50 GB free: the downloads don't run until space is freed" (the switch on),
    or "…: the next downloads may not fit" (off);
  - a disk that doesn't say its free space (a share): TonTuyau's videos alone, their shares of them, no warning.
- **A "Size" column** at the end of the channels' table, sortable.
- **A channel's admin page**: "Size" under "In TonTuyau", its total and by type (the types it has files of).
- **The YouTube page**, "Downloading": the threshold in GB and the switch, said under Save.
- **The Tasks page**: under the threshold, the switch on, the YouTube syncs can't be started, and say why.

### Proposed backend (contract v2.12)

- `GET /api/admin/dashboard` gains `storage`: `disk` (`{ total, free }` in bytes, the videos folder's file system,
  `DriveInfo`; null when it can't be read), `types` (`[{ type, bytes, count }]`, the videos' file sizes summed by
  type), `minFree` (bytes) and `holdDownloads` (the settings, for the warning).
- `ChannelTotals` gains `size` (bytes); `GET /api/admin/dashboard/channels` takes `sort=size`. `AdminChannelResponse`
  gains `size` (`{ videos, shorts, streams }` in bytes).
- The YouTube settings gain `minFreeGb` (0 or more; 50 by default) and `holdBelowMinFree` (true by default).
- **The downloads below it** (the switch on): the YouTube syncs, "Download a video" (and LaPompe's), "Try again" and
  the full sync don't start: `unavailable` says why on the Tasks page, a run asked for is refused (409, saying the free
  space and the threshold). A sync already running checks before each download, and goes **on hold** when it crosses
  the threshold ("Under 50 GB free"), resumed by Resume now or by itself once space is freed (looked at every
  minute, as the schedule). The refreshes, the import and "Add a channel" don't download: they still run.
