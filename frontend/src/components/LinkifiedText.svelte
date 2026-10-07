<script lang="ts">
  // Text with its http(s) URLs as links (QA06, M5a-7). Text nodes and <a> elements only: the data is never
  // parsed as HTML. Links open in a new tab, without giving it access to this page or its URL.
  import { linkify } from '../lib/linkify'

  let { text }: { text: string } = $props()

  const segments = $derived(linkify(text))
</script>

{#each segments as segment, index (index)}{#if segment.kind === 'link'}<a
      class="link"
      href={segment.href}
      target="_blank"
      rel="noopener noreferrer">{segment.text}</a
    >{:else}{segment.text}{/if}{/each}

<style>
  .link {
    color: var(--color-link);
    overflow-wrap: anywhere;
  }

  .link:hover {
    text-decoration: underline;
  }
</style>
