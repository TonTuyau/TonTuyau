<script lang="ts" module>
  /**
   * The storage the dashboard shows (2.2.0, a mockup: the API doesn't give it yet; docs/PLAN_2.2.0.md proposes
   * `storage` in GET /api/admin/dashboard). Sizes in bytes.
   */
  export interface Storage {
    /** The disk the videos folder is on; null when it can't be read (a share that doesn't say). */
    disk: { total: number; free: number } | null
    /** TonTuyau's videos on it, by type: their files' sizes, and how many. */
    types: { type: VideoType; bytes: number; count: number }[]
    /** TonTuyau's own data, on its volumes: the images, the database, the search index. */
    data: { art: number; database: number; index: number }
  }

  /** Under this share of the disk free, the zone warns. */
  export const LOW_FREE_SHARE = 0.1
</script>

<script lang="ts">
  // The dashboard's storage zone (2.2.0 mockup): the free space of the videos folder's disk first, then the disk as a
  // bar (TonTuyau's videos by type, the disk's other files, the free space), each part with its size and its share of
  // the disk, and TonTuyau's own data on its volumes in a line under it. Few free: the zone says so.
  import type { VideoType } from '../lib/api/types'
  import { formatBytes } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import HelpButton from './HelpButton.svelte'

  let { storage }: { storage: Storage } = $props()

  const typeLabels: Record<VideoType, TextKey> = { video: 'type.video', short: 'type.short', stream: 'type.stream' }
  const bytes = (value: number) => formatBytes(value, i18n.lang)
  const percent = $derived(new Intl.NumberFormat(i18n.lang, { style: 'percent', maximumFractionDigits: 1 }))

  const videos = $derived(storage.types.reduce((sum, type) => sum + type.bytes, 0))
  // The whole: the disk when it's known, else TonTuyau's videos alone.
  const whole = $derived(storage.disk?.total ?? videos)
  const other = $derived(storage.disk ? Math.max(0, storage.disk.total - storage.disk.free - videos) : 0)
  const low = $derived(storage.disk !== null && storage.disk.free / storage.disk.total < LOW_FREE_SHARE)
  const share = (value: number) => (whole > 0 ? value / whole : 0)
  /** A share of the disk; a part too small to show as one: "< 0.1%". */
  const shareText = (value: number) =>
    value > 0 && share(value) < 0.001 ? `< ${percent.format(0.001)}` : percent.format(share(value))
</script>

<section class="card" class:low aria-labelledby="storage-title">
  <div class="head">
    <h2 id="storage-title">{t('storage.title')}</h2>
    <HelpButton zone={t('storage.title')}><p>{t('storage.hint')}</p></HelpButton>
  </div>

  {#if storage.disk}
    <p class="big">
      {t('storage.free', { free: bytes(storage.disk.free) })}
      <small>{t('storage.of', { total: bytes(storage.disk.total) })}</small>
    </p>
    {#if low}
      <p class="warning" role="status">{t('storage.low')}</p>
    {/if}
  {:else}
    <p class="big">
      {bytes(videos)} <small>{t('storage.unknownDisk')}</small>
    </p>
  {/if}

  <div class="bar" aria-hidden="true">
    {#each storage.types as type (type.type)}
      <span class="{type.type} part" style:--share={share(type.bytes)}></span>
    {/each}
    {#if storage.disk}
      <span class="other part" style:--share={share(other)}></span>
      <span class="free part" style:--share={share(storage.disk.free)}></span>
    {/if}
  </div>

  <dl class="parts">
    {#each storage.types as type (type.type)}
      <div class="item">
        <span class="{type.type} dot" aria-hidden="true"></span>
        <dt>
          {t(typeLabels[type.type])}
          <small>{tp('storage.count', type.count, type.count.toLocaleString(i18n.lang))}</small>
        </dt>
        <dd>
          {bytes(type.bytes)}
          <small>{shareText(type.bytes)}</small>
        </dd>
      </div>
    {/each}
    {#if storage.disk}
      <div class="item">
        <span class="other dot" aria-hidden="true"></span>
        <dt>{t('storage.other')}</dt>
        <dd>
          {bytes(other)}
          <small>{shareText(other)}</small>
        </dd>
      </div>
      <div class="item">
        <span class="free dot" aria-hidden="true"></span>
        <dt>{t('storage.freeLabel')}</dt>
        <dd>
          {bytes(storage.disk.free)}
          <small>{shareText(storage.disk.free)}</small>
        </dd>
      </div>
    {/if}
  </dl>

  <p class="data">
    {t('storage.data', {
      art: bytes(storage.data.art),
      database: bytes(storage.data.database),
      index: bytes(storage.data.index),
    })}
  </p>
</section>

<style>
  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .big {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .big small {
    margin-left: var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-regular);
  }

  .low .big {
    color: var(--color-on-warning);
  }

  .warning {
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    font-size: var(--font-size-sm);
  }

  /* The disk, left to right: the videos by type, the disk's other files, the free space (outlined). */
  .bar {
    display: flex;
    gap: var(--border-width);
    height: var(--space-3);
    overflow: hidden;
    border-radius: var(--radius-sm);
  }

  .part {
    flex: var(--share) 0 0;
    min-width: var(--border-width);
  }

  .parts {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(var(--meta-min-width), 1fr));
    gap: var(--space-3) var(--space-6);
    margin: 0;
  }

  .item {
    display: grid;
    grid-template-columns: var(--space-3) minmax(0, 1fr);
    grid-template-areas:
      'dot label'
      '. size';
    align-items: center;
    gap: var(--space-1) var(--space-2);
  }

  .item dt {
    grid-area: label;
    font-size: var(--font-size-sm);
  }

  .item dt small,
  .item dd small {
    margin-left: var(--space-1);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-regular);
  }

  .item dd {
    grid-area: size;
    margin: 0;
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .dot {
    grid-area: dot;
    width: var(--space-3);
    height: var(--space-3);
    border-radius: var(--radius-sm);
  }

  .data {
    padding-top: var(--space-3);
    border-top: var(--border-width) solid var(--color-divider);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .video {
    background: var(--color-type-video);
  }

  .short {
    background: var(--color-type-short);
  }

  .stream {
    background: var(--color-type-stream);
  }

  .other {
    background: var(--color-type-unknown);
  }

  /* Outlined in the secondary text colour: the surface grey alone vanished on a dark card. */
  .free {
    background: var(--color-surface);
    box-shadow: inset 0 0 0 var(--border-width) var(--color-text-secondary);
  }
</style>
