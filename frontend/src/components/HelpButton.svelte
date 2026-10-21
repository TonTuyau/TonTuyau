<script lang="ts">
  // A zone's help (alpha.4): a round "?" at the top right of the zone, opening its explanation in a bubble under it,
  // so the zone shows only what's needed to act. Closed on each visit; the "?" again, Esc (focus back on the "?") or a
  // click outside close it. A disclosure: the button says whether it's open and which bubble it opens.
  import type { Snippet } from 'svelte'
  import { t } from '../lib/i18n/i18n.svelte'

  /** `zone`: the zone's name, for the button's label ("Help: …"). */
  let { zone, children }: { zone: string; children: Snippet } = $props()

  const uid = $props.id()
  const bubbleId = `${uid}-help`

  let open = $state(false)
  let root: HTMLElement | undefined = $state()
  let trigger: HTMLButtonElement | undefined = $state()
  let bubble: HTMLElement | undefined = $state()
  /** How far the bubble moves sideways to stay on the screen (a "?" near a phone's left edge). */
  let shift = $state(0)

  const EDGE = 8

  // Measured once open (shown, not moved yet): moved back inside the window when it sticks out.
  $effect(() => {
    if (!open || !bubble) {
      shift = 0
      return
    }
    const box = bubble.getBoundingClientRect()
    if (box.left < EDGE) {
      shift = EDGE - box.left
    } else if (box.right > window.innerWidth - EDGE) {
      shift = window.innerWidth - EDGE - box.right
    }
  })

  $effect(() => {
    if (!open) {
      return
    }
    const onpointerdown = (event: PointerEvent) => {
      if (root && event.target instanceof Node && !root.contains(event.target)) {
        open = false
      }
    }
    const onkeydown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        open = false
        trigger?.focus()
      }
    }
    document.addEventListener('pointerdown', onpointerdown)
    document.addEventListener('keydown', onkeydown)
    return () => {
      document.removeEventListener('pointerdown', onpointerdown)
      document.removeEventListener('keydown', onkeydown)
    }
  })
</script>

<span class="help" bind:this={root}>
  <button
    bind:this={trigger}
    type="button"
    class="trigger"
    aria-expanded={open}
    aria-controls={bubbleId}
    aria-label={t('help.label', { zone })}
    title={t('help.label', { zone })}
    onclick={() => (open = !open)}
  >
    ?
  </button>
  <div
    bind:this={bubble}
    id={bubbleId}
    class="bubble"
    hidden={!open}
    style:transform={shift === 0 ? undefined : `translateX(${shift}px)`}
  >
    {@render children()}
  </div>
</span>

<style>
  .help {
    position: relative;
    flex: none;
  }

  .trigger {
    display: grid;
    place-items: center;
    width: var(--help-size);
    height: var(--help-size);
    padding: 0;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-round);
    background: none;
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    line-height: 1;
  }

  .trigger:hover,
  .trigger[aria-expanded='true'] {
    border-color: var(--color-text-secondary);
    color: var(--color-text);
  }

  .trigger:focus-visible {
    outline: var(--focus-ring);
  }

  /* Under the "?", its right edge on the button's: the zone's "?" sits at its right, the bubble opens leftwards. */
  .bubble {
    position: absolute;
    top: calc(100% + var(--space-1));
    right: 0;
    z-index: var(--z-menu);
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    width: var(--help-width);
    max-width: calc(100vw - var(--space-8));
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
    color: var(--color-text);
    box-shadow: var(--shadow-menu);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-regular);
    line-height: 1.5;
    text-align: left;
    white-space: normal;
  }

  .bubble[hidden] {
    display: none;
  }

  .bubble :global(p),
  .bubble :global(dl),
  .bubble :global(dd) {
    margin: 0;
  }

  /* Terms and what they mean (a choice's options, a form's fields): each term on its line, its text under it. */
  .bubble :global(dl) {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  /* Set in full: the zone's own dl, dt and dd rules (scoped to its page, which renders the help) reach here too. */
  .bubble :global(dt) {
    color: var(--color-text);
    font-size: inherit;
    font-weight: var(--font-weight-medium);
  }

  .bubble :global(dd) {
    display: block;
    color: var(--color-text-secondary);
  }

  .bubble :global(dd + dt) {
    margin-top: var(--space-1);
  }
</style>
