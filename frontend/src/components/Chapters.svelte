<script lang="ts">
  // A video's chapters (1.5), always shown in its description box, after a line that sets them apart from the
  // description (2.1.0), their count by their heading: a row of cards, the one playing outlined; a click goes there (a
  // plain click seeks the player, like a timecode).
  import type { Chapter } from '../lib/api/types'
  import { formatDuration } from '../lib/format'
  import { t } from '../lib/i18n/i18n.svelte'

  let {
    chapters,
    time,
    hrefFor,
    onseek,
  }: {
    chapters: readonly Chapter[]
    /** Where the video is, in seconds. */
    time: number
    hrefFor: (seconds: number) => string
    onseek: (seconds: number) => void
  } = $props()

  const playing = $derived(chapters.findLastIndex((chapter) => chapter.startSeconds <= time))

  function go(event: MouseEvent, seconds: number) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return
    }
    event.preventDefault()
    onseek(seconds)
  }
</script>

<section class="chapters" aria-labelledby="chapters-title">
  <h2><span id="chapters-title">{t('watch.chapters')}</span> <span class="count">· {chapters.length}</span></h2>
  <ol>
    {#each chapters as chapter, index (index)}
      <li>
        <a
          href={hrefFor(Math.floor(chapter.startSeconds))}
          class:playing={index === playing}
          aria-current={index === playing ? 'time' : undefined}
          onclick={(event) => go(event, chapter.startSeconds)}
        >
          <b>{chapter.title}</b>
          <span class="at">{formatDuration(chapter.startSeconds)}</span>
        </a>
      </li>
    {/each}
  </ol>
</section>

<style>
  /* Not part of the description: a line between them. */
  .chapters {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: var(--space-3);
    padding-top: var(--space-3);
    border-top: var(--border-width) solid var(--color-divider);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  ol {
    display: flex;
    gap: var(--space-2);
    margin: 0;
    padding: 0 0 var(--space-1);
    overflow-x: auto;
    list-style: none;
    scroll-snap-type: x proximity;
  }

  li {
    flex: none;
    scroll-snap-align: start;
  }

  a {
    display: grid;
    gap: var(--space-1);
    width: 11rem;
    height: 100%;
    padding: var(--space-2) var(--space-3);
    border: var(--border-width) solid var(--color-divider);
    border-radius: var(--radius-md);
    background: var(--color-bg-elevated);
    color: var(--color-text);
  }

  a:hover {
    background: var(--color-surface-hover);
  }

  /* Two pixels of brand, both inside the card: the list scrolls sideways, so it clips anything drawn outside it (an
     outer ring showed at the bottom and right only, cut at the top and on the first card's left). */
  a.playing {
    border-color: var(--color-brand);
    box-shadow: inset 0 0 0 var(--border-width) var(--color-brand);
  }

  b {
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-medium);
    line-height: var(--line-height-tight);
  }

  .count {
    color: var(--color-text-secondary);
  }

  .at {
    color: var(--color-link);
    font-size: var(--font-size-xs);
    font-variant-numeric: tabular-nums;
  }
</style>
