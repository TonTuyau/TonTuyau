<script lang="ts">
  // Every channel of the dashboard: its videos, shorts, streams, those gone from YouTube, private, unlisted, to retry
  // or not checked yet (1.5; each count not 0 in its state's colour), and the total. Its name opens its admin page;
  // "Go to channel" its page in TonTuyau. Sorted by a column's button (again: the other way), filtered by name (accents
  // and case ignored) and to the channels with videos gone from YouTube. All in the page: the API sent every channel.
  // The list scrolls in its own box, its header on top, so its sideways scroll bar stays in sight.
  import type { ChannelTotals, YouTubeState } from '../lib/api/types'
  import { formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { href } from '../lib/router/routes'
  import Avatar from './Avatar.svelte'
  import Button from './Button.svelte'
  import YouTubeStateBadge from './YouTubeStateBadge.svelte'

  let { channels }: { channels: ChannelTotals[] } = $props()

  type Column =
    'name' | 'videos' | 'shorts' | 'streams' | 'gone' | 'private' | 'unlisted' | 'retryLater' | 'notChecked' | 'total'

  const columns: { key: Column; label: TextKey }[] = [
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

  let sort = $state<Column>('total')
  let ascending = $state(false)
  let query = $state('')
  let onlyGone = $state(false)

  const fold = (text: string) =>
    text
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase()

  function value(channel: ChannelTotals, column: Column): number {
    switch (column) {
      case 'videos':
      case 'shorts':
      case 'streams':
        return channel.counts[column]
      case 'gone':
        return channel.gone
      case 'private':
        return channel.private
      case 'unlisted':
        return channel.unlisted
      case 'retryLater':
        return channel.retryLater
      case 'notChecked':
        return channel.notChecked
      default:
        return channel.total
    }
  }

  const collator = $derived(new Intl.Collator(i18n.lang, { sensitivity: 'base' }))

  const shown = $derived.by(() => {
    const words = fold(query.trim())
    const direction = ascending ? 1 : -1
    return channels
      .filter((channel) => fold(channel.name).includes(words) && (!onlyGone || channel.gone > 0))
      .sort(
        (a, b) => direction * (sort === 'name' ? collator.compare(a.name, b.name) : value(a, sort) - value(b, sort)),
      )
  })

  function sortBy(column: Column) {
    if (sort === column) {
      ascending = !ascending
    } else {
      sort = column
      // Names A to Z first, numbers biggest first.
      ascending = column === 'name'
    }
  }

  const number = (count: number) => formatNumber(count, i18n.lang)
</script>

<!-- A count of a state: in its colour when not 0. -->
{#snippet stateCount(state: YouTubeState, count: number)}
  {#if count > 0}
    <YouTubeStateBadge {state}>{number(count)}</YouTubeStateBadge>
  {:else}
    <span class="zero">{number(count)}</span>
  {/if}
{/snippet}

<div class="tools">
  <input
    class="search"
    type="search"
    bind:value={query}
    placeholder={t('dashboard.search')}
    aria-label={t('dashboard.search')}
    autocomplete="off"
  />
  <button type="button" class="chip" aria-pressed={onlyGone} onclick={() => (onlyGone = !onlyGone)}>
    {t('dashboard.gone')}
  </button>
  <span class="count" role="status">{tp('dashboard.shown', channels.length, undefined, { shown: shown.length })}</span>
</div>

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
      {#each shown as channel (channel.id)}
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
    max-height: 70vh;
    overflow: auto;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-variant-numeric: tabular-nums;
  }

  th {
    position: sticky;
    top: 0;
    z-index: 1;
    padding: 0;
    /* A shadow, not a border: a collapsed table's borders don't stay with a sticky cell. */
    box-shadow: inset 0 calc(-1 * var(--border-width)) 0 var(--color-divider);
    background: var(--color-bg);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    text-align: left;
    white-space: nowrap;
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

  .channel {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .name {
    font-weight: var(--font-weight-medium);
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
