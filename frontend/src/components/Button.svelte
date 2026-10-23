<script lang="ts">
  // Pill button (YouTube style). `href` makes it a link (e.g. Download; `external` opens another site in a new tab).
  // Two kinds only (2.1.0), the same on every page: `primary`, the zone's one main action, in TonTuyau's teal; and
  // `secondary` (the default), every other action, grey, made lighter in the dark theme so it stands out on a card.
  import type { Snippet } from 'svelte'

  let {
    href,
    download,
    external = false,
    type = 'button',
    variant = 'secondary',
    onclick,
    disabled = false,
    children,
  }: {
    href?: string
    download?: boolean
    /** A link to another site: a new tab, no referrer. */
    external?: boolean
    /** `submit` in a form (1.5: creating an API key). */
    type?: 'button' | 'submit'
    variant?: 'primary' | 'secondary'
    onclick?: () => void
    disabled?: boolean
    children: Snippet
  } = $props()
</script>

{#if href}
  <a
    class="button {variant}"
    {href}
    download={download ? '' : undefined}
    target={external ? '_blank' : undefined}
    rel={external ? 'noreferrer noopener' : undefined}>{@render children()}</a
  >
{:else}
  <button {type} class="button {variant}" {onclick} {disabled}>{@render children()}</button>
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

  .secondary {
    background: var(--color-button-secondary);
    color: var(--color-text);
  }

  .secondary:hover:not(:disabled) {
    background: var(--color-button-secondary-hover);
  }

  .primary {
    background: var(--color-button-primary);
    color: var(--color-on-button-primary);
  }

  .primary:hover:not(:disabled) {
    background: var(--color-button-primary-hover);
  }

  .button:disabled {
    opacity: 0.5;
    cursor: default;
  }
</style>
