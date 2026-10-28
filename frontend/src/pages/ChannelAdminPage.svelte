<script lang="ts">
  // A channel's admin page (1.5, from the dashboard's Manage): the channel as TonTuyau knows it on YouTube, how many of
  // its videos are in each state, its subscription (2.0: subscribe, its tabs, unsubscribe), the refreshes (the channel,
  // the channel and its videos, one video), and its videos,
  // latest first, by state (?state=, the chips) or words. While a run goes on (any), a line says which and a refresh
  // asked for waits its turn in the queue (alpha.3: the buttons stay enabled); the page asks again every 3 s then, and
  // shows what changed once it's over. A refusal says why; a 401 ends the session.
  // 2.3.0: on each row, its refresh, its download again and its purge, as icons. Select shows the boxes, then the
  // selection's bar does the same for the ones ticked (refreshVideos, redownloadVideos, purgeVideos), and purges the
  // whole channel. A purge is asked first in a dialog, which purges everything (the next sync downloads it again) or
  // the files only (the videos kept, their syncs blocked: on top of the list, greyed, each to unblock, or the ones
  // ticked); a download again too, with yt-dlp's format and the retry without
  // the cookie, filled in with the YouTube page's settings. The run followed until it's over, then its result in a line
  // and the list read again.
  import { untrack } from 'svelte'
  import { SvelteSet } from 'svelte/reactivity'
  import Avatar from '../components/Avatar.svelte'
  import Button from '../components/Button.svelte'
  import Chips from '../components/Chips.svelte'
  import Dialog from '../components/Dialog.svelte'
  import DownloadOnce, { downloadOnce } from '../components/DownloadOnce.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import FailedDownloads from '../components/FailedDownloads.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import Icon from '../components/Icon.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import SubscriptionCard from '../components/SubscriptionCard.svelte'
  import Thumbnail from '../components/Thumbnail.svelte'
  import YouTubeStateBadge from '../components/YouTubeStateBadge.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { AdminChannel, AdminVideo, Run, RunTask, Tasks, YouTubeSettings } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatBytes, formatDate, formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { runResult } from '../lib/runs'
  import { pageTitle } from '../lib/title.svelte'
  import { youTubeStates } from '../lib/youTubeStates'

  let { id }: { id: string } = $props()

  const BUSY_EVERY = 3_000
  const IDLE_EVERY = 30_000
  const SEARCH_AFTER = 300
  /** The videos picked are this many at most (the API's limit): more, it's the whole channel's purge. */
  const MAX_PICKED = 500
  const FOLLOW_EVERY = 1_000
  const states = youTubeStates

  const channel = new Remote<AdminChannel>()
  const tasks = new Remote<Tasks>()
  const videos = new Pager<AdminVideo>()

  const shownState = $derived(states.find((s) => s === router.query.get('state')) ?? null)
  // 2.3.0: the chip of the videos whose syncs are blocked (?state=blocked), not a state on YouTube.
  const shownBlocked = $derived(router.query.get('state') === 'blocked')
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
    const query = { state: shownState ?? undefined, q: words || undefined, blocked: shownBlocked || undefined }
    // Another list: the videos ticked in the one before aren't shown any more, so none stays ticked.
    untrack(() => selected.clear())
    return videos.start((cursor, signal) =>
      api.adminChannelVideos(id, { ...query, cursor: cursor ?? undefined }, signal),
    )
  })

  const lock = $derived(tasks.data?.lock ?? null)
  /** A refresh asked for now waits its turn: a run goes on, one is on hold, or others wait already. */
  const waits = $derived(
    tasks.data != null && (tasks.data.lock != null || tasks.data.onHold.length > 0 || tasks.data.queue.length > 0),
  )
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
  let queued = $state<string | null>(null)
  let pending = $state(false)

  async function refresh(task: RunTask, target: string) {
    refused = null
    queued = null
    pending = true
    const waiting = waits
    try {
      await api.startTask(task, target)
      if (waiting) {
        queued = t('channelAdmin.queued', { task: t(`task.${task}`) })
      }
    } catch (error) {
      refused = t('task.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
      await tasks.refresh()
    }
  }

  // The videos picked (2.3.0): Select shows a box on each row. A purge or a download again waits in its dialog until
  // confirmed; a refresh starts at once.
  type Picked = { kind: 'one'; video: AdminVideo['video'] } | { kind: 'selection' } | { kind: 'channel' }
  let selecting = $state(false)
  const selected = new SvelteSet<string>()
  let purging = $state<Picked | null>(null)
  let confirming = $state(false)
  // The purge's choice: everything, or the files only (the videos' syncs blocked).
  let filesOnly = $state(false)
  let redownloading = $state<Picked | null>(null)
  let askingDownload = $state(false)
  // The YouTube page's download settings, read when the dialog opens: what it's filled in with.
  let settings = $state<YouTubeSettings | null>(null)
  let format = $state('')
  let withoutCookie = $state(true)
  // The run asked for, as last read: followed until it's over.
  let followed = $state<Run | null>(null)
  let alive = true
  $effect(() => () => (alive = false))

  const shownIds = $derived(videos.items.map((item) => item.video.id))
  const allShown = $derived(shownIds.length > 0 && shownIds.every((videoId) => selected.has(videoId)))
  const noPick = $derived(pending || selected.size === 0 || selected.size > MAX_PICKED)
  // The videos ticked whose syncs are blocked (2.3.0): those the selection's Unblock unblocks.
  const blockedPicked = $derived(
    videos.items.filter((item) => item.syncBlocked && selected.has(item.video.id)).map((item) => item.video.id),
  )

  function stopSelecting() {
    selecting = false
    selected.clear()
  }

  function toggleAll() {
    if (allShown) {
      selected.clear()
    } else {
      shownIds.forEach((videoId) => selected.add(videoId))
    }
  }

  function askPurge(what: NonNullable<typeof purging>) {
    purging = what
    filesOnly = false
    confirming = true
  }

  async function askRedownload(what: Picked) {
    redownloading = what
    askingDownload = true
    try {
      settings = await api.youTubeSettings()
      format = settings.downloadFormat
      withoutCookie = settings.retryWithoutCookie
    } catch {
      // Not read: the run takes the YouTube page's settings, nothing else is offered.
      settings = null
    }
  }

  async function startPicked(
    task: 'purgeVideos' | 'refreshVideos' | 'redownloadVideos',
    what: Picked | null,
    once: { format?: string; retryWithoutCookie?: boolean; filesOnly?: boolean } = {},
  ) {
    if (!what) {
      return
    }
    refused = null
    queued = null
    followed = null
    pending = true
    try {
      const started = await api.startTask(task, id, undefined, undefined, once.format, {
        retryWithoutCookie: once.retryWithoutCookie,
        filesOnly: once.filesOnly,
        videos: what.kind === 'one' ? [what.video.id] : what.kind === 'selection' ? [...selected] : undefined,
      })
      stopSelecting()
      void follow(started)
    } catch (error) {
      refused = t('task.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
      confirming = false
      askingDownload = false
      await tasks.refresh()
    }
  }

  /** Unblocks the syncs of those videos (2.3.0): back in the library's lists, the page's list read again. */
  async function unblock(videoIds: string[]) {
    refused = null
    pending = true
    try {
      await api.unblockVideos(id, videoIds)
      stopSelecting()
      void channel.refresh()
      videos.retry()
    } catch (error) {
      refused = t('task.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
    }
  }

  /** Reads the run again until it's over, then the channel and its videos (from the first page: some may be gone). */
  async function follow(run: Run) {
    let current = run
    followed = current
    while (alive && (current.state === 'queued' || current.state === 'running')) {
      await new Promise((resolve) => setTimeout(resolve, FOLLOW_EVERY))
      current = await api.run(run.id).catch(() => current)
      followed = current
    }
    if (alive) {
      void channel.refresh()
      videos.retry()
      runsEnded++
    }
  }

  const purgeTitle = $derived(
    purging?.kind === 'one'
      ? t('purge.titleOne', { title: purging.video.title })
      : purging?.kind === 'selection'
        ? tp('purge.titleMany', selected.size)
        : t('purge.titleChannel'),
  )
  const redownloadTitle = $derived(
    redownloading?.kind === 'one'
      ? t('redownload.titleOne', { title: redownloading.video.title })
      : tp('redownload.titleMany', selected.size),
  )

  pageTitle(() => channel.data?.channel.name ?? t('admin.dashboard'))

  const number = (count: number) => formatNumber(count, i18n.lang)

  const filters = $derived([
    { value: 'all', label: tp('channelAdmin.all', channel.data ? total(channel.data) : 0) },
    ...states.map((s) => ({
      value: s,
      label: `${t(`youTubeState.${s}`)} ${channel.data ? number(channel.data.videos[s]) : ''}`.trim(),
    })),
    ...(shownBlocked || (channel.data?.syncBlocked ?? 0) > 0
      ? [{ value: 'blocked', label: `${t('picked.blockedChip')} ${number(channel.data?.syncBlocked ?? 0)}` }]
      : []),
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
    {@const size = data.size}
    <header>
      <Avatar src={card.thumbnailUrl} versions={card.thumbnails} size="lg" />
      <div class="names">
        <h1>{card.name}</h1>
        <code>{card.id}</code>
      </div>
      <div class="links">
        <Button href={href.channel(card.id)}>{t('channelAdmin.openHere')}</Button>
        <Button href={`https://www.youtube.com/channel/${encodeURIComponent(card.id)}`} external
          >{t('channelAdmin.openYouTube')}</Button
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
    {#if queued}
      <p class="queued">{queued}</p>
    {/if}
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}
    {#if followed}
      <p class="followed" role="status">
        {followed.state === 'queued' || followed.state === 'running'
          ? t('picked.asked', { task: t(`task.${followed.task}`) })
          : runResult(followed)}
        <a href={href.adminRun(followed.id)}>{t('task.follow')}</a>
      </p>
    {/if}

    <div class="two">
      <section class="card" aria-labelledby="channel-title">
        <div class="head">
          <h2 id="channel-title">{t('channelAdmin.onYouTube')}</h2>
          <HelpButton zone={t('channelAdmin.onYouTube')}><p>{t('channelAdmin.refreshHint')}</p></HelpButton>
        </div>
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
          <div>
            <dt>{t('channelAdmin.size')}</dt>
            <dd>
              <b>{formatBytes(size.videos + size.shorts + size.streams, i18n.lang)}</b>
              <!-- By type, the types it has files of. -->
              <span class="sizes">
                {(
                  [
                    ['type.video', size.videos],
                    ['type.short', size.shorts],
                    ['type.stream', size.streams],
                  ] as const
                )
                  .filter(([, bytes]) => bytes > 0)
                  .map(([label, bytes]) => `${t(label)} ${formatBytes(bytes, i18n.lang)}`)
                  .join(' · ')}
              </span>
            </dd>
          </div>
        </dl>
        <div class="actions">
          <Button variant="primary" disabled={pending} onclick={() => void refresh('refreshChannel', card.id)}>
            {t('channelAdmin.refreshChannel')}
          </Button>
          <Button disabled={pending} onclick={() => void refresh('refreshChannelVideos', card.id)}>
            {t('channelAdmin.refreshAll')}
          </Button>
        </div>
        {#if waits}
          <p class="hint">{t('task.waitsItsTurn')}</p>
        {/if}
      </section>

      <section class="card" aria-labelledby="states-title">
        <div class="head">
          <h2 id="states-title">{t('channelAdmin.videosOnYouTube')}</h2>
          <HelpButton zone={t('channelAdmin.videosOnYouTube')}><p>{t('channelAdmin.statesHint')}</p></HelpButton>
        </div>
        <dl class="states">
          {#each states as s (s)}
            <div>
              <dt><YouTubeStateBadge state={s} /></dt>
              <dd class:zero={data.videos[s] === 0}>{number(data.videos[s])}</dd>
            </div>
          {/each}
        </dl>
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
          selected={shownBlocked ? 'blocked' : (shownState ?? 'all')}
          hrefFor={(value) =>
            value === 'all' ? href.adminChannel(id) : `${href.adminChannel(id)}?state=${encodeURIComponent(value)}`}
        />
        {#if videos.total !== undefined}
          <span class="count">{tp('channelAdmin.count', videos.total)}</span>
        {/if}
      </div>

      {#if selecting}
        <div class="selection" role="group" aria-label={t('purge.select')}>
          <span class="picked-count">{tp('purge.selected', selected.size)}</span>
          {#if selected.size > MAX_PICKED}
            <small class="too-many">{t('purge.tooMany', { max: MAX_PICKED })}</small>
          {/if}
          <span class="selection-actions">
            <Button disabled={noPick} onclick={() => void startPicked('refreshVideos', { kind: 'selection' })}>
              <Icon name="refresh" />{t('picked.refresh')}
            </Button>
            <Button disabled={noPick} onclick={() => void askRedownload({ kind: 'selection' })}>
              <Icon name="download" />{t('picked.redownload')}
            </Button>
            <Button disabled={noPick} onclick={() => askPurge({ kind: 'selection' })}>
              <span class="danger"><Icon name="trash" /></span>{t('picked.purge')}
            </Button>
            {#if blockedPicked.length > 0}
              <Button disabled={pending} onclick={() => void unblock(blockedPicked)}>
                <Icon name="unlock" />{t('picked.unblock')}
              </Button>
            {/if}
            <span class="divider" aria-hidden="true"></span>
            <Button disabled={pending} onclick={() => askPurge({ kind: 'channel' })}>{t('purge.channel')}</Button>
            <Button onclick={stopSelecting}>{t('purge.cancel')}</Button>
          </span>
        </div>
      {/if}

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
                {#if selecting}
                  <th class="pick">
                    <input type="checkbox" aria-label={t('purge.selectAll')} checked={allShown} onchange={toggleAll} />
                  </th>
                {/if}
                <th>{t('channelAdmin.video')}</th>
                <th>{t('channelAdmin.type')}</th>
                <th>{t('channelAdmin.published')}</th>
                <th>{t('channelAdmin.onYouTubeColumn')}</th>
                <th>{t('channelAdmin.lastCheck')}</th>
                <!-- Select (2.3.0) over the rows' actions: the boxes, then the selection's bar. -->
                <th class="head-actions">
                  {#if selecting}
                    <span class="visually-hidden">{t('channelAdmin.actions')}</span>
                  {:else}
                    <Button onclick={() => (selecting = true)}>{t('purge.select')}</Button>
                  {/if}
                </th>
              </tr>
            </thead>
            <tbody>
              {#each videos.items as item (item.video.id)}
                {@const video = item.video}
                <tr class:picked={selecting && selected.has(video.id)} class:blocked={item.syncBlocked}>
                  {#if selecting}
                    <td class="pick">
                      <input
                        type="checkbox"
                        aria-label={t('purge.selectOne', { title: video.title })}
                        checked={selected.has(video.id)}
                        onchange={(event) =>
                          event.currentTarget.checked ? selected.add(video.id) : selected.delete(video.id)}
                      />
                    </td>
                  {/if}
                  <td>
                    {#snippet shown()}
                      <span class="thumb"
                        ><Thumbnail src={video.thumbnailUrl} versions={video.thumbnails} sizes="96px" /></span
                      >
                      <span class="title">
                        <b>{video.title}</b>
                        <code>{video.id}</code>
                        {#if item.syncBlocked}<small class="blocked-badge">{t('picked.blocked')}</small>{/if}
                      </span>
                    {/snippet}
                    <!-- A blocked video has no page of its own (404) until it's unblocked: no link. -->
                    {#if item.syncBlocked}
                      <span class="video">{@render shown()}</span>
                    {:else}
                      <a class="video" href={href.watch(video.id)}>{@render shown()}</a>
                    {/if}
                  </td>
                  <td>{t(`type.${video.type}`)}</td>
                  <td>{formatDate(video.published, i18n.lang)}</td>
                  <td><YouTubeStateBadge state={item.youTube.state} /></td>
                  <td class="when">
                    {when(item.youTube.checkedAt)}
                    {#if item.youTube.reason}<small>{item.youTube.reason}</small>{/if}
                  </td>
                  <td class="row-cell">
                    <!-- A gone video isn't read again (1.5): its refresh would be refused. -->
                    <span class="row-actions">
                      {#if item.youTube.state === 'gone'}
                        <small>{t('channelAdmin.goneNotRefreshed')}</small>
                      {/if}
                      {#if item.syncBlocked}
                        <button
                          type="button"
                          class="icon-action"
                          title={t('picked.unblock')}
                          aria-label={t('picked.unblockOne', { title: video.title })}
                          disabled={pending}
                          onclick={() => void unblock([video.id])}
                        >
                          <Icon name="unlock" />
                        </button>
                      {/if}
                      <button
                        type="button"
                        class="icon-action"
                        title={t('picked.refresh')}
                        aria-label={t('picked.refreshOne', { title: video.title })}
                        disabled={pending || item.youTube.state === 'gone'}
                        onclick={() => void refresh('refreshVideo', video.id)}
                      >
                        <Icon name="refresh" />
                      </button>
                      <button
                        type="button"
                        class="icon-action"
                        title={t('picked.redownload')}
                        aria-label={t('picked.redownloadOne', { title: video.title })}
                        disabled={pending}
                        onclick={() => void askRedownload({ kind: 'one', video })}
                      >
                        <Icon name="download" />
                      </button>
                      <button
                        type="button"
                        class="icon-action danger"
                        title={t('picked.purge')}
                        aria-label={t('picked.purgeOne', { title: video.title })}
                        disabled={pending}
                        onclick={() => askPurge({ kind: 'one', video })}
                      >
                        <Icon name="trash" />
                      </button>
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

    <Dialog bind:open={confirming} title={purgeTitle}>
      {#if purging?.kind === 'channel'}
        <p class="sure">
          {t('purge.channelSize', {
            videos: tp('count.videos', total(data)),
            size: formatBytes(size.videos + size.shorts + size.streams, i18n.lang),
          })}
        </p>
      {/if}
      <fieldset class="purge-mode">
        <legend>{t('purge.mode')}</legend>
        <label>
          <input type="radio" name="purge-mode" value={false} bind:group={filesOnly} />
          <span><b>{t('purge.all')}</b><small>{t('purge.allHint')}</small></span>
        </label>
        <label>
          <input type="radio" name="purge-mode" value={true} bind:group={filesOnly} />
          <span><b>{t('purge.filesOnly')}</b><small>{t('purge.filesOnlyHint')}</small></span>
        </label>
      </fieldset>
      {#snippet actions()}
        <Button
          variant="primary"
          disabled={pending}
          onclick={() => void startPicked('purgeVideos', purging, filesOnly ? { filesOnly } : {})}
        >
          {t('purge.confirm')}
        </Button>
        <Button onclick={() => (confirming = false)}>{t('purge.cancel')}</Button>
      {/snippet}
    </Dialog>

    <Dialog bind:open={askingDownload} title={redownloadTitle}>
      <p>{t('redownload.what')}</p>
      <DownloadOnce {settings} bind:format bind:withoutCookie disabled={pending} />
      {#snippet actions()}
        <Button
          variant="primary"
          disabled={pending}
          onclick={() =>
            void startPicked('redownloadVideos', redownloading, downloadOnce(settings, format, withoutCookie))}
        >
          {t('redownload.confirm')}
        </Button>
        <Button onclick={() => (askingDownload = false)}>{t('purge.cancel')}</Button>
      {/snippet}
    </Dialog>
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

  .busy {
    color: var(--color-running);
    font-weight: var(--font-weight-medium);
  }

  /* "Follow its log" (2.1.1): a link in the sentence, so it looks like one, not like the sentence's teal. */
  .busy a {
    margin-left: var(--space-2);
    color: var(--color-link);
    text-decoration: underline;
    text-underline-offset: var(--underline-offset);
  }

  .queued,
  .followed {
    color: var(--color-text-secondary);
  }

  /* "Follow its log": a link in the sentence, as the running line's. */
  .followed a {
    margin-left: var(--space-2);
    color: var(--color-link);
    text-decoration: underline;
    text-underline-offset: var(--underline-offset);
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

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
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

  /* Its videos by state (2.1.1): the badges in a column as wide as the widest one, the counts in a right-aligned one,
     each count beside its badge's middle; a zero in the secondary colour, so the counts that matter stand out. The
     other cards' label column (--task-label-width) is too narrow for "On YouTube" or "Not checked". */
  .states {
    grid-template-columns: max-content max-content;
    justify-content: start;
    gap: var(--space-2) var(--space-6);
  }

  .states div {
    display: contents;
  }

  .states dt {
    display: flex;
    align-items: center;
    color: var(--color-text);
    font-size: var(--font-size-sm);
  }

  .states dd {
    justify-content: flex-end;
    align-items: center;
    font-size: var(--font-size-md);
  }

  .sizes {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .states dd.zero {
    color: var(--color-text-secondary);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* The rows' actions (2.3.0): quiet icons on the right, the purge's in red; each named for screen readers. */
  .row-cell {
    text-align: right;
  }

  .row-actions {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
  }

  .row-actions small {
    margin-right: var(--space-2);
  }

  .icon-action {
    display: grid;
    place-items: center;
    width: var(--icon-button-size);
    height: var(--icon-button-size);
    border: none;
    border-radius: var(--radius-pill);
    background: transparent;
    color: var(--color-text-secondary);
  }

  .icon-action:hover:not(:disabled) {
    background: var(--color-surface-hover);
    color: var(--color-text);
  }

  .icon-action.danger {
    color: var(--color-error);
  }

  .icon-action.danger:hover:not(:disabled) {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .icon-action:disabled {
    opacity: 0.5;
    cursor: default;
  }

  /* The purge's choice (2.3.0): a radio on each line, its explanation under its name. */
  .purge-mode {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: 0;
    border: 0;
  }

  .purge-mode legend {
    margin-bottom: var(--space-2);
    font-weight: var(--font-weight-medium);
  }

  .purge-mode label {
    display: flex;
    align-items: flex-start;
    gap: var(--space-2);
  }

  .purge-mode label span {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .purge-mode small {
    color: var(--color-text-secondary);
  }

  /* The trash of the selection's purge: red, as the rows'. */
  .danger {
    display: inline-flex;
    color: var(--color-error);
  }

  /* Select (2.3.0): a quiet bar over the table, what's ticked on the left, its actions on the right. */
  .selection {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-3);
    padding: var(--space-2) var(--space-2) var(--space-2) var(--space-4);
    border-radius: var(--radius-lg);
    background: var(--color-surface);
  }

  .picked-count {
    font-weight: var(--font-weight-medium);
  }

  .too-many {
    color: var(--color-error);
  }

  .selection-actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin-left: auto;
  }

  /* Between what's done to the ones ticked and the rest. */
  .divider {
    align-self: stretch;
    width: var(--border-width);
    margin: var(--space-1) var(--space-1);
    background: var(--color-divider);
  }

  /* Select, against the right edge over the rows' buttons. */
  th.head-actions {
    padding-block: var(--space-2);
    text-align: right;
  }

  /* The boxes' column: as narrow as a box. */
  .pick {
    width: 0;
    padding-right: 0;
  }

  .pick input {
    display: block;
  }

  /* A ticked row: the selection bar's grey. */
  tr.picked td {
    background: var(--color-surface);
  }

  /*
   * A video whose syncs are blocked (2.3.0): its row greyed at a glance, its thumbnail in black and white; its
   * actions' cell left as the others'.
   */
  tr.blocked td:not(.row-cell) {
    opacity: 0.6;
  }

  tr.blocked .thumb {
    filter: grayscale(1);
  }

  .blocked-badge {
    align-self: flex-start;
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    font-size: var(--font-size-xs);
  }

  .sure {
    color: var(--color-text);
    font-weight: var(--font-weight-medium);
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
