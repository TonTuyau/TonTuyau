<script lang="ts">
  // A short, in YouTube's vertical format: thumbnail, title, views. No duration overlay (as on YouTube).
  import type { VideoCard } from '../lib/api/types'
  import { formatCompact } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { videoHref } from '../lib/shortsPlayer.svelte'
  import Badge from './Badge.svelte'
  import Thumbnail from './Thumbnail.svelte'
  import { imageSizes } from '../lib/srcset'

  let { video, priority = false }: { video: VideoCard; priority?: boolean } = $props()

  // The vertical shorts player, or the watch page if the user chose "normal" (settings menu).
  const url = $derived(videoHref(video))
</script>

<article class="short" data-testid="short-card">
  <a href={url} tabindex="-1" aria-hidden="true"
    ><Thumbnail
      src={video.thumbnailUrl}
      versions={video.thumbnails}
      sizes={imageSizes.short}
      shape="portrait"
      {priority}
    /></a
  >
  <h3 class="title"><a href={url} title={video.title}>{video.title}</a></h3>
  <p class="meta">{tp('count.views', video.views, formatCompact(video.views, i18n.lang))}</p>
  {#if !video.active}
    <p><Badge hint={t('badge.inactiveHint')}>{t('badge.inactive')}</Badge></p>
  {/if}
</article>

<style>
  .short {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .title {
    display: -webkit-box;
    overflow: hidden;
    margin-top: var(--space-2);
    -webkit-line-clamp: 2;
    line-clamp: 2;
    -webkit-box-orient: vertical;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
    line-height: var(--line-height-tight);
    overflow-wrap: anywhere;
  }

  .meta {
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
  }
</style>
