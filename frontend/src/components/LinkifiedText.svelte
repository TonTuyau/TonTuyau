<script lang="ts" module>
  /** Timecodes as links to a time of the video (1.1.0): its duration, the link, and what a plain click does. */
  export interface Timecodes {
    /** The video's duration from the API (seconds): later times stay text. */
    duration: number
    /** The link of a time, e.g. /watch?v=<id>&t=83: a new tab (middle click…) opens the video there. */
    href: (seconds: number) => string
    /** A plain click: seek the player there instead of following the link. */
    onseek?: (seconds: number) => void
  }
</script>

<script lang="ts">
  // Text with its http(s) URLs as links (QA06, M5a-7), and, given `timecodes`, its timecodes as links to that time.
  // Text nodes and <a> elements only: the data is never parsed as HTML. URLs open in a new tab, without giving it
  // access to this page or its URL; timecodes are in-app links.
  import { t } from '../lib/i18n/i18n.svelte'
  import { linkify } from '../lib/linkify'

  let { text, timecodes }: { text: string; timecodes?: Timecodes } = $props()

  const segments = $derived(linkify(text, { duration: timecodes?.duration }))

  function seek(event: MouseEvent, seconds: number) {
    const onseek = timecodes?.onseek
    if (!onseek || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return
    }
    event.preventDefault()
    onseek(seconds)
  }
</script>

{#each segments as segment, index (index)}{#if segment.kind === 'link'}<a
      class="link"
      href={segment.href}
      target="_blank"
      rel="noopener noreferrer">{segment.text}</a
    >{:else if segment.kind === 'time' && timecodes}<a
      class="link"
      href={timecodes.href(segment.seconds)}
      title={t('watch.playFrom', { time: segment.text })}
      data-seconds={segment.seconds}
      onclick={(event) => seek(event, segment.seconds)}>{segment.text}</a
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
