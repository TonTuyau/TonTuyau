<script lang="ts">
  // /feed/videos|shorts|streams?sort=: the whole library of one type (/api/videos), cursor-paged.
  import { api } from '../lib/api/client'
  import type { VideoCard, VideoSort, VideoType } from '../lib/api/types'
  import { t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'
  import Chips from '../components/Chips.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import VideoList from '../components/VideoList.svelte'

  let { type }: { type: VideoType } = $props()

  const sorts: VideoSort[] = ['latest', 'popular', 'oldest']
  const sort = $derived(sorts.find((value) => value === router.query.get('sort')) ?? 'latest')
  const layout = $derived(type === 'short' ? 'shorts' : 'grid')

  const pager = new Pager<VideoCard>()
  $effect(() => {
    const current = type
    const order = sort
    return pager.start((cursor, signal) => api.videos({ type: current, sort: order, cursor }, signal))
  })

  pageTitle(() => t(`feed.title.${type}`))
</script>

<div class="page">
  <header class="head">
    <h1>{t(`feed.title.${type}`)}</h1>
    {#if pager.status === 'ready' && pager.total !== undefined}
      <p class="total">{tp('count.results', pager.total)}</p>
    {/if}
  </header>
  <Chips
    label={t('sort.label')}
    options={sorts.map((value) => ({ value, label: t(`sort.${value}`) }))}
    selected={sort}
    hrefFor={(value) => href.feed(type, value === 'latest' ? null : value)}
  />

  {#if pager.status === 'loading'}
    <Skeleton {layout} />
  {:else if pager.status === 'error' && pager.error}
    <ErrorState error={pager.error} onretry={() => pager.retry()} />
  {:else if pager.items.length === 0}
    <StateMessage title={t('state.empty')} role="status" />
  {:else}
    <VideoList videos={pager.items} {layout} eagerCount={4} />
    <LoadMore {pager} />
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
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

  @media (max-width: 600px) {
    .page {
      padding: var(--space-3);
    }
  }
</style>
