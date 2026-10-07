<script lang="ts">
  // /results?q=&type=&duration=&uploaded=&channel=&sort=: the API's search. No type: kind=all (top channels on
  // the first page + videos); a video type: kind=videos; "channel": kind=channels. Filters panel as on YouTube.
  import { api } from '../lib/api/client'
  import type { ChannelCard, ChannelDetail, VideoCard } from '../lib/api/types'
  import { t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { mergeQuery } from '../lib/router/routes'
  import {
    apiFilters,
    durationOptions,
    readResultsQuery,
    sortOptions,
    typeOptions,
    uploadOptions,
  } from '../lib/searchFilters'
  import { pageTitle } from '../lib/title.svelte'
  import ChannelFilter from '../components/ChannelFilter.svelte'
  import ChannelRow from '../components/ChannelRow.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Icon from '../components/Icon.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import VideoList from '../components/VideoList.svelte'

  const query = $derived(readResultsQuery(router.query))
  const channelsOnly = $derived(query.type === 'channel')

  const videos = new Pager<VideoCard>()
  const channels = new Pager<ChannelCard>()
  let topChannels = $state<ChannelCard[]>([])

  $effect(() => {
    const current = query
    if (!current.q) {
      return
    }
    const filters = apiFilters(current)
    if (current.type === 'channel') {
      return channels.start((cursor, signal) => api.searchChannels({ q: current.q, cursor }, signal))
    }
    if (current.type) {
      return videos.start((cursor, signal) => api.searchVideos({ q: current.q, ...filters, cursor }, signal))
    }
    return videos.start(async (cursor, signal) => {
      const result = await api.searchAll({ q: current.q, ...filters, cursor }, signal)
      if (cursor === null) {
        topChannels = result.channels
      }
      return result.videos
    })
  })

  // The channel filter chip shows the channel's name, which only the API knows.
  const filterChannel = new Remote<ChannelDetail>()
  $effect(() => {
    const id = query.channel
    if (id) {
      return filterChannel.run((signal) => api.channel(id, signal))
    }
  })

  let panelOpen = $state(false)

  /** The URL with one parameter changed (null removes it); choosing the selected option again removes it. */
  function link(key: string, value: string | null): string {
    const selected = router.query.get(key) === value
    return `/results${mergeQuery(router.search, { [key]: selected ? null : value })}`
  }

  const sortLabels = { relevance: 'sort.relevance', latest: 'sort.uploadDate', popular: 'sort.viewCount' } as const
  const typeLabel = (type: (typeof typeOptions)[number]): TextKey =>
    type === 'channel' ? 'filter.type.channel' : `type.one.${type}`

  const active = $derived(
    [
      query.uploaded && { key: 'uploaded', label: t(`filter.${query.uploaded}`) },
      query.type && { key: 'type', label: t(typeLabel(query.type)) },
      query.duration && { key: 'duration', label: t(`filter.duration.${query.duration}`) },
      query.channel && {
        key: 'channel',
        label: filterChannel.data?.id === query.channel ? filterChannel.data.name : query.channel,
      },
      query.sort !== 'relevance' && { key: 'sort', label: t(sortLabels[query.sort]) },
    ].filter((chip): chip is { key: string; label: string } => Boolean(chip)),
  )

  const pager = $derived(channelsOnly ? channels : videos)
  const nothing = $derived(
    pager.status === 'ready' && pager.items.length === 0 && (channelsOnly || topChannels.length === 0),
  )

  pageTitle(() => (query.q ? t('results.title', { q: query.q }) : null))
</script>

{#snippet option(key: string, value: string, label: string, disabled = false)}
  <li>
    {#if disabled}
      <span class="option disabled" aria-disabled="true">{label}</span>
    {:else}
      <a
        class="option"
        href={link(key, value)}
        aria-current={router.query.get(key) === value || (key === 'sort' && value === query.sort) ? 'true' : undefined}
        >{label}</a
      >
    {/if}
  </li>
{/snippet}

<div class="page">
  {#if !query.q}
    <StateMessage icon="search" title={t('results.noQuery')} role="status" />
  {:else}
    <div class="toolbar">
      <button
        type="button"
        class="filters-button"
        aria-expanded={panelOpen}
        aria-controls="search-filters"
        onclick={() => (panelOpen = !panelOpen)}
      >
        <Icon name="tune" />{t('results.filters')}
      </button>
      {#if pager.status === 'ready' && pager.total !== undefined}
        <p class="total">{tp('count.results', pager.total)}</p>
      {/if}
    </div>

    {#if active.length > 0}
      <ul class="active" aria-label={t('results.activeFilters')}>
        {#each active as chip (chip.key)}
          <li>
            <a class="chip" href={link(chip.key, null)} aria-label={t('results.removeFilter', { name: chip.label })}
              >{chip.label}<Icon name="close" /></a
            >
          </li>
        {/each}
        <li><a class="clear" href={`/results${mergeQuery('', { q: query.q })}`}>{t('results.clearFilters')}</a></li>
      </ul>
    {/if}

    <div id="search-filters" class="panel" hidden={!panelOpen}>
      <section>
        <h2>{t('filter.uploadDate')}</h2>
        <ul>
          {#each uploadOptions as value (value)}
            {@render option('uploaded', value, t(`filter.${value}`), channelsOnly)}
          {/each}
        </ul>
      </section>
      <section>
        <h2>{t('filter.type')}</h2>
        <ul>
          {#each typeOptions as value (value)}
            {@render option('type', value, t(typeLabel(value)))}
          {/each}
        </ul>
      </section>
      <section>
        <h2>{t('filter.duration')}</h2>
        <ul>
          {#each durationOptions as value (value)}
            {@render option('duration', value, t(`filter.duration.${value}`), channelsOnly)}
          {/each}
        </ul>
      </section>
      <section>
        <h2>{t('filter.channel')}</h2>
        <ChannelFilter
          hrefFor={(id) => `/results${mergeQuery(router.search, { channel: id })}`}
          disabled={channelsOnly}
        />
      </section>
      <section>
        <h2>{t('sort.label')}</h2>
        <ul>
          {#each sortOptions as value (value)}
            {@render option('sort', value, t(sortLabels[value]), channelsOnly)}
          {/each}
        </ul>
      </section>
    </div>

    {#if pager.status === 'loading'}
      <Skeleton layout="rows" count={5} />
    {:else if pager.status === 'error' && pager.error}
      <ErrorState error={pager.error} onretry={() => pager.retry()} />
    {:else if nothing}
      <StateMessage
        icon="search"
        title={t('results.noResults', { q: query.q })}
        detail={t('results.noResultsHint')}
        role="status"
      />
    {:else if channelsOnly}
      <div class="channels">
        {#each channels.items as channel (channel.id)}
          <ChannelRow {channel} />
        {/each}
      </div>
      <LoadMore pager={channels} />
    {:else}
      {#if !query.type && topChannels.length > 0}
        <section class="group" aria-labelledby="results-channels">
          <h2 id="results-channels" class="visually-hidden">{t('results.channels')}</h2>
          <div class="channels">
            {#each topChannels as channel (channel.id)}
              <ChannelRow {channel} />
            {/each}
          </div>
        </section>
      {/if}
      <section class="group" aria-labelledby="results-videos">
        <h2 id="results-videos" class="visually-hidden">{t('results.videos')}</h2>
        <VideoList videos={videos.items} layout="rows" eagerCount={4} />
      </section>
      <LoadMore pager={videos} />
    {/if}
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    max-width: var(--page-max-width);
    margin: 0 auto;
    padding: var(--space-4) var(--space-6) var(--space-6);
  }

  .toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .filters-button {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    height: var(--button-height);
    padding: 0 var(--space-4);
    border: none;
    border-radius: var(--radius-pill);
    background: transparent;
    font-weight: var(--font-weight-medium);
  }

  .filters-button:hover,
  .filters-button[aria-expanded='true'] {
    background: var(--color-surface);
  }

  .total {
    color: var(--color-text-secondary);
  }

  .active {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    height: var(--chip-height);
    padding: 0 var(--space-2) 0 var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
    font-weight: var(--font-weight-medium);
    --icon-size: var(--font-size-lg);
  }

  .clear {
    padding: 0 var(--space-2);
    color: var(--color-link);
  }

  .panel {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(calc(var(--avatar-lg) * 1.1), 1fr));
    gap: var(--space-6);
    padding: var(--space-4) 0;
    border-bottom: var(--border-width) solid var(--color-divider);
  }

  .panel[hidden] {
    display: none;
  }

  .panel h2 {
    padding-bottom: var(--space-3);
    margin-bottom: var(--space-2);
    border-bottom: var(--border-width) solid var(--color-divider);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-bold);
    text-transform: uppercase;
  }

  .panel ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .option {
    display: block;
    padding: var(--space-2) 0;
    color: var(--color-text-secondary);
  }

  a.option:hover {
    color: var(--color-text);
  }

  .option[aria-current='true'] {
    color: var(--color-text);
    font-weight: var(--font-weight-bold);
  }

  .option.disabled {
    opacity: 0.4;
  }

  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .channels {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-bottom: var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-3);
    }
  }
</style>
