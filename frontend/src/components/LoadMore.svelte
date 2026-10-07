<script lang="ts">
  // Infinite scroll for a Pager: loads the next page when the sentinel comes into view, with a
  // "Show more" button as the keyboard / no-IntersectionObserver fallback.
  import type { Pager } from '../lib/remote.svelte'
  import { t } from '../lib/i18n/i18n.svelte'
  import Button from './Button.svelte'
  import ErrorState from './ErrorState.svelte'

  let { pager }: { pager: Pager<unknown> } = $props()

  let sentinel: HTMLElement | undefined = $state()

  $effect(() => {
    if (!sentinel || typeof IntersectionObserver === 'undefined') {
      return
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting) && !pager.moreError) {
          void pager.loadMore()
        }
      },
      { rootMargin: '600px' },
    )
    observer.observe(sentinel)
    return () => observer.disconnect()
  })
</script>

{#if pager.moreError}
  <ErrorState error={pager.moreError} onretry={() => void pager.loadMore()} />
{:else if pager.hasMore}
  <div class="more" bind:this={sentinel}>
    <Button onclick={() => void pager.loadMore()} disabled={pager.loadingMore}>
      {pager.loadingMore ? t('state.loading') : t('state.loadMore')}
    </Button>
  </div>
{/if}

<style>
  .more {
    display: flex;
    justify-content: center;
    padding: var(--space-6) 0;
  }
</style>
