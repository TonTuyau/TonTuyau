<script lang="ts">
  // A task of the Tasks page (1.4; 2.0: the TubeArchivist import and the YouTube syncs): what it does, its command,
  // when it runs (the incremental sync: when next, by itself), how it last ran, and its buttons. `now` (a clock ticking
  // every second) keeps a running run's duration going up, without asking the API. Run now asks first for the long
  // ones (an import, a full sync: up to hours); without TubeArchivist's settings the import can't start, and says why. One run at a time: while one goes on (or waits), it's added to the run queue (2.0); this task's run,
  // going on in this backend, can be canceled (asked first too). The page does the requests (onstart, oncancel).
  import type { RunTask, TaskInfo, Tasks } from '../lib/api/types'
  import { formatAge, formatDateTime, formatRunTime } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
  import { runSeconds, taskCommands } from '../lib/runs'
  import Button from './Button.svelte'
  import RunStatePill from './RunStatePill.svelte'

  let {
    info,
    now,
    waits,
    lock,
    runningHere,
    onstart,
    oncancel,
  }: {
    info: TaskInfo
    now: number
    waits: boolean
    lock: Tasks['lock']
    runningHere: string | null
    onstart: (task: RunTask) => Promise<void>
    oncancel: (runId: string) => Promise<void>
  } = $props()

  let asking = $state<'start' | 'cancel' | null>(null)
  let pending = $state(false)

  /** This task's run holds the lock; `cancellable` when it goes on in this backend. */
  const mine = $derived(lock?.task === info.task)
  const cancellable = $derived(mine && info.lastRun !== null && info.lastRun.id === runningHere)

  async function act(work: () => Promise<void>) {
    pending = true
    try {
      await work()
    } finally {
      pending = false
      asking = null
    }
  }

  /** What Run now asks first, for the long tasks; null: it starts at once. */
  const confirm = $derived(
    info.task === 'import'
      ? { text: t('task.confirmImport'), start: t('task.startImport') }
      : info.task === 'youTubeFullSync'
        ? { text: t('task.confirmFullSync'), start: t('task.startFullSync') }
        : null,
  )

  function start() {
    if (confirm) {
      asking = 'start'
    } else {
      void act(() => onstart(info.task))
    }
  }

  /** When it runs: at startup (the import), next by itself (the incremental sync), or by hand only. */
  const when = $derived(
    info.unavailable ??
      (info.task === 'import'
        ? t('task.atStartup')
        : info.next
          ? new Date(info.next).getTime() <= now
            ? t('task.dueNow')
            : t('task.nextAt', { at: formatDateTime(info.next, i18n.lang) })
          : t('task.byHand')),
  )

  const last = $derived(info.lastRun)
  const titleId = $props.id()
</script>

<article class="card" aria-labelledby={titleId}>
  <header>
    <h2 id={titleId}>{t(`task.${info.task}`)}</h2>
    <code>{taskCommands[info.task]}</code>
  </header>
  <p class="what">{t(`task.${info.task}.what`)}</p>
  <dl>
    <div>
      <dt>{t('task.when')}</dt>
      <dd>
        <small>{when}</small>
      </dd>
    </div>
    <div>
      <dt>{t('task.lastRun')}</dt>
      <dd>
        {#if last}
          <a class="state" href={href.adminRun(last.id)}><RunStatePill state={last.state} /></a>
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

  <div class="actions">
    {#if mine && cancellable && info.lastRun}
      {@const running = info.lastRun}
      {#if asking === 'cancel'}
        <div class="confirm" role="group" aria-label={t('task.confirmCancel')}>
          <p>{t('task.confirmCancel')}</p>
          <div>
            <Button variant="filled" disabled={pending} onclick={() => void act(() => oncancel(running.id))}>
              {t('task.cancelRun')}
            </Button>
            <Button onclick={() => (asking = null)}>{t('task.keepRunning')}</Button>
          </div>
        </div>
      {:else}
        <Button onclick={() => (asking = 'cancel')}>{t('task.cancel')}</Button>
        <a href={href.adminRun(running.id)}>{t('task.follow')}</a>
      {/if}
    {:else if mine}
      <small>{t('task.elsewhere')}</small>
    {:else if asking === 'start' && confirm}
      <div class="confirm" role="group" aria-label={t(`task.${info.task}`)}>
        <p>{confirm.text}</p>
        <div>
          <Button variant="filled" disabled={pending} onclick={() => void act(() => onstart(info.task))}>
            {waits ? t('task.addToQueue') : confirm.start}
          </Button>
          <Button onclick={() => (asking = null)}>{t('task.back')}</Button>
        </div>
      </div>
    {:else}
      <Button variant="filled" disabled={pending || info.unavailable !== null} onclick={start}>
        {waits ? t('task.addToQueue') : t('task.runNow')}
      </Button>
      {#if waits}
        <small>{t('task.waitsItsTurn')}</small>
      {/if}
    {/if}
  </div>
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

  .state {
    display: inline-flex;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: auto;
  }

  .actions a {
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
  }

  .confirm {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    width: 100%;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-surface);
  }

  .confirm div {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
