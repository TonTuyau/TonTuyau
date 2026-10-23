# TonTuyau 2.2.0 — plan (draft)

## Storage on the dashboard

A zone on the admin dashboard about disk space, and a column for each channel's size. **A mockup for now**
(`work/2.2.0-storage-mockup`): the front shows it, the mockup's admin side answers it (`/__mock`: log in as admin, then
"Disk": roomy, almost full, not saying); the backend doesn't give it yet.

### What the mockup shows

- **"Storage"**, across the dashboard under the play time and the import history:
  - the free space of the disk the videos folder is on, big ("412 GB free", "of 1 TB, on the videos folder's disk");
  - the disk as a bar: TonTuyau's videos by type (the types' colours), the disk's other files (grey: not TonTuyau's,
    e.g. what TubeArchivist left), the free space (outlined);
  - each part with its size, its share of the disk, and for the types how many files;
  - TonTuyau's own data, on its volumes, in a line: the images, the database, the search index;
  - under a tenth of the disk free, a warning: "Little space left on the disk: the next downloads may not fit";
  - a disk that doesn't say its free space (a share): TonTuyau's videos alone, their shares of them.
- **A "Size" column** at the end of the channels' table: its files' total size, sortable like the others.

### Proposed API (contract v2.12)

- `GET /api/admin/dashboard` gains `storage`:
  - `disk`: `{ total, free }` in bytes, of the videos folder's file system (statvfs / `DriveInfo`); null when it can't
    be read;
  - `types`: `[{ type, bytes, count }]`, the sum of the videos' file sizes by type (PostgreSQL already has each file's
    size);
  - `data`: `{ art, database, index }` in bytes: the art folder, PostgreSQL's `pg_database_size`, Elasticsearch's index
    stats.
- `ChannelTotals` gains `size` (bytes), and `GET /api/admin/dashboard/channels` `sort=size`.

### Open questions

1. The disk: only the videos folder's (as mocked), or also the free space of TonTuyau's own volumes (images, database,
   index), which may be on other disks?
2. The images' size means walking the art folder (tens of thousands of files): measured at each dashboard load, or
   kept and refreshed after each run that adds some?
3. The warning's threshold: a tenth of the disk (as mocked), a fixed size (e.g. 50 GB), or a setting? And should the
   downloads wait (on hold) below it, rather than fail one by one when the disk is full?
4. A channel's size: its total only (as mocked), or by type too (in a tooltip, or three columns)?
