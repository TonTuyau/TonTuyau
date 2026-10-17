<script lang="ts">
  // A state on YouTube as a coloured badge (1.5), the same colours everywhere: on YouTube green, unlisted grey,
  // private orange, gone red, retry later yellow, not checked outlined. `forVisitors`: the words visitors see ("Gone
  // from YouTube"…, its explanation as tooltip), and only for gone, private and unlisted. `children`: other content
  // in its colour (a count).
  import type { Snippet } from 'svelte'
  import type { YouTubeState } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { visitorBadge } from '../lib/youTubeStates'

  let {
    state,
    forVisitors = false,
    children,
  }: { state: YouTubeState; forVisitors?: boolean; children?: Snippet } = $props()

  const visitor = $derived(visitorBadge(state))
</script>

{#if !forVisitors || visitor}
  <span class="state {state}" title={forVisitors ? visitor?.hint : undefined}>
    {#if children}
      {@render children()}
    {:else}
      {forVisitors ? visitor?.label : t(`youTubeState.${state}`)}
    {/if}
  </span>
{/if}

<style>
  .state {
    display: inline-flex;
    align-items: center;
    height: var(--badge-height);
    padding: 0 var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--color-surface);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .onYouTube {
    background: var(--color-success-bg);
    color: var(--color-success);
  }

  .private {
    background: var(--color-private-bg);
    color: var(--color-private);
  }

  .gone {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .retryLater {
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .notChecked {
    background: transparent;
    box-shadow: inset 0 0 0 var(--border-width) var(--color-border);
  }
</style>
