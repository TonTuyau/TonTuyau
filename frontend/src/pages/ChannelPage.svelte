<script lang="ts">
  // /channel/<id>[/videos|shorts|streams]?sort=: banner, avatar, name, subscribers, description, then the tabs
  // the API's videoCounts allow (> 0, as the contract says), each a cursor-paged list in the chosen sort.
  import { api } from '../lib/api/client'
  import { SEARCH_MAX_LENGTH } from '../lib/api/limits'
  import type { ChannelDetail, VideoCard, VideoSort } from '../lib/api/types'
  import { missingResource } from '../lib/errors'
  import { formatCompact } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { PluralKey } from '../lib/i18n/types'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { feedSegments, href, type ChannelTab } from '../lib/router/routes'
  import { imageSizes, srcset } from '../lib/srcset'
  import { pageTitle } from '../lib/title.svelte'
  import Avatar from '../components/Avatar.svelte'
  import Badge from '../components/Badge.svelte'
  import Chips from '../components/Chips.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import ExpandableText from '../components/ExpandableText.svelte'
  import Icon from '../components/Icon.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import MissingState from '../components/MissingState.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import VideoList from '../components/VideoList.svelte'

  let { id, tab }: { id: string; tab: ChannelTab | null } = $props()

  const sorts: VideoSort[] = ['latest', 'popular', 'oldest']
  const sort = $derived(sorts.find((value) => value === router.query.get('sort')) ?? 'latest')

  const remote = new Remote<ChannelDetail>()
  $effect(() => {
    const channelId = id
    return remote.run((signal) => api.channel(channelId, signal))
  })
  const channel = $derived(remote.status === 'ready' && remote.data?.id === id ? remote.data : undefined)

  const tabKeys: Record<ChannelTab, { label: 'type.video' | 'type.short' | 'type.stream'; count: PluralKey }> = {
    videos: { label: 'type.video', count: 'count.videos' },
    shorts: { label: 'type.short', count: 'count.shorts' },
    streams: { label: 'type.stream', count: 'count.streams' },
  }
  const tabs = $derived(
    channel ? (['videos', 'shorts', 'streams'] as const).filter((name) => channel.videoCounts[name] > 0) : [],
  )
  const active = $derived(tab && tabs.includes(tab) ? tab : tabs[0])

  const pager = new Pager<VideoCard>()
  $effect(() => {
    const channelId = channel?.id
    const current = active
    const order = sort
    if (channelId && current) {
      return pager.start((cursor, signal) =>
        api.channelVideos(channelId, { type: feedSegments[current], sort: order, cursor }, signal),
      )
    }
  })

  let query = $state('')
  function search(event: SubmitEvent) {
    event.preventDefault()
    if (query.trim()) {
      router.navigate(href.results({ q: query.trim(), channel: id }))
    }
  }

  const missing = $derived(remote.status === 'error' && remote.error ? missingResource(remote.error, 'channel') : null)

  pageTitle(() => channel?.name ?? (missing ? t(missing.title) : null))
  const lang = $derived(i18n.lang)
  const meta = $derived(
    channel
      ? [
          tp('count.subscribers', channel.subscribers, formatCompact(channel.subscribers, lang)),
          ...tabs.map((name) =>
            tp(tabKeys[name].count, channel.videoCounts[name], formatCompact(channel.videoCounts[name], lang)),
          ),
        ].join(' • ')
      : '',
  )
</script>

