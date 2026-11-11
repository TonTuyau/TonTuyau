<script lang="ts">
  // The run queue (2.0) on the Tasks page: the runs waiting their turn, in the order they start, each with who asked
  // and when, Run now (2.3.0) and Cancel. While a run YouTube refused is on hold, the queue waits for it (the next runs
  // would be refused too): said above the list. A run held by hand or for disk space lets it go on (2.3.0).
  import type { Run } from '../lib/api/types'
  import { formatAge, formatDateTime, formatNumber } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
  import { startedBy } from '../lib/runs'
  import Button from './Button.svelte'

  let {
    queue,
    onHold,
    queueWaitsFor,
    now,
    onrunnow,
    oncancel,
  }: {
    queue: Run[]
    onHold: Run[]
    /** The run on hold the queue waits for (YouTube refused it), or null. */
    queueWaitsFor: string | null
    now: number
    onrunnow: (runId: string) => Promise<void>
    oncancel: (runId: string) => Promise<void>
  } = $props()

  const held = $derived(onHold.find((run) => run.id === queueWaitsFor) ?? null)
  const uid = $props.id()
</script>

{#if queue.length > 0}
  <section class="queue" aria-labelledby="{uid}-title">
    {#if held}
      <p class="held" role="status">
        {t('tasks.queue.waitsForYouTube', { task: t(`task.${held.task}`) })}
        <a href={href.adminRun(held.id)}>{held.target ?? held.id}</a>
      </p>
    {/if}
    <div class="box">
      <header>
        <h2 id="{uid}-title">{t('tasks.queue')} · {queue.length}</h2>
        <small>
          {#if held?.resumeAt}
            {t('tasks.queue.onHoldUntil', { at: formatDateTime(held.resumeAt, i18n.lang) })}
          {:else if held}
            {t('tasks.queue.onHold')}
          {:else}
            {t('tasks.queue.order')}
          {/if}
        </small>
      </header>
      <ol>
        {#each queue as run (run.id)}
          <li>
            <span class="position">{run.position}</span>
            <a class="what" href={href.adminRun(run.id)}>
              <b>{t(`task.${run.task}`)}</b>
              {#if run.target}<code>{run.target}</code>{/if}
            </a>
            <small>
              {#if run.left > 0}
                <!-- A run held, back in the queue (2.3.0): its turn resumes it where it stopped. -->
                {t('tasks.queue.resumes', { left: formatNumber(run.left, i18n.lang) })} ·
              {/if}
              {t('tasks.queue.asked', {
                by: startedBy(run),
                ago: formatAge(run.queuedAt ?? run.startedAt, i18n.lang, new Date(now)),
              })}
            </small>
            <div class="actions">
              <Button onclick={() => void onrunnow(run.id)}>{t('tasks.queue.runNow')}</Button>
              <Button onclick={() => void oncancel(run.id)}>{t('task.cancel')}</Button>
            </div>
          </li>
        {/each}
      </ol>
    </div>
  </section>
{/if}

<style>
  .queue {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .held {
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .held a {
    color: inherit;
    font-weight: var(--font-weight-medium);
  }

  .box {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-3) var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    justify-content: space-between;
    gap: var(--space-2);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  ol {
    display: grid;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    display: grid;
    grid-template-columns: var(--space-4) minmax(0, 1fr) auto auto;
    gap: var(--space-3);
    align-items: center;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .position {
    color: var(--color-text-secondary);
    font-variant-numeric: tabular-nums;
  }

  .what {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    align-items: baseline;
    min-width: 0;
    color: inherit;
    text-decoration: none;
  }

  .what b {
    font-weight: var(--font-weight-medium);
  }

  code {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    overflow-wrap: anywhere;
  }

  @media (max-width: 40rem) {
    li {
      grid-template-columns: var(--space-4) minmax(0, 1fr);
      row-gap: var(--space-1);
    }

    li small,
    .actions {
      grid-column: 2;
    }
  }
</style>
