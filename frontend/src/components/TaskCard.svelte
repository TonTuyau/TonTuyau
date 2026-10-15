<script lang="ts">
  // A task of the Tasks page (1.4): what it does, its command, when it runs next, how it last ran, and its buttons.
  // `now` (a clock ticking every second) keeps a running run's duration going up, without asking the API.
  // Run now starts the incremental sync at once; the full sync (an hour) and applying deletions ask first. One run at a
  // time: while one holds the lock, the buttons wait; this task's run, going on in this backend, can be canceled
  // (asked first too). The page does the requests (onstart, oncancel).
  import type { RunTask, TaskInfo, Tasks } from '../lib/api/types'
  import { formatAge, formatDateTime, formatRunTime } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'
  import { runSeconds } from '../lib/runs'
  import Button from './Button.svelte'
  import RunStatePill from './RunStatePill.svelte'

  let {
    info,
    now,
    lock,
    runningHere,
    onstart,
    oncancel,
  }: {
    info: TaskInfo
    now: number
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

  function start() {
    if (info.task === 'incremental') {
      void act(() => onstart(info.task))
    } else {
      asking = 'start'
    }
  }

  const commands: Record<RunTask, string> = {
    incremental: 'tontuyau sync incremental',
    full: 'tontuyau sync full',
    applyDeletions: 'tontuyau sync apply-deletions <run>',
    refreshVideo: 'tontuyau youtube video <id>',
    refreshChannel: 'tontuyau youtube channel <id>',
    refreshChannelVideos: 'tontuyau youtube channel-videos <id>',
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
            <small>
              <a href={href.adminRun(info.waiting.runId)}>
                {t('task.waitingFrom', { date: formatDateTime(info.waiting.startedAt, i18n.lang) })}
              </a>
            </small>
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
    {:else if asking === 'start'}
      <div class="confirm" role="group" aria-label={t(`task.${info.task}`)}>
        <p>
          {info.task === 'full' ? t('task.confirmFull') : tp('task.confirmApply', info.waiting?.count ?? 0)}
        </p>
        <div>
          <Button
            variant="filled"
            disabled={pending || lock !== null}
            onclick={() => void act(() => onstart(info.task))}
          >
            {info.task === 'full' ? t('task.startFull') : tp('task.apply', info.waiting?.count ?? 0)}
          </Button>
          <Button onclick={() => (asking = null)}>{t('task.back')}</Button>
        </div>
      </div>
    {:else}
      <Button
        variant="filled"
        disabled={pending || lock !== null || (info.task === 'applyDeletions' && !info.waiting)}
        onclick={start}
      >
        {#if info.task !== 'applyDeletions'}
          {t('task.runNow')}
        {:else if info.waiting}
          {tp('task.apply', info.waiting.count)}
        {:else}
          {t('task.nothingToApply')}
        {/if}
      </Button>
      {#if lock}
        <small>{t('task.oneAtATime', { task: t(`task.${lock.task}`) })}</small>
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

  small a {
    color: var(--color-link);
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
