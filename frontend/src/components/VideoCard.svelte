<script lang="ts">
  // One video in a list. `grid`: home/channel grids; `row`: search results (thumbnail left);
  // `compact`: the watch page side column. Shows exactly the VideoCard fields, formatted.
  import type { VideoCard } from '../lib/api/types'
  import { formatAge, formatCompact } from '../lib/format'
  import { i18n, tp } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
  import { videoHref } from '../lib/shortsPlayer.svelte'
  import Avatar from './Avatar.svelte'
  import YouTubeStateBadge from './YouTubeStateBadge.svelte'
  import Thumbnail from './Thumbnail.svelte'
  import { imageSizes } from '../lib/srcset'
  import { isVisitorState } from '../lib/youTubeStates'

  let {
    video,
    variant = 'grid',
    priority = false,
  }: { video: VideoCard; variant?: 'grid' | 'row' | 'compact'; priority?: boolean } = $props()

  const watchUrl = $derived(videoHref(video))
  const channelUrl = $derived(href.channel(video.channel.id))
  const views = $derived(tp('count.views', video.views, formatCompact(video.views, i18n.lang)))
  const age = $derived(formatAge(video.published, i18n.lang))
</script>

<article class="card {variant}" data-testid="video-card">
  <a class="thumb" href={watchUrl} tabindex="-1" aria-hidden="true">
    <Thumbnail
      src={video.thumbnailUrl}
      versions={video.thumbnails}
      sizes={imageSizes[variant]}
      durationSeconds={video.durationSeconds}
      {priority}
    />
  </a>
  <div class="details">
    {#if variant === 'grid'}
      <a class="avatar" href={channelUrl} tabindex="-1" aria-hidden="true"
        ><Avatar src={video.channel.thumbnailUrl} versions={video.channel.thumbnails} /></a
      >
    {/if}
    <div class="text">
      <h3 class="title"><a href={watchUrl} title={video.title}>{video.title}</a></h3>
      {#if variant === 'row'}
        <p class="meta">{views} • <time datetime={video.published}>{age}</time></p>
        <a class="channel row-channel" href={channelUrl}
          ><Avatar src={video.channel.thumbnailUrl} versions={video.channel.thumbnails} size="xs" />{video.channel
            .name}</a
        >
      {:else}
        <a class="channel" href={channelUrl}>{video.channel.name}</a>
        <p class="meta">{views} • <time datetime={video.published}>{age}</time></p>
      {/if}
      {#if isVisitorState(video.youTubeState)}
        <p class="badge"><YouTubeStateBadge state={video.youTubeState} forVisitors /></p>
      {/if}
    </div>
  </div>
</article>

<style>
  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .details {
    display: flex;
    gap: var(--space-3);
    min-width: 0;
  }

  .avatar {
    flex: none;
  }

  .text {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .title {
    display: -webkit-box;
    overflow: hidden;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    -webkit-box-orient: vertical;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
    line-height: var(--line-height-tight);
    overflow-wrap: anywhere;
  }

  .channel,
  .meta {
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
  }

  .channel {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .channel:hover {
    color: var(--color-text);
  }

  .row-channel {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) 0;
  }

  .row {
    flex-direction: row;
    gap: var(--space-4);
  }

  .row .thumb {
    flex: none;
    width: min(var(--row-thumb-width), 45%);
  }

  .row .title {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-regular);
  }

  .compact {
    flex-direction: row;
    gap: var(--space-2);
  }

  .compact .thumb {
    flex: none;
    width: var(--compact-thumb-width);
  }

  .compact .title {
    font-size: var(--font-size-sm);
  }

  .compact .channel,
  .compact .meta {
    font-size: var(--font-size-xs);
  }

  @media (max-width: 600px) {
    .row {
      flex-direction: column;
      gap: var(--space-3);
    }

    .row .thumb {
      width: 100%;
    }
  }
</style>
