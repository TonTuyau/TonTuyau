<script lang="ts">
  // Every channel of the dashboard: its videos, shorts, streams, those gone from YouTube and the total. Sorted by a
  // column's button (again: the other way), filtered by name (accents and case ignored) and to the channels with videos
  // gone from YouTube. All in the page: the API sent every channel.
  import type { ChannelTotals } from '../lib/api/types'
  import { formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { href } from '../lib/router/routes'
  import Avatar from './Avatar.svelte'

  let { channels }: { channels: ChannelTotals[] } = $props()

  type Column = 'name' | 'videos' | 'shorts' | 'streams' | 'gone' | 'total'

  const columns: { key: Column; label: TextKey }[] = [
    { key: 'name', label: 'dashboard.channel' },
    { key: 'videos', label: 'type.video' },
    { key: 'shorts', label: 'type.short' },
    { key: 'streams', label: 'type.stream' },
    { key: 'gone', label: 'dashboard.gone' },
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
      </tr>
    </thead>
    <tbody>
      {#each shown as channel (channel.id)}
        <tr>
          <td>
            <a class="channel" href={href.channel(channel.id)}>
              <Avatar src={channel.thumbnailUrl} versions={channel.thumbnails} />
              <span class="name">{channel.name}</span>
            </a>
          </td>
          <td class="num" class:zero={channel.counts.videos === 0}>{number(channel.counts.videos)}</td>
          <td class="num" class:zero={channel.counts.shorts === 0}>{number(channel.counts.shorts)}</td>
          <td class="num" class:zero={channel.counts.streams === 0}>{number(channel.counts.streams)}</td>
          <td class="num"><span class="gone" class:some={channel.gone > 0}>{number(channel.gone)}</span></td>
          <td class="num"><b>{number(channel.total)}</b></td>
        </tr>
      {:else}
        <tr>
          <td class="empty" colspan={columns.length}>{t('dashboard.noChannel')}</td>
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

  .gone {
    display: inline-block;
    min-width: var(--space-6);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    text-align: center;
  }

  .gone:not(.some) {
    color: var(--color-text-secondary);
  }

  .gone.some {
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    font-weight: var(--font-weight-medium);
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
