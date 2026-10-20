<script lang="ts">
  // A channel's admin page (1.5, from the dashboard's Manage): the channel as TonTuyau knows it on YouTube, how many of
  // its videos are in each state, its subscription (2.0: subscribe, its tabs, unsubscribe), the refreshes (the channel,
  // the channel and its videos, one video), and its videos,
  // latest first, by state (?state=, the chips) or words. One task at a time: while one goes on (any), the buttons
  // wait and a line says which; the page asks again every 3 s then, and shows what changed once it's over. A refusal
  // says why; a 401 ends the session.
  import { untrack } from 'svelte'
  import Avatar from '../components/Avatar.svelte'
  import Button from '../components/Button.svelte'
  import Chips from '../components/Chips.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import FailedDownloads from '../components/FailedDownloads.svelte'
  import Icon from '../components/Icon.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import SubscriptionCard from '../components/SubscriptionCard.svelte'
  import Thumbnail from '../components/Thumbnail.svelte'
  import YouTubeStateBadge from '../components/YouTubeStateBadge.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { AdminChannel, AdminVideo, RunTask, Tasks } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDate, formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'
  import { youTubeStates } from '../lib/youTubeStates'

  let { id }: { id: string } = $props()

  const BUSY_EVERY = 3_000
  const IDLE_EVERY = 30_000
  const SEARCH_AFTER = 300
  const states = youTubeStates

  const channel = new Remote<AdminChannel>()
  const tasks = new Remote<Tasks>()
  const videos = new Pager<AdminVideo>()

  const shownState = $derived(states.find((s) => s === router.query.get('state')) ?? null)
  let typed = $state('')
  let words = $state('')
  $effect(() => {
    const text = typed.trim()
    const timer = setTimeout(() => (words = text), SEARCH_AFTER)
    return () => clearTimeout(timer)
  })

  $effect(() => channel.run((signal) => api.adminChannel(id, signal)))
  $effect(() => tasks.run((signal) => api.tasks(signal)))
  $effect(() => {
    const query = { state: shownState ?? undefined, q: words || undefined }
    return videos.start((cursor, signal) =>
      api.adminChannelVideos(id, { ...query, cursor: cursor ?? undefined }, signal),
    )
  })

  const lock = $derived(tasks.data?.lock ?? null)
  $effect(() => {
    const timer = setInterval(() => void tasks.refresh(), lock ? BUSY_EVERY : IDLE_EVERY)
    return () => clearInterval(timer)
  })
  // A run ending: what it changed.
  let wasBusy = false
  // Each run ending: its failed downloads read again too.
  let runsEnded = $state(0)
  $effect(() => {
    const busy = lock !== null
    if (wasBusy && !busy) {
      untrack(() => {
        void channel.refresh()
        void videos.refreshFirst((video) => video.video.id)
        runsEnded++
      })
    }
    wasBusy = busy
  })

  $effect(() => {
    if (channel.error?.status === 401 || tasks.error?.status === 401 || videos.error?.status === 401) {
      auth.expired()
    }
  })

  let refused = $state<string | null>(null)
  let pending = $state(false)

  async function refresh(task: RunTask, target: string) {
    refused = null
    pending = true
    try {
      await api.startTask(task, target)
    } catch (error) {
      refused = t('task.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
      await tasks.refresh()
    }
  }

  pageTitle(() => channel.data?.channel.name ?? t('admin.dashboard'))

  const number = (count: number) => formatNumber(count, i18n.lang)

  const filters = $derived([
    { value: 'all', label: tp('channelAdmin.all', channel.data ? total(channel.data) : 0) },
    ...states.map((s) => ({
      value: s,
      label: `${t(`youTubeState.${s}`)} ${channel.data ? number(channel.data.videos[s]) : ''}`.trim(),
    })),
  ])

  function total(data: AdminChannel): number {
    return states.reduce((sum, s) => sum + data.videos[s], 0)
  }

  function when(at: string | null | undefined): string {
    return at ? formatAge(at, i18n.lang) : t('youTubeState.never')
  }
</script>

<div class="page">
  <a class="back" href={href.admin()}><Icon name="back" />{t('admin.dashboard')}</a>

  {#if channel.status === 'loading'}
    <Skeleton layout="page" />
  {:else if channel.status === 'error' && channel.error}
    <ErrorState error={channel.error} onretry={() => channel.retry()} />
  {:else if channel.data}
    {@const data = channel.data}
    {@const card = data.channel}
    <header>
      <Avatar src={card.thumbnailUrl} versions={card.thumbnails} size="lg" />
      <div class="names">
        <h1>{card.name}</h1>
        <code>{card.id}</code>
      </div>
      <div class="links">
        <a class="link-button" href={href.channel(card.id)}>{t('channelAdmin.openHere')}</a>
        <a
          class="link-button"
          href={`https://www.youtube.com/channel/${encodeURIComponent(card.id)}`}
          target="_blank"
          rel="noreferrer noopener">{t('channelAdmin.openYouTube')}</a
        >
      </div>
    </header>

    {#if lock}
      <p class="busy" role="status">
        {t('tasks.busy', {
          task: t(`task.${lock.task}`),
          by: lock.trigger === 'admin' ? 'admin' : t(`run.by.${lock.trigger}`),
          ago: formatAge(lock.since, i18n.lang),
        })}
        {#if tasks.data?.runningHere}
          <a href={href.adminRun(tasks.data.runningHere)}>{t('task.follow')}</a>
        {/if}
      </p>
    {/if}
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}

    <div class="two">
      <section class="card" aria-labelledby="channel-title">
        <h2 id="channel-title">{t('channelAdmin.onYouTube')}</h2>
        <dl>
          <div>
            <dt>{t('channelAdmin.state')}</dt>
            <dd>
              <YouTubeStateBadge state={data.youTube.state} />
              <small>{t('channelAdmin.checked', { when: when(data.youTube.checkedAt) })}</small>
              {#if data.youTube.reason}<small>{data.youTube.reason}</small>{/if}
            </dd>
          </div>
          <div>
            <dt>{t('channelAdmin.subscribers')}</dt>
            <dd>{number(card.subscribers)}</dd>
          </div>
          <div>
            <dt>{t('channelAdmin.inTonTuyau')}</dt>
            <dd>
              {tp('count.videos', card.videoCounts.videos)} · {tp('count.shorts', card.videoCounts.shorts)} · {tp(
                'count.streams',
                card.videoCounts.streams,
              )}
            </dd>
          </div>
        </dl>
        <div class="actions">
          <Button
            variant="filled"
            disabled={pending || lock !== null}
            onclick={() => void refresh('refreshChannel', card.id)}
          >
            {t('channelAdmin.refreshChannel')}
          </Button>
          <Button disabled={pending || lock !== null} onclick={() => void refresh('refreshChannelVideos', card.id)}>
            {t('channelAdmin.refreshAll')}
          </Button>
        </div>
        <p class="hint">{t('channelAdmin.refreshHint')}</p>
      </section>

      <section class="card" aria-labelledby="states-title">
        <h2 id="states-title">{t('channelAdmin.videosOnYouTube')}</h2>
        <dl class="states">
          {#each states as s (s)}
            <div>
              <dt><YouTubeStateBadge state={s} /></dt>
              <dd>{number(data.videos[s])}</dd>
            </div>
          {/each}
        </dl>
        <p class="hint">{t('channelAdmin.statesHint')}</p>
      </section>

      <SubscriptionCard channelId={card.id} subscription={data.subscription} onchange={() => channel.refresh()} />
    </div>

    <FailedDownloads channelId={card.id} refreshes={runsEnded} />

    <section class="videos" aria-labelledby="videos-title">
      <h2 id="videos-title" class="visually-hidden">{t('channelAdmin.videos')}</h2>
      <div class="tools">
        <input
          class="search"
          type="search"
          bind:value={typed}
          placeholder={t('channelAdmin.search')}
          aria-label={t('channelAdmin.search')}
          autocomplete="off"
        />
        <Chips
          label={t('channelAdmin.byState')}
          options={filters}
          selected={shownState ?? 'all'}
          hrefFor={(value) =>
            value === 'all' ? href.adminChannel(id) : `${href.adminChannel(id)}?state=${encodeURIComponent(value)}`}
        />
        {#if videos.total !== undefined}
          <span class="count">{tp('channelAdmin.count', videos.total)}</span>
        {/if}
      </div>

      {#if videos.status === 'loading'}
        <Skeleton layout="page" />
      {:else if videos.status === 'error' && videos.error}
        <ErrorState error={videos.error} onretry={() => videos.retry()} />
      {:else if videos.items.length === 0}
        <p class="empty">{t('channelAdmin.noVideo')}</p>
      {:else}
        <div class="table-box">
          <table aria-labelledby="videos-title">
            <thead>
              <tr>
                <th>{t('channelAdmin.video')}</th>
                <th>{t('channelAdmin.type')}</th>
                <th>{t('channelAdmin.published')}</th>
                <th>{t('channelAdmin.onYouTubeColumn')}</th>
                <th>{t('channelAdmin.lastCheck')}</th>
                <th><span class="visually-hidden">{t('channelAdmin.actions')}</span></th>
              </tr>
            </thead>
            <tbody>
              {#each videos.items as item (item.video.id)}
                {@const video = item.video}
                <tr>
                  <td>
                    <a class="video" href={href.watch(video.id)}>
                      <span class="thumb"
                        ><Thumbnail src={video.thumbnailUrl} versions={video.thumbnails} sizes="96px" /></span
                      >
                      <span class="title">
                        <b>{video.title}</b>
                        <code>{video.id}</code>
                      </span>
                    </a>
                  </td>
                  <td>{t(`type.${video.type}`)}</td>
                  <td>{formatDate(video.published, i18n.lang)}</td>
                  <td><YouTubeStateBadge state={item.youTube.state} /></td>
                  <td class="when">
                    {when(item.youTube.checkedAt)}
                    {#if item.youTube.reason}<small>{item.youTube.reason}</small>{/if}
                  </td>
                  <td>
                    <!-- A gone video isn't read again (1.5): its refresh would be refused. -->
                    <span class="row-actions">
                      <Button
                        disabled={pending || lock !== null || item.youTube.state === 'gone'}
                        onclick={() => void refresh('refreshVideo', video.id)}
                      >
                        {t('task.refresh')} <span class="visually-hidden">{video.title}</span>
                      </Button>
                      {#if item.youTube.state === 'gone'}
                        <small>{t('channelAdmin.goneNotRefreshed')}</small>
                      {/if}
                    </span>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
        <LoadMore pager={videos} />
      {/if}
    </section>
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
    gap: var(--space-1);
    align-self: flex-start;
    color: var(--color-text-secondary);
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-4);
  }

  .names {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  h1 {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
    overflow-wrap: anywhere;
  }

  code,
  small,
  .hint,
  .count {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .links {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin-left: auto;
  }

  .link-button {
    display: inline-flex;
    align-items: center;
    height: var(--button-height);
    padding: 0 var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-pill);
    color: var(--color-text);
    font-weight: var(--font-weight-medium);
  }

  .busy {
    color: var(--color-running);
    font-weight: var(--font-weight-medium);
  }

  .busy a {
    margin-left: var(--space-2);
  }

  .refused {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .two {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(var(--card-min-width), 1fr));
    gap: var(--space-4);
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
    align-items: baseline;
    gap: var(--space-1) var(--space-2);
    margin: 0;
    font-variant-numeric: tabular-nums;
  }

  .states dt {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--color-text);
    font-size: var(--font-size-sm);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .row-actions {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
  }

  .videos {
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

  .search {
    flex: 1 1 16rem;
    max-width: 24rem;
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  .search:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  .count {
    margin-left: auto;
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

  .video {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    color: var(--color-text);
    white-space: normal;
  }

  .thumb {
    flex: none;
    width: 6rem;
  }

  .title {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 12rem;
  }

  .title b {
    font-weight: var(--font-weight-medium);
  }

  .when {
    white-space: normal;
    min-width: 10rem;
  }

  .when small {
    display: block;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }

    .links {
      margin-left: 0;
    }
  }
</style>
