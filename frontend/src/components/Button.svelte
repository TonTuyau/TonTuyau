<script lang="ts">
  // Pill button (YouTube style). `href` makes it a link (e.g. Download), `variant` picks the colours.
  import type { Snippet } from 'svelte'

  let {
    href,
    download,
    variant = 'tonal',
    onclick,
    disabled = false,
    children,
  }: {
    href?: string
    download?: boolean
    variant?: 'tonal' | 'filled'
    onclick?: () => void
    disabled?: boolean
    children: Snippet
  } = $props()
</script>

{#if href}
  <a class="button {variant}" {href} download={download ? '' : undefined}>{@render children()}</a>
{:else}
  <button type="button" class="button {variant}" {onclick} {disabled}>{@render children()}</button>
{/if}

<style>
  .button {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    height: var(--button-height);
    padding: 0 var(--space-4);
    border: none;
    border-radius: var(--radius-pill);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-medium);
    white-space: nowrap;
  }

  .tonal {
    background: var(--color-surface);
    color: var(--color-text);
  }

  .tonal:hover {
    background: var(--color-surface-hover);
  }

  .filled {
    background: var(--color-button);
    color: var(--color-on-button);
  }

  .button:disabled {
    opacity: 0.5;
    cursor: default;
  }
</style>
