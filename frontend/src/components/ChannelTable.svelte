<script lang="ts">
  // The dashboard's channels: their videos, shorts, streams, those gone from YouTube, private, unlisted, to retry or
  // not checked yet (1.5; each count not 0 in its state's colour), and the total. Its name opens its admin page;
  // "Go to channel" its page in TonTuyau. Sorted by a column's button (again: the other way), filtered by name (accents
  // and case ignored, once typing pauses) and to the channels with videos gone from YouTube. Loaded like a channel's
  // videos on its admin page (alpha.3): the API filters, sorts and sends a page at a time, the next one as it comes
  // into view. A 401 ends the session.
  import type { ChannelTotals, DashboardChannelSort, YouTubeState } from '../lib/api/types'
  import { api } from '../lib/api/client'
  import { auth } from '../lib/auth.svelte'
  import { formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { Pager } from '../lib/remote.svelte'
  import { href } from '../lib/router/routes'
  import Avatar from './Avatar.svelte'
  import Button from './Button.svelte'
  import ErrorState from './ErrorState.svelte'
  import LoadMore from './LoadMore.svelte'
  import Skeleton from './Skeleton.svelte'
  import YouTubeStateBadge from './YouTubeStateBadge.svelte'

  /** Every channel of the library: the count says how many of them are shown. */
  let { count }: { count: number } = $props()

  const SEARCH_AFTER = 300

  const columns: { key: DashboardChannelSort; label: TextKey }[] = [
    { key: 'name', label: 'dashboard.channel' },
    { key: 'videos', label: 'type.video' },
    { key: 'shorts', label: 'type.short' },
    { key: 'streams', label: 'type.stream' },
    { key: 'gone', label: 'dashboard.gone' },
    { key: 'private', label: 'dashboard.private' },
    { key: 'unlisted', label: 'dashboard.unlisted' },
    { key: 'retryLater', label: 'dashboard.retryLater' },
    { key: 'notChecked', label: 'dashboard.notChecked' },
    { key: 'total', label: 'dashboard.total' },
  ]

  const channels = new Pager<ChannelTotals>()
  let sort = $state<DashboardChannelSort>('total')
  let ascending = $state(false)
  let onlyGone = $state(false)
  let typed = $state('')
  let words = $state('')
  $effect(() => {
    const text = typed.trim()
    const timer = setTimeout(() => (words = text), SEARCH_AFTER)
    return () => clearTimeout(timer)
  })

  $effect(() => {
    const query = {
      q: words || undefined,
      sort,
      order: ascending ? ('asc' as const) : ('desc' as const),
      gone: onlyGone || undefined,
    }
    return channels.start((cursor, signal) => api.dashboardChannels({ ...query, cursor }, signal))
  })

  $effect(() => {
    if (channels.error?.status === 401) {
      auth.expired()
    }
  })

  function sortBy(column: DashboardChannelSort) {
    if (sort === column) {
      ascending = !ascending
    } else {
      sort = column
      // Names A to Z first, numbers biggest first.
      ascending = column === 'name'
    }
  }

  const number = (value: number) => formatNumber(value, i18n.lang)
</script>

<!-- A count of a state: in its colour when not 0. -->
{#snippet stateCount(state: YouTubeState, value: number)}
  {#if value > 0}
    <YouTubeStateBadge {state}>{number(value)}</YouTubeStateBadge>
  {:else}
    <span class="zero">{number(value)}</span>
  {/if}
{/snippet}

<div class="tools">
  <input
    class="search"
    type="search"
    bind:value={typed}
    placeholder={t('dashboard.search')}
    aria-label={t('dashboard.search')}
    autocomplete="off"
  />
  <button type="button" class="chip" aria-pressed={onlyGone} onclick={() => (onlyGone = !onlyGone)}>
    {t('dashboard.gone')}
  </button>
  {#if channels.total !== undefined}
    <span class="count" role="status">{tp('dashboard.shown', count, undefined, { shown: channels.total })}</span>
  {/if}
</div>

{#if channels.status === 'loading'}
  <Skeleton layout="page" />
{:else if channels.status === 'error' && channels.error}
  <ErrorState error={channels.error} onretry={() => channels.retry()} />
{:else}
  <div class="table-box">
    <table aria-label={t('dashboard.channels')}>
      <thead>
        <tr>
          {#each columns as column (column.key)}
            <th
              class:num={column.key !== 'name'}
              aria-sort={sort === column.key ? (ascending ? 'ascending' : 'descending') : undefined}
            >
              <button type="button" onclick={() => sortBy(column.key)}>
                {t(column.label)}
                {#if sort === column.key}
                  <span aria-hidden="true">{ascending ? '↑' : '↓'}</span>
                {/if}
              </button>
            </th>
          {/each}
          <th><span class="visually-hidden">{t('dashboard.goToChannel')}</span></th>
        </tr>
      </thead>
      <tbody>
        {#each channels.items as channel (channel.id)}
          <tr>
            <td>
              <a class="channel" href={href.adminChannel(channel.id)}>
                <Avatar src={channel.thumbnailUrl} versions={channel.thumbnails} />
                <span class="name">{channel.name}</span>
              </a>
            </td>
            <td class="num" class:zero={channel.counts.videos === 0}>{number(channel.counts.videos)}</td>
            <td class="num" class:zero={channel.counts.shorts === 0}>{number(channel.counts.shorts)}</td>
            <td class="num" class:zero={channel.counts.streams === 0}>{number(channel.counts.streams)}</td>
            <td class="num">{@render stateCount('gone', channel.gone)}</td>
            <td class="num">{@render stateCount('private', channel.private)}</td>
            <td class="num">{@render stateCount('unlisted', channel.unlisted)}</td>
            <td class="num">{@render stateCount('retryLater', channel.retryLater)}</td>
            <td class="num" class:zero={channel.notChecked === 0}>{number(channel.notChecked)}</td>
            <td class="num"><b>{number(channel.total)}</b></td>
            <td>
              <Button href={href.channel(channel.id)}>
                {t('dashboard.goToChannel')} <span class="visually-hidden">{channel.name}</span>
              </Button>
            </td>
          </tr>
        {:else}
          <tr>
            <td class="empty" colspan={columns.length + 1}>{t('dashboard.noChannel')}</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
  <LoadMore pager={channels} />
{/if}

<style>
  .tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
  }

  .search {
    flex: 1 1 var(--card-min-width);
    max-width: var(--admin-search-max-width);
    height: var(--button-height);
    padding: 0 var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-pill);
    background: var(--color-input-bg);
  }

  .search:focus {
    border-color: var(--color-focus);
    outline: var(--focus-ring);
    outline-offset: 0;
  }

  .chip {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-surface);
    font-weight: var(--font-weight-medium);
  }

  .chip:hover {
    background: var(--color-surface-hover);
  }

  .chip[aria-pressed='true'] {
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
  }

  .count {
    margin-left: auto;
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .table-box {
    /* Positioned: the hidden labels (.visually-hidden, absolute) then scroll with it instead of widening the page. */
    position: relative;
    overflow-x: auto;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-variant-numeric: tabular-nums;
  }

  th {
    padding: 0;
    border-bottom: var(--border-width) solid var(--color-divider);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    text-align: left;
  }

  th button {
    width: 100%;
    padding: var(--space-3) var(--space-4);
    border: none;
    background: none;
    color: inherit;
    text-align: inherit;
  }

  th button:hover,
  th[aria-sort] button {
    color: var(--color-text);
  }

  .num {
    text-align: right;
  }

  td {
    padding: var(--space-2) var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    white-space: nowrap;
  }

  tbody tr:last-child td {
    border-bottom: none;
  }

  tbody tr:hover {
    background: var(--color-surface);
  }

  /* A long name wraps (alpha.3) rather than widening the table past its "Go to channel" button. */
  .channel {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    white-space: normal;
  }

  .name {
    min-width: 10rem;
    font-weight: var(--font-weight-medium);
    overflow-wrap: anywhere;
  }

  .zero {
    color: var(--color-text-secondary);
  }

  .empty {
    padding: var(--space-8);
    color: var(--color-text-secondary);
    text-align: center;
  }

  @media (max-width: 600px) {
    .count {
      width: 100%;
      margin-left: 0;
    }
  }
</style>
