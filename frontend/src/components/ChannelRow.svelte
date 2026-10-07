<script lang="ts">
  // One channel in a list (channels page, search results): avatar, name, subscribers, the API's counts.
  import type { ChannelCard } from '../lib/api/types'
  import { formatCompact } from '../lib/format'
  import { i18n, tp } from '../lib/i18n/i18n.svelte'
  import type { PluralKey } from '../lib/i18n/types'
  import { href } from '../lib/router/routes'
  import Avatar from './Avatar.svelte'

  let { channel }: { channel: ChannelCard } = $props()

  const lang = $derived(i18n.lang)
  const counts: { key: PluralKey; value: number }[] = $derived([
    { key: 'count.videos', value: channel.videoCounts.videos },
    { key: 'count.shorts', value: channel.videoCounts.shorts },
    { key: 'count.streams', value: channel.videoCounts.streams },
  ])
  const shown = $derived(counts.filter((count) => count.value > 0))
</script>

<article class="row" data-testid="channel-row">
  <a class="avatar" href={href.channel(channel.id)} tabindex="-1" aria-hidden="true">
    <Avatar src={channel.thumbnailUrl} versions={channel.thumbnails} size="md" />
  </a>
  <div class="text">
    <h3 class="name"><a href={href.channel(channel.id)}>{channel.name}</a></h3>
    <p class="meta">{tp('count.subscribers', channel.subscribers, formatCompact(channel.subscribers, lang))}</p>
    <p class="meta">
      {#each shown.length > 0 ? shown : counts.slice(0, 1) as count, index (count.key)}
        {index > 0 ? ' • ' : ''}{tp(count.key, count.value, formatCompact(count.value, lang))}
      {/each}
    </p>
  </div>
</article>

<style>
  .row {
    display: flex;
    align-items: center;
    gap: var(--space-4);
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

  .name {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-regular);
    overflow-wrap: anywhere;
  }

  .meta {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }
</style>
