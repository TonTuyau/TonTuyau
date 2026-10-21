<script lang="ts">
  // The admin's Tasks page (1.4): the TubeArchivist import (2.0: how it last ran, or why it can't start), the
  // refreshes on YouTube (1.5: a video, a channel, a channel and its videos,
  // pasted as a link or an id; a warning when there's no YouTube cookie; the refreshes on hold, with Resume now and
  // Cancel it), and the run history, all tasks' or one's (?task=, the chips). Kept up to
  // date by itself: asked again every 3 s while a run goes on, every 30 s else, without redrawing what didn't change
  // (Svelte updates the values that did: a row under the pointer stays); a running duration ticks every second from
  // its start, without asking. Starting a task opens its run's page (its live log); a refusal (409: a run going on,
  // TubeArchivist not set up) says why above the cards. A 401 means the session ended meanwhile: the shell then goes to the
  // login page.
  import { untrack } from 'svelte'
  import AddChannelCard from '../components/AddChannelCard.svelte'
  import Chips from '../components/Chips.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import HoldBar from '../components/HoldBar.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import RunQueue from '../components/RunQueue.svelte'
  import RunStatePill from '../components/RunStatePill.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import TaskCard from '../components/TaskCard.svelte'
  import YouTubeTaskCard from '../components/YouTubeTaskCard.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { ChannelTabs, Run, RunTask, Tasks, YouTubeOverview, YouTubeSettings } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDateTime, formatRunTime } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { runResult, runSeconds, startedBy, youTubeTasks } from '../lib/runs'
  import { pageTitle } from '../lib/title.svelte'

  const BUSY_EVERY = 3_000
  const IDLE_EVERY = 30_000
  const tasksOrder: RunTask[] = ['import', 'indexRebuild', ...youTubeTasks]

  const tasks = new Remote<Tasks>()
  const history = new Pager<Run>()
  // Only for the warning: no cookie, YouTube may refuse.
  const youTube = new Remote<YouTubeOverview>()
  // Only for "Add a channel"'s default tabs (2.0).
  const youTubeSettings = new Remote<YouTubeSettings>()

  const filter = $derived(tasksOrder.find((task) => task === router.query.get('task')) ?? null)
  const busy = $derived(tasks.data?.lock != null || (tasks.data?.queue.length ?? 0) > 0)
  /** A task asked for now waits its turn (2.0): a run goes on, one is on hold, or others wait already. */
  const waits = $derived(
    tasks.data != null && (tasks.data.lock != null || tasks.data.onHold.length > 0 || tasks.data.queue.length > 0),
  )

  $effect(() => tasks.run((signal) => api.tasks(signal)))
  $effect(() => youTube.run((signal) => api.youTube(signal)))
  $effect(() => youTubeSettings.run((signal) => api.youTubeSettings(signal)))
  $effect(() => {
    const task = filter
    return history.start((before, signal) => api.runs({ task: task ?? undefined, before: before ?? undefined }, signal))
  })

  // The clock the running durations read.
  let now = $state(Date.now())
  $effect(() => {
    const timer = setInterval(() => (now = Date.now()), 1_000)
    return () => clearInterval(timer)
  })

  // Asked again, more often while a run goes on; once more when it ends, for its final state.
  $effect(() => {
    const timer = setInterval(refresh, busy ? BUSY_EVERY : IDLE_EVERY)
    return () => clearInterval(timer)
  })
  // Only that change: the history's own state isn't watched (untrack), its first load is no reason to ask again.
  let wasBusy = false
  $effect(() => {
    const isBusy = busy
    if (wasBusy && !isBusy) {
      untrack(() => void history.refreshFirst((run) => run.id))
    }
    wasBusy = isBusy
  })

  function refresh() {
    void tasks.refresh()
    void history.refreshFirst((run) => run.id)
  }

  $effect(() => {
    if (tasks.error?.status === 401 || history.error?.status === 401) {
      auth.expired()
    }
  })

  /** Why the last start or cancel was refused; cleared by the next one. */
  let refused = $state<string | null>(null)

  async function onstart(task: RunTask, target?: string, tabs?: ChannelTabs, days?: number) {
    refused = null
    try {
      const started = await api.startTask(task, target, tabs, days)
      router.navigate(href.adminRun(started.id))
    } catch (error) {
      refused = t('task.refused', { reason: reasonOf(error) })
      refresh()
    }
  }

  async function onresume(runId: string) {
    refused = null
    try {
      await api.resumeRun(runId)
      router.navigate(href.adminRun(runId))
    } catch (error) {
      refused = t('hold.resumeRefused', { reason: reasonOf(error) })
      refresh()
    }
  }

  async function oncancel(runId: string) {
    refused = null
    try {
      await api.cancelRun(runId)
    } catch (error) {
      refused = t('task.cancelRefused', { reason: reasonOf(error) })
    }
    refresh()
  }

  function reasonOf(error: unknown): string {
    return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
  }

  pageTitle(() => t('admin.tasks'))

  const filters = $derived([
    { value: 'all', label: t('runs.all') },
    ...tasksOrder.map((task) => ({ value: task, label: t(`task.${task}`) })),
  ])
</script>

