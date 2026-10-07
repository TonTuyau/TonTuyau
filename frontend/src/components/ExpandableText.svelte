<script lang="ts">
  // Text clamped to a few lines with "…more" / "Show less". `extra` is shown only when expanded
  // (tags, file details on the watch page). The toggle appears when the text overflows or there is extra.
  // http(s) URLs in the text are links (QA06, M5a-7).
  import type { Snippet } from 'svelte'
  import LinkifiedText from './LinkifiedText.svelte'

  let {
    text,
    lines = 3,
    more,
    less,
    header,
    extra,
  }: { text: string; lines?: number; more: string; less: string; header?: Snippet; extra?: Snippet } = $props()

  let expanded = $state(false)
  let body: HTMLElement | undefined = $state()
  let overflows = $state(false)

  $effect(() => {
    void text
    expanded = false
  })

  $effect(() => {
    if (body && !expanded) {
      overflows = body.scrollHeight > body.clientHeight + 1
    }
  })

  const toggle = $derived(overflows || extra !== undefined)
</script>

<div class="expandable" class:expanded>
  {#if header}{@render header()}{/if}
  <p class="body" bind:this={body} style:--lines={lines}><LinkifiedText {text} /></p>
  {#if expanded && extra}{@render extra()}{/if}
  {#if toggle}
    <button type="button" class="toggle" aria-expanded={expanded} onclick={() => (expanded = !expanded)}>
      {expanded ? less : more}
    </button>
  {/if}
</div>

<style>
  .expandable {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: var(--space-2);
  }

  .body {
    display: -webkit-box;
    overflow: hidden;
    -webkit-line-clamp: var(--lines);
    line-clamp: var(--lines);
    -webkit-box-orient: vertical;
    white-space: pre-line;
    overflow-wrap: anywhere;
  }

  .body:empty {
    display: none;
  }

  .expanded .body {
    display: block;
  }

  .toggle {
    padding: 0;
    border: none;
    background: none;
    font-weight: var(--font-weight-medium);
  }
</style>
