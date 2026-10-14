<script lang="ts">
  // A task of the Tasks page (1.4): what it does, its command, when it runs next, how it last ran. `now` (a clock
  // ticking every second) keeps a running run's duration going up, without asking the API.
  import type { RunTask, TaskInfo } from '../lib/api/types'
  import { formatAge, formatDateTime, formatRunTime } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { runSeconds } from '../lib/runs'
  import RunStatePill from './RunStatePill.svelte'

  let { info, now }: { info: TaskInfo; now: number } = $props()

  const commands: Record<RunTask, string> = {
    incremental: 'tontuyau sync incremental',
    full: 'tontuyau sync full',
    applyDeletions: 'tontuyau sync apply-deletions <run>',
  }

  const last = $derived(info.lastRun)
  const titleId = $props.id()
</script>

<article class="card" aria-labelledby={titleId}>
  <header>
    <h2 id={titleId}>{t(`task.${info.task}`)}</h2>
    <code>{commands[info.task]}</code>
  </header>
  <p class="what">{t(`task.${info.task}.what`)}</p>
  <dl>
    <div>
      <dt>{t('task.when')}</dt>
      <dd>
        {#if info.task === 'applyDeletions'}
          {#if info.waiting}
            <b>{tp('task.waiting', info.waiting.count)}</b>
            <small>{t('task.waitingFrom', { date: formatDateTime(info.waiting.startedAt, i18n.lang) })}</small>
          {:else}
            <small>{t('task.afterFull')} · {t('task.nothingWaiting')}</small>
          {/if}
        {:else}
          <code>{info.schedule}</code>
          <small>
            {info.next ? t('task.next', { date: formatDateTime(info.next, i18n.lang) }) : t('task.notScheduled')}
          </small>
        {/if}
      </dd>
    </div>
    <div>
      <dt>{t('task.lastRun')}</dt>
      <dd>
        {#if last}
          <RunStatePill state={last.state} />
          <small>
            {#if last.state === 'running'}
              {t('task.runningFor', { duration: formatRunTime(runSeconds(last, now)) })}
            {:else}
              {formatAge(last.finishedAt ?? last.startedAt, i18n.lang, new Date(now))} · {formatRunTime(
                runSeconds(last, now),
              )}
            {/if}
          </small>
        {:else}
          <small>{t('task.never')}</small>
        {/if}
      </dd>
    </div>
  </dl>
</article>

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

  header {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  code {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .what {
    color: var(--color-text-secondary);
    text-wrap: pretty;
  }

  dl {
    display: grid;
    gap: var(--space-2);
    margin: 0;
  }

  dl div {
    display: grid;
    grid-template-columns: var(--task-label-width) minmax(0, 1fr);
    gap: var(--space-2);
    align-items: baseline;
  }

  dt {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  dd {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin: 0;
  }

  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }
</style>
