<script lang="ts">
  // A run's log (1.4), oldest line first: its lines after the last one read, asked for again every 2 s until the API
  // says no more will come (the run is over and its log written). Warnings and errors stand out, and can be shown
  // alone; Copy takes every line. While at the end of the box, it follows the new lines; scrolled up, it stays put.
  import { tick, untrack } from 'svelte'
  import { api } from '../lib/api/client'
  import type { RunLogLine } from '../lib/api/types'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import CopyButton from './CopyButton.svelte'

  let { runId }: { runId: string } = $props()

  const EVERY = 2_000
  /** The API's default page: a full one means more lines are there already. */
  const PAGE = 500

  let lines = $state<RunLogLine[]>([])
  let complete = $state(false)
  let onlyProblems = $state(false)
  let box: HTMLDivElement | undefined = $state()

  const shown = $derived(onlyProblems ? lines.filter((line) => line.level !== 'information') : lines)
  const time = $derived(
    new Intl.DateTimeFormat(i18n.lang, { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false }),
  )

  $effect(() => {
    const id = runId
    const controller = new AbortController()
    untrack(() => {
      lines = []
      complete = false
    })
    void follow(id, controller.signal)
    return () => controller.abort()
  })

  async function follow(id: string, signal: AbortSignal) {
    let after = 0
    while (!signal.aborted) {
      let full = false
      try {
        const log = await api.runLog(id, after, signal)
        if (log.lines.length > 0) {
          const atEnd = !box || box.scrollHeight - box.scrollTop - box.clientHeight < 40
          lines = [...lines, ...log.lines]
          after = log.lines.at(-1)?.id ?? after
          await tick()
          if (atEnd && box) {
            box.scrollTop = box.scrollHeight
          }
        }
        complete = log.complete
        full = log.lines.length >= PAGE
      } catch {
        // A failed ask (the network, the backend restarting): the next one tries again.
      }
      if (complete || signal.aborted) {
        return
      }
      if (!full) {
        await new Promise((resolve) => setTimeout(resolve, EVERY))
      }
    }
  }

  const levels = { information: 'INF', warning: 'WRN', error: 'ERR' } as const

  function asText(): string {
    return lines.map((line) => `${time.format(new Date(line.at))} ${levels[line.level]} ${line.message}`).join('\n')
  }
</script>

<section class="card" aria-labelledby="log-title">
  <div class="tools">
    <h2 id="log-title">{t('run.log')}</h2>
    <div class="filter" role="group" aria-label={t('run.log.lines')}>
      <button type="button" class="chip" aria-pressed={!onlyProblems} onclick={() => (onlyProblems = false)}>
        {t('run.log.all')}
      </button>
      <button type="button" class="chip" aria-pressed={onlyProblems} onclick={() => (onlyProblems = true)}>
        {t('run.log.problems')}
      </button>
    </div>
    <CopyButton text={asText} label={`${t('run.copy')} · ${t('run.log')}`} />
  </div>

  {#if complete && lines.length === 0}
    <p class="note">{t('run.log.none')}</p>
  {:else}
    <div class="log" role="log" aria-labelledby="log-title" bind:this={box}>
      {#if lines.length === 0}
        <p class="note">{t('run.log.waiting')}</p>
      {/if}
      <ol>
        {#each shown as line (line.id)}
          <li class={line.level}>
            <time datetime={line.at}>{time.format(new Date(line.at))}</time>
            <span class="level">{levels[line.level]}</span>
            <span class="message">{line.message}</span>
          </li>
        {/each}
      </ol>
      {#if !complete}
        <p class="live">{t('run.log.live')}</p>
      {/if}
    </div>
  {/if}
</section>

<style>
  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  h2 {
    margin-right: auto;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .filter {
    display: flex;
    gap: var(--space-2);
  }

  .chip {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-surface);
    font-weight: var(--font-weight-medium);
  }

  .chip:hover {
    background: var(--color-surface-hover);
  }

  .chip[aria-pressed='true'] {
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
  }

  .log {
    max-height: var(--log-max-height);
    overflow: auto;
    padding: var(--space-2) 0;
    border: var(--border-width) solid var(--color-divider);
    border-radius: var(--radius-md);
    background: var(--color-log-bg);
    font: var(--font-size-xs) / 1.6 var(--font-mono);
  }

  ol {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    display: grid;
    grid-template-columns: var(--log-time-width) var(--log-level-width) minmax(0, 1fr);
    gap: var(--space-2);
    padding: 0 var(--space-4);
  }

  time,
  .level {
    color: var(--color-text-secondary);
  }

  .message {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
  }

  li.warning {
    background: var(--color-warning-bg);
  }

  li.warning .level,
  li.warning .message {
    color: var(--color-on-warning);
  }

  li.error {
    background: var(--color-error-bg);
  }

  li.error .level,
  li.error .message {
    color: var(--color-error);
  }

  .live {
    padding: var(--space-1) var(--space-4) 0;
    color: var(--color-running);
  }

  .note {
    padding: 0 var(--space-4);
    color: var(--color-text-secondary);
  }

  @media (max-width: 600px) {
    li {
      grid-template-columns: var(--log-time-width) minmax(0, 1fr);
    }

    .level {
      display: none;
    }
  }
</style>
