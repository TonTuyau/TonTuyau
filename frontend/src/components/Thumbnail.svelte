<script lang="ts">
  // A video thumbnail from the API's URL (never built here), with the API's resized versions as `srcset` and the
  // layout's display width as `sizes` (v1.4). No URL, or the image fails: a neutral placeholder.
  // `priority`: above the fold (the first row), loaded at once with high priority instead of lazily (LCP).
  import { formatDuration } from '../lib/format'
  import type { ImageSize } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { srcset } from '../lib/srcset'
  import Icon from './Icon.svelte'

  let {
    src,
    versions = [],
    sizes,
    shape = 'landscape',
    durationSeconds,
    priority = false,
  }: {
    src: string | null
    versions?: readonly ImageSize[]
    sizes?: string
    shape?: 'landscape' | 'portrait'
    durationSeconds?: number
    priority?: boolean
  } = $props()

  let failed = $state(false)
  $effect(() => {
    void src
    failed = false
  })
</script>

<div class="thumbnail {shape}">
  {#if src && !failed}
    <img
      {src}
      srcset={srcset(versions)}
      sizes={srcset(versions) ? sizes : undefined}
      alt=""
      loading={priority ? 'eager' : 'lazy'}
      fetchpriority={priority ? 'high' : 'auto'}
      decoding="async"
      onerror={() => (failed = true)}
    />
  {:else}
    <div class="placeholder" data-testid="thumbnail-placeholder"><Icon name="movie" /></div>
  {/if}
  {#if durationSeconds !== undefined}
    <span class="duration"
      ><span class="visually-hidden">{t('card.duration')}</span>{formatDuration(durationSeconds)}</span
    >
  {/if}
</div>

<style>
  .thumbnail {
    position: relative;
    overflow: hidden;
    width: 100%;
    border-radius: var(--radius-lg);
    background: var(--color-skeleton);
  }

  .landscape {
    aspect-ratio: 16 / 9;
  }

  .portrait {
    aspect-ratio: 9 / 16;
  }

  img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .placeholder {
    display: grid;
    place-items: center;
    width: 100%;
    height: 100%;
    color: var(--color-text-secondary);
  }

  .duration {
    position: absolute;
    right: var(--space-2);
    bottom: var(--space-2);
    padding: 0 var(--space-1);
    border-radius: var(--radius-sm);
    background: var(--color-overlay);
    color: var(--color-on-overlay);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    line-height: var(--badge-height);
  }
</style>
