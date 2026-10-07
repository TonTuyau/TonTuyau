<script lang="ts">
  // A list of videos in the API's order: a grid of cards, the vertical shorts grid, or result rows.
  // `eagerCount`: how many leading thumbnails are above the fold (the page's first list only).
  import type { VideoCard as Video } from '../lib/api/types'
  import ShortCard from './ShortCard.svelte'
  import VideoCard from './VideoCard.svelte'

  let {
    videos,
    layout = 'grid',
    eagerCount = 0,
  }: { videos: Video[]; layout?: 'grid' | 'shorts' | 'rows'; eagerCount?: number } = $props()
</script>

<div class="list {layout}">
  {#each videos as video, index (video.id)}
    {#if layout === 'shorts'}
      <ShortCard {video} priority={index < eagerCount} />
    {:else}
      <VideoCard {video} variant={layout === 'rows' ? 'row' : 'grid'} priority={index < eagerCount} />
    {/if}
  {/each}
</div>

<style>
  .list {
    display: grid;
    gap: var(--space-10) var(--grid-gap-x);
  }

  .grid {
    grid-template-columns: var(--grid-columns);
  }

  .shorts {
    grid-template-columns: var(--shorts-columns);
    gap: var(--space-6) var(--shorts-gap-x);
  }

  .rows {
    grid-template-columns: 1fr;
    gap: var(--space-4);
  }
</style>
