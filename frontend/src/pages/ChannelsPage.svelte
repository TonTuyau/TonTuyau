<script lang="ts">
  // /channels?q=&sort=: every channel, filtered as you type by the API (q), sorted by the API, paged by cursor.
  import { untrack } from 'svelte'
  import { api } from '../lib/api/client'
  import { CHANNEL_FILTER_MAX_LENGTH } from '../lib/api/limits'
  import type { ChannelCard, ChannelSort } from '../lib/api/types'
  import { t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'
  import ChannelRow from '../components/ChannelRow.svelte'
  import Chips from '../components/Chips.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Icon from '../components/Icon.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'

  let { debounceMs = 250 }: { debounceMs?: number } = $props()

  const sorts: ChannelSort[] = ['name', 'subscribers']
  const q = $derived(router.query.get('q') ?? '')
  const sort = $derived(sorts.find((value) => value === router.query.get('sort')) ?? 'name')

  const pager = new Pager<ChannelCard>()
  $effect(() => {
    const filter = q.trim()
    const order = sort
    return pager.start((cursor, signal) => api.channels({ q: filter, sort: order, cursor }, signal))
  })

  // The box follows the URL (back/forward), and typing updates the URL after a short pause.
  let text = $state('')
  $effect(() => {
    const value = q
    if (untrack(() => text.trim()) !== value) {
      text = value
    }
  })
  let timer: ReturnType<typeof setTimeout> | undefined
  $effect(() => () => clearTimeout(timer))
  function oninput() {
    clearTimeout(timer)
    timer = setTimeout(() => router.setQuery({ q: text.trim() }, { replace: true }), debounceMs)
  }

  pageTitle(() => t('channels.title'))
</script>

<div class="page">
  <header class="head">
    <h1>{t('channels.title')}</h1>
    {#if pager.total !== undefined && pager.status === 'ready'}
      <p class="total">{tp('count.channels', pager.total)}</p>
    {/if}
  </header>

  <div class="tools">
    <label class="filter">
      <Icon name="search" />
      <span class="visually-hidden">{t('channels.filter')}</span>
      <input
        type="search"
        bind:value={text}
        {oninput}
        maxlength={CHANNEL_FILTER_MAX_LENGTH}
        placeholder={t('channels.filterPlaceholder')}
      />
    </label>
    <Chips
      label={t('sort.label')}
      options={sorts.map((value) => ({ value, label: t(`sort.${value}`) }))}
      selected={sort}
      hrefFor={(value) => href.channels({ q: q || null, sort: value === 'name' ? null : value })}
    />
  </div>

  {#if pager.status === 'loading' && pager.items.length === 0}
    <Skeleton layout="rows" count={6} />
  {:else if pager.status === 'error' && pager.error}
    <ErrorState error={pager.error} onretry={() => pager.retry()} />
  {:else if pager.status === 'ready' && pager.items.length === 0}
    <StateMessage
      icon="channels"
      title={q.trim() ? t('channels.noMatch', { q: q.trim() }) : t('channels.empty')}
      role="status"
    />
  {:else}
    <div class="list" class:stale={pager.status === 'loading'} aria-busy={pager.status === 'loading'}>
      {#each pager.items as channel (channel.id)}
        <ChannelRow {channel} />
      {/each}
    </div>
    <LoadMore {pager} />
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--page-max-width);
    margin: 0 auto;
    padding: var(--space-6);
  }

  .head {
    display: flex;
    align-items: baseline;
    gap: var(--space-4);
  }

  h1 {
    font-size: var(--font-size-xxl);
    font-weight: var(--font-weight-bold);
  }

  .total {
    color: var(--color-text-secondary);
  }

  .tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
  }

  .filter {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    width: min(var(--search-max-width), 100%);
    height: var(--search-height);
    padding: 0 var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-pill);
    background: var(--color-input-bg);
    color: var(--color-text-secondary);
  }

  .filter:focus-within {
    border-color: var(--color-focus);
  }

  .filter input {
    flex: 1;
    min-width: 0;
    border: none;
    outline: none;
    background: transparent;
    color: var(--color-text);
    font-size: var(--font-size-md);
  }

  .list {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(min(var(--card-min-width), 100%), 1fr));
    gap: var(--space-6) var(--space-4);
  }

  .stale {
    opacity: 0.5;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-3);
    }
  }
</style>
