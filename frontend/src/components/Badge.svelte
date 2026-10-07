<script lang="ts">
  // A small warning badge, e.g. "Unavailable on YouTube" for `active: false` (R2-9). `hint` is its tooltip.
  // (`active: false` only means TubeArchivist / yt-dlp can't reach the video any more: deleted, private,
  // region-blocked or taken down, we don't know which.)
  import type { Snippet } from 'svelte'
  import Icon from './Icon.svelte'

  let { hint, children }: { hint?: string; children: Snippet } = $props()
</script>

<span class="badge" title={hint}><Icon name="warning" />{@render children()}</span>

<style>
  .badge {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    height: var(--badge-height);
    padding: 0 var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    white-space: nowrap;
    --icon-size: var(--font-size-md);
  }
</style>