<div class="page">
  <header>
    <h1>{t('admin.tasks')}</h1>
    {#if tasks.data?.lock}
      {@const lock = tasks.data.lock}
      <p class="busy" role="status">
        {t('tasks.busy', {
          task: t(`task.${lock.task}`),
          by: lock.trigger === 'admin' ? 'admin' : t(`run.by.${lock.trigger}`),
          ago: formatAge(lock.since, i18n.lang, new Date(now)),
        })}
      </p>
    {/if}
  </header>

  {#if tasks.status === 'loading'}
    <Skeleton layout="page" />
  {:else if tasks.status === 'error' && tasks.error}
    <ErrorState error={tasks.error} onretry={() => tasks.retry()} />
  {:else if tasks.data}
    {@const data = tasks.data}
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}
    <RunQueue queue={data.queue} onHold={data.onHold} {now} {oncancel} />
    <section class="group" aria-labelledby="ta-title">
      <h2 id="ta-title">TubeArchivist</h2>
      <div class="tasks">
        {#each data.tasks.filter((info) => info.task === 'import') as info (info.task)}
          <TaskCard {info} {now} {waits} lock={data.lock} runningHere={data.runningHere} {onstart} {oncancel} />
        {/each}
      </div>
    </section>
    <section class="group" aria-labelledby="yt-title">
      <div class="head">
        <h2 id="yt-title">YouTube</h2>
        <HelpButton zone="YouTube"><p>{t('tasks.youTube.intro')}</p></HelpButton>
      </div>
      {#if youTube.data && youTube.data.cookie === null}
        <p class="warn">{t('tasks.youTube.noCookie')} <a href={href.adminYouTube()}>{t('admin.youTube')}</a></p>
      {/if}
      {#each data.onHold as held (held.id)}
        <HoldBar run={held} disabled={busy} {onresume} {oncancel} />
      {/each}
      <div class="tasks">
        {#each data.tasks.filter((info) => info.task !== 'import') as info (info.task)}
          <TaskCard {info} {now} {waits} lock={data.lock} runningHere={data.runningHere} {onstart} {oncancel} />
        {/each}
        {#each youTubeTasks as task (task)}
          {#if task === 'addChannel'}
            <AddChannelCard {waits} defaultTabs={youTubeSettings.data?.defaultTabs ?? null} {onstart} />
          {:else}
            <YouTubeTaskCard {task} {waits} {onstart} />
          {/if}
        {/each}
      </div>
    </section>
  {/if}

  <section class="history" aria-labelledby="runs-title">
    <div class="tools">
      <h2 id="runs-title">{t('runs.title')}</h2>
      <Chips
        label={t('runs.filter')}
        options={filters}
        selected={filter ?? 'all'}
        hrefFor={(value) => href.adminTasks(value === 'all' ? null : value)}
      />
      {#if history.total !== undefined}
        <span class="count">{tp('runs.count', history.total)}</span>
      {/if}
    </div>

    {#if history.status === 'loading'}
      <Skeleton layout="page" />
    {:else if history.status === 'error' && history.error}
      <ErrorState error={history.error} onretry={() => history.retry()} />
    {:else if history.items.length === 0}
      <p class="empty">{t('runs.empty')}</p>
    {:else}
      <div class="table-box">
        <table aria-labelledby="runs-title">
          <thead>
            <tr>
              <th>{t('runs.state')}</th>
              <th>{t('runs.run')}</th>
              <th>{t('runs.by')}</th>
              <th>{t('runs.started')}</th>
              <th class="num">{t('runs.duration')}</th>
              <th>{t('runs.result')}</th>
            </tr>
          </thead>
          <tbody>
            {#each history.items as run (run.id)}
              <tr>
                <td><RunStatePill state={run.state} /></td>
                <td class="run">
                  <b>{t(`task.${run.task}`)}</b>
                  <a href={href.adminRun(run.id)}><code>{run.id}</code></a>
                </td>
                <td>{startedBy(run)}</td>
                <td>{formatDateTime(run.startedAt, i18n.lang)}</td>
                <td class="num">{formatRunTime(runSeconds(run, now))}</td>
                <td class="result">{runResult(run)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
      <LoadMore pager={history} />
    {/if}
  </section>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--admin-page-max-width);
    margin: 0 auto;
    padding: var(--space-6) var(--space-8) var(--space-12);
  }

  h1 {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .busy {
    margin-top: var(--space-1);
    color: var(--color-text-secondary);
  }

  .busy {
    color: var(--color-running);
    font-weight: var(--font-weight-medium);
  }

  .refused {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .warn {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  .tasks {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(var(--card-min-width), 1fr));
    gap: var(--space-4);
  }

  .history {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
  }

  h2 {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .count {
    margin-left: auto;
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .empty {
    color: var(--color-text-secondary);
  }

  .table-box {
    /* Positioned: the hidden labels (.visually-hidden, absolute) then scroll with it instead of widening the page. */
    position: relative;
    overflow-x: auto;
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-variant-numeric: tabular-nums;
  }

  th {
    padding: var(--space-3) var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
    text-align: left;
    white-space: nowrap;
  }

  td {
    padding: var(--space-2) var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    white-space: nowrap;
  }

  tbody tr:last-child td {
    border-bottom: none;
  }

  tbody tr:hover {
    background: var(--color-surface);
  }

  .num {
    text-align: right;
  }

  .run b {
    display: block;
    font-weight: var(--font-weight-medium);
  }

  .run code {
    color: var(--color-link);
    font-size: var(--font-size-xs);
  }

  .result {
    min-width: var(--card-min-width);
    color: var(--color-text-secondary);
    white-space: normal;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }
  }
</style>
