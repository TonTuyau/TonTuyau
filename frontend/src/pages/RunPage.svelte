<script lang="ts">
  // A run's page (1.4): what it is (its task, state, id), who started it and when, how long it ran (ticking while it
  // goes on), its result counts, its notes and error, and its log (RunLogView). While it goes on,
  // the run is asked again every 2 s, and Cancel (asked first) stops it when it goes on in this backend; a refresh can
  // also be put on hold by hand (1.5, asked first). A refresh on hold is asked again every 30 s, with Resume now and
  // Cancel it. A 404: no such run. A 401: the session ended,
  // the shell goes to the login page.
  import CopyButton from '../components/CopyButton.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HoldBar from '../components/HoldBar.svelte'
  import Icon from '../components/Icon.svelte'
  import RunLogView from '../components/RunLogView.svelte'
  import RunStatePill from '../components/RunStatePill.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import Button from '../components/Button.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { Run } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatDateTime, formatNumber, formatRunTime } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { Remote } from '../lib/remote.svelte'
  import { href } from '../lib/router/routes'
  import { asksYouTube, isRefresh, runSeconds, startedBy } from '../lib/runs'
  import { pageTitle } from '../lib/title.svelte'

  let { id }: { id: string } = $props()

  const EVERY = 2_000
  const HELD_EVERY = 30_000

  const remote = new Remote<Run>()
  $effect(() => {
    const runId = id
    return remote.run((signal) => api.run(runId, signal))
  })

  const run = $derived(remote.data)
  const running = $derived(run?.state === 'running')
  /** Waiting its turn (2.0): asked again as often as a run going on, to see it start. */
  const queued = $derived(run?.state === 'queued')
  const held = $derived(run?.state === 'onHold')

  let now = $state(Date.now())
  $effect(() => {
    if (held) {
      const timer = setInterval(() => void remote.refresh(), HELD_EVERY)
      return () => clearInterval(timer)
    }
    if (!running && !queued) {
      return
    }
    const timer = setInterval(() => {
      now = Date.now()
      void remote.refresh()
    }, EVERY)
    const clock = setInterval(() => (now = Date.now()), 1_000)
    return () => {
      clearInterval(timer)
      clearInterval(clock)
    }
  })

  let asking = $state(false)
  let askingHold = $state(false)
  let refused = $state<string | null>(null)

  async function cancel(runId: string) {
    refused = null
    try {
      await api.cancelRun(runId)
    } catch (error) {
      refused = t('task.cancelRefused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    }
    asking = false
    await remote.refresh()
  }

  async function hold(runId: string) {
    refused = null
    try {
      await api.holdRun(runId)
    } catch (error) {
      refused = t('hold.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    }
    askingHold = false
    await remote.refresh()
  }

  async function runNow(runId: string) {
    refused = null
    try {
      await api.runNow(runId)
    } catch (error) {
      refused = t('tasks.queue.runNowRefused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    }
    await remote.refresh()
  }

  async function resume(runId: string) {
    refused = null
    try {
      await api.resumeRun(runId)
    } catch (error) {
      refused = t('hold.resumeRefused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    }
    await remote.refresh()
  }

  $effect(() => {
    if (remote.error?.status === 401) {
      auth.expired()
    }
  })

  pageTitle(() => (run ? t(`task.${run.task}`) : t('admin.tasks')))

  /** Its counts, as the run's kind has them. */
  function counts(r: Run): [TextKey, number][] {
    const c = r.counts
    if (r.task === 'indexRebuild') {
      return []
    }
    if (isRefresh(r)) {
      return [
        ['run.count.videosRead', c.videosRead],
        ['run.count.channelsRead', c.channelsRead],
        ['run.count.foundOnYouTube', c.foundOnYouTube],
        ['run.count.unlistedOnYouTube', c.unlistedOnYouTube],
        ['run.count.goneFromYouTube', c.goneFromYouTube],
        ['run.count.privateOnYouTube', c.privateOnYouTube],
        ['run.count.checkFailed', c.checkFailed],
        ['run.count.commentsRead', c.commentsRead],
        ['run.count.picturesStored', c.artCopied],
        ['run.count.updated', c.updated],
      ]
    }
    return [
      ['run.count.videosRead', c.videosRead],
      ['run.count.created', c.created],
      ['run.count.unchanged', c.unchanged],
      ['run.count.rejected', c.rejected],
      ['run.count.artCopied', c.artCopied],
      ['run.count.artResized', c.artResized],
      ['run.count.mediaMissing', c.mediaMissing],
    ]
  }
</script>

<div class="page">
  <a class="back" href={href.adminTasks()}><Icon name="back" />{t('run.back')}</a>

  {#if remote.status === 'loading'}
    <Skeleton layout="page" />
  {:else if remote.error?.status === 404}
    <StateMessage icon="broken" title={t('run.missing')} detail={t('run.missingHint')} role="status" />
  {:else if remote.status === 'error' && remote.error}
    <ErrorState error={remote.error} onretry={() => remote.retry()} />
  {:else if run}
    <div>
      <div class="head">
        <h1>{t(`task.${run.task}`)}</h1>
        <RunStatePill state={run.state} />
      </div>
      <p class="id">
        <code>{run.id}</code>
        <CopyButton text={() => run.id} label={`${t('run.copy')} · ${run.id}`} />
      </p>
    </div>

    {#if run.state === 'queued'}
      <div class="actions">
        <p>
          {t('run.queued', { position: run.position ?? '–' })}
          {#if run.left > 0}{t('run.queuedToResume', { left: formatNumber(run.left, i18n.lang) })}{/if}
        </p>
        <div class="buttons">
          <Button onclick={() => void runNow(run.id)}>{t('tasks.queue.runNow')}</Button>
          {#if run.cancellable}
            <Button onclick={() => void cancel(run.id)}>{t('task.cancelRun')}</Button>
          {/if}
        </div>
      </div>
    {/if}
    {#if running && run.cancellable}
      <div class="actions">
        {#if asking}
          <div class="confirm" role="group" aria-label={t('task.confirmCancel')}>
            <p>{t('task.confirmCancel')}</p>
            <div>
              <Button variant="primary" onclick={() => void cancel(run.id)}>{t('task.cancelRun')}</Button>
              <Button onclick={() => (asking = false)}>{t('task.keepRunning')}</Button>
            </div>
          </div>
        {:else if askingHold}
          <div class="confirm" role="group" aria-label={t('hold.confirmTitle')}>
            <p><b>{t('hold.confirmTitle')}</b> {t('hold.confirm')}</p>
            <div>
              <Button variant="primary" onclick={() => void hold(run.id)}>{t('hold.put')}</Button>
              <Button onclick={() => (askingHold = false)}>{t('task.keepRunning')}</Button>
            </div>
          </div>
        {:else}
          {#if asksYouTube(run)}
            <Button onclick={() => (askingHold = true)}>{t('hold.put')}</Button>
          {/if}
          <Button onclick={() => (asking = true)}>{t('task.cancelRun')}</Button>
        {/if}
      </div>
    {/if}
    {#if held}
      <HoldBar
        {run}
        showTask={false}
        onresume={(runId) => void resume(runId)}
        oncancel={(runId) => void cancel(runId)}
      />
    {/if}
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}

    <dl class="meta">
      <div>
        <dt>{t('run.startedBy')}</dt>
        <dd>{startedBy(run)}</dd>
      </div>
      <div>
        <dt>{t('run.started')}</dt>
        <dd>{formatDateTime(run.startedAt, i18n.lang)}</dd>
      </div>
      <div>
        <dt>{t('run.finished')}</dt>
        <dd>{run.finishedAt ? formatDateTime(run.finishedAt, i18n.lang) : '–'}</dd>
      </div>
      <div>
        <dt>{t('run.duration')}</dt>
        <dd>{formatRunTime(runSeconds(run, now))}</dd>
      </div>
    </dl>

    {#if run.error}
      <section class="card problem" aria-labelledby="error-title">
        <h2 id="error-title">{t('run.error')}</h2>
        <p>{run.error}</p>
      </section>
    {/if}

    <section class="card" aria-labelledby="result-title">
      <h2 id="result-title">{running ? t('run.soFar') : t('run.result')}</h2>
      {#if run.target}
        <p>
          {t('run.target')}
          <a
            href={run.task === 'refreshVideo' || run.task === 'downloadVideo'
              ? href.watch(run.target)
              : href.channel(run.target)}><code>{run.target}</code></a
          >
        </p>
      {/if}
      <dl class="counts">
        {#each counts(run) as [label, value] (label)}
          <div>
            <dt>{t(label)}</dt>
            <dd>{formatNumber(value, i18n.lang)}</dd>
          </div>
        {/each}
      </dl>
    </section>

    {#if run.notes.length > 0}
      <section class="card" aria-labelledby="notes-title">
        <h2 id="notes-title">{t('run.notes')}</h2>
        <ul class="notes">
          {#each run.notes as note, index (index)}
            <li>{note}</li>
          {/each}
        </ul>
      </section>
    {/if}

    <!-- The page's own id, not run.id: a prop is read through its expression, and the run is replaced every 2 s
         while it goes on (the log would start over each time). -->
    <!-- Again from its start when it resumes: its log goes on. -->
    {#key held}
      <RunLogView runId={id} />
    {/key}
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-5);
    max-width: var(--admin-page-max-width);
    margin: 0 auto;
    padding: var(--space-6) var(--space-8) var(--space-12);
  }

  .back {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    align-self: flex-start;
    color: var(--color-text-secondary);
    font-weight: var(--font-weight-medium);
  }

  .back:hover {
    color: var(--color-text);
  }

  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
  }

  h1 {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
  }

  .id {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-top: var(--space-1);
  }

  .id code {
    overflow-wrap: anywhere;
  }

  dl {
    margin: 0;
  }

  dd {
    margin: 0;
  }

  .meta {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(var(--meta-min-width), 1fr));
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .meta div {
    padding: var(--space-3) var(--space-4);
  }

  dt {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .meta dd {
    margin-top: var(--space-1);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .buttons {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .confirm b {
    font-weight: var(--font-weight-medium);
  }

  .confirm {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-surface);
  }

  .confirm div {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .refused {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .problem {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .counts {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(var(--meta-min-width), 1fr));
    gap: var(--space-3) var(--space-6);
  }

  .counts dd {
    margin-top: var(--space-1);
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .notes {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    max-height: var(--notes-max-height);
    overflow-y: auto;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .notes li {
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    white-space: pre-wrap;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }
  }
</style>