{#if missing}
  <MissingState title={t(missing.title)} detail={t(missing.hint)} />
{:else if remote.status === 'error' && remote.error}
  <ErrorState error={remote.error} onretry={() => remote.retry()} />
{:else if !channel}
  <div class="page"><Skeleton layout="page" /></div>
{:else}
  <div class="page">
    {#if channel.bannerUrl}
      <img
        class="banner"
        src={channel.bannerUrl}
        srcset={srcset(channel.banners)}
        sizes={srcset(channel.banners) ? imageSizes.banner : undefined}
        alt=""
      />
    {/if}

    <header class="head">
      <Avatar src={channel.thumbnailUrl} versions={channel.thumbnails} size="lg" />
      <div class="info">
        <h1>{channel.name}</h1>
        <p class="meta">{meta}</p>
        {#if !channel.active}
          <p class="inactive">
            <Badge hint={t('badge.channelInactiveHint')}>{t('badge.channelInactive')}</Badge>
            <span class="hint">{t('badge.channelInactiveHint')}</span>
          </p>
        {/if}
        {#if channel.description}
          <div class="description">
            <ExpandableText text={channel.description} lines={2} more={t('channel.more')} less={t('channel.less')} />
          </div>
        {/if}
      </div>
    </header>

    {#if tabs.length === 0}
      <StateMessage icon="videos" title={t('channel.noContent')} role="status" />
    {:else}
      <div class="tabbar">
        <nav class="tabs" aria-label={t('channel.tabs')}>
          {#each tabs as name (name)}
            <a class="tab" href={href.channel(channel.id, name)} aria-current={name === active ? 'page' : undefined}
              >{t(tabKeys[name].label)}</a
            >
          {/each}
        </nav>
        <form class="search" role="search" onsubmit={search}>
          <label>
            <Icon name="search" />
            <span class="visually-hidden">{t('channel.searchIn')}</span>
            <input type="search" bind:value={query} maxlength={SEARCH_MAX_LENGTH} placeholder={t('channel.searchIn')} />
          </label>
        </form>
      </div>

      <Chips
        label={t('sort.label')}
        options={sorts.map((value) => ({ value, label: t(`sort.${value}`) }))}
        selected={sort}
        hrefFor={(value) => href.channel(channel.id, tab, value === 'latest' ? null : value)}
      />

      {#if pager.status === 'loading'}
        <Skeleton layout={active === 'shorts' ? 'shorts' : 'grid'} />
      {:else if pager.status === 'error' && pager.error}
        <ErrorState error={pager.error} onretry={() => pager.retry()} />
      {:else if pager.items.length === 0}
        <StateMessage icon="videos" title={t('state.empty')} role="status" />
      {:else}
        <VideoList videos={pager.items} layout={active === 'shorts' ? 'shorts' : 'grid'} eagerCount={4} />
        <LoadMore {pager} />
      {/if}
    {/if}
  </div>
{/if}

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--page-max-width);
    margin: 0 auto;
    padding: var(--space-4) var(--space-6) var(--space-6);
  }

  .banner {
    width: 100%;
    aspect-ratio: var(--banner-aspect);
    object-fit: cover;
    border-radius: var(--radius-xl);
    background: var(--color-skeleton);
  }

  .head {
    display: flex;
    align-items: center;
    gap: var(--space-6);
  }

  .info {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    min-width: 0;
  }

  h1 {
    font-size: var(--font-size-xxl);
    font-weight: var(--font-weight-bold);
    line-height: var(--line-height-tight);
    overflow-wrap: anywhere;
  }

  .meta {
    color: var(--color-text-secondary);
  }

  .inactive {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .hint {
    font-size: var(--font-size-xs);
  }

  .description {
    max-width: 80ch;
    color: var(--color-text-secondary);
  }

  .tabbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
  }

  .tabs {
    display: flex;
    gap: var(--space-6);
  }

  .tab {
    padding: var(--space-3) 0;
    border-bottom: var(--space-1) solid transparent;
    color: var(--color-text-secondary);
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .tab:hover {
    color: var(--color-text);
  }

  .tab[aria-current='page'] {
    border-bottom-color: var(--color-text);
    color: var(--color-text);
  }

  .search label {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--color-text-secondary);
  }

  .search input {
    width: calc(var(--avatar-lg) * 1.4);
    padding: var(--space-1) 0;
    border: none;
    border-bottom: var(--border-width) solid var(--color-border);
    outline: none;
    background: transparent;
    color: var(--color-text);
  }

  .search input:focus {
    border-bottom-color: var(--color-text);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-3);
    }

    .head {
      flex-direction: column;
      align-items: flex-start;
      gap: var(--space-3);
      --avatar-lg: var(--avatar-md);
    }
  }
</style>
