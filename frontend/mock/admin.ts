// The mock's admin side (2.1.0): logged in as `admin` when /__mock says so, the admin pages answered from the
// synthetic library (counts and states derived from the fixtures), the tasks, runs, keys and settings from small
// in-memory state, so the actions (start a task, subscribe, change a setting, make a key…) answer as the backend's
// would and show on the next read. Pure but for that state: the plugin passes the request's JSON body.
import type {
  AdminChannel,
  AdminVideo,
  ApiKey,
  ChannelTabs,
  ChannelTotals,
  Dashboard,
  DownloadFailure,
  ImportHistory,
  ImportPeriod,
  Run,
  RunLog,
  RunTask,
  Subscription,
  Tasks,
  VideoCard,
  YouTubeOverview,
  YouTubeSettings,
  YouTubeState,
  YtdlpState,
  YtdlpVersions,
} from '../src/lib/api/types.ts'
import type { ChannelFixture, MockDb, VideoFixture } from './db.ts'

export interface AdminMockState {
  loggedIn: boolean
  /** 2.2.0 mockup: the videos folder's disk, as /__mock sets it: roomy, almost full, or not saying. */
  disk: 'normal' | 'low' | 'unknown'
  runs: Run[]
  keys: ApiKey[]
  settings: YouTubeSettings
  subscriptions: Map<string, Subscription>
  cookie: YouTubeOverview['cookie']
  ytdlp: YtdlpState
  nextKey: number
}

export interface AdminAnswer {
  status: number
  body?: unknown
}

const DAY = 86_400_000
const at = (daysAgo: number, now: number) => new Date(now - daysAgo * DAY).toISOString()

const allTabs: ChannelTabs = { videos: true, shorts: true, streams: true }

const noCounts: Run['counts'] = {
  videosRead: 0,
  channelsRead: 0,
  created: 0,
  updated: 0,
  unchanged: 0,
  rejected: 0,
  corrected: 0,
  artCopied: 0,
  artFailed: 0,
  artResized: 0,
  artResizeFailed: 0,
  mediaMissing: 0,
  foundOnYouTube: 0,
  unlistedOnYouTube: 0,
  goneFromYouTube: 0,
  privateOnYouTube: 0,
  checkFailed: 0,
  commentsRead: 0,
  subscribed: 0,
  downloadFailed: 0,
}

function makeRun(task: RunTask, overrides: Partial<Run>, now: number): Run {
  const startedAt = overrides.startedAt ?? at(0, now)
  return {
    id: `run-${task}-${startedAt.replace(/\D/g, '').slice(0, 14)}`,
    task,
    trigger: 'admin',
    startedBy: 'admin',
    state: 'succeeded',
    startedAt,
    queuedAt: startedAt,
    position: null,
    finishedAt: new Date(Date.parse(startedAt) + 95_000).toISOString(),
    error: null,
    counts: noCounts,
    target: null,
    tabs: null,
    resumeAt: null,
    left: 0,
    indexRebuild: false,
    notes: [],
    cancellable: false,
    ...overrides,
  }
}

/** A fresh admin state: logged out, a few runs of the last days, one key, the default settings. */
export function adminMockState(db: MockDb, now: number = Date.now()): AdminMockState {
  const first = db.channels[0]
  const runs: Run[] = [
    makeRun(
      'import',
      {
        trigger: 'startup',
        startedBy: null,
        startedAt: at(6, now),
        counts: {
          ...noCounts,
          videosRead: db.videos.length,
          channelsRead: db.channels.length,
          created: db.videos.length,
        },
      },
      now,
    ),
    makeRun(
      'youTubeSync',
      {
        trigger: 'scheduler',
        startedBy: null,
        startedAt: at(1.2, now),
        counts: { ...noCounts, channelsRead: db.channels.length, created: 4, downloadFailed: 1 },
        notes: ['"Minute Méca": 4 videos to download.'],
      },
      now,
    ),
    makeRun(
      'refreshChannelVideos',
      {
        startedAt: at(0.4, now),
        target: first?.id ?? null,
        counts: { ...noCounts, videosRead: 24, foundOnYouTube: 22, goneFromYouTube: 1, unlistedOnYouTube: 1 },
      },
      now,
    ),
  ]
  const subscriptions = new Map<string, Subscription>()
  db.channels.forEach((channel, index) => {
    if (index % 3 !== 2) {
      subscriptions.set(channel.id, {
        tabs: index % 2 === 0 ? allTabs : { videos: true, shorts: false, streams: true },
        since: at(30 + index, now),
        by: index === 0 ? null : 'admin',
        origin: index === 0 ? 'tubeArchivist' : 'tonTuyau',
      })
    }
  })
  return {
    loggedIn: false,
    disk: 'normal',
    runs,
    keys: [
      {
        id: 1,
        name: 'Firefox, laptop',
        prefix: 'tt_k7Qm',
        kind: 'browserExtension',
        enabled: true,
        createdAt: at(12, now),
        createdBy: 'admin',
        lastUsedAt: at(0.02, now),
      },
    ],
    settings: {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      // 2.2.0 mockup: the disk space settings.
      ...{ minFreeGb: 50, holdBelowMinFree: true },
      defaultTabs: allTabs,
      changedBy: null,
      changedAt: null,
    },
    subscriptions,
    cookie: {
      receivedAt: at(0.02, now),
      receivedBy: 'Firefox, laptop',
      state: 'works',
      checkedAt: at(0.02, now),
      reason: null,
    },
    ytdlp: {
      choice: { kind: 'latest', version: null, chosenBy: null, chosenAt: null },
      running: '2026.10.10',
      bundled: false,
      waiting: null,
      checkedAt: at(0.3, now),
      error: null,
    },
    nextKey: 2,
  }
}

/** A fixture's state on YouTube, stable per video (the same as the public side's badges). */
function stateOf(video: VideoFixture): YouTubeState {
  if (video.youTubeState) return video.youTubeState
  if (!video.active) return 'gone'
  const sum = [...video.id].reduce((total, char) => total + char.charCodeAt(0), 0)
  return sum % 23 === 0 ? 'private' : sum % 19 === 0 ? 'unlisted' : sum % 7 === 0 ? 'retryLater' : 'onYouTube'
}

function card(db: MockDb, video: VideoFixture): VideoCard {
  const channel = db.channels.find((candidate) => candidate.id === video.channelId) as ChannelFixture
  return {
    id: video.id,
    title: video.title,
    type: video.type,
    published: video.published,
    durationSeconds: video.durationSeconds,
    views: video.views,
    active: video.active,
    youTubeState: stateOf(video),
    thumbnailUrl: video.thumbnailUrl,
    thumbnails: video.thumbnails,
    channel: { id: channel.id, name: channel.name, thumbnailUrl: channel.thumbnailUrl, thumbnails: channel.thumbnails },
  }
}

function totals(db: MockDb, channel: ChannelFixture): ChannelTotals {
  const own = db.videos.filter((video) => video.channelId === channel.id)
  const count = (type: VideoFixture['type']) => own.filter((video) => video.type === type).length
  const states = (state: YouTubeState) => own.filter((video) => stateOf(video) === state).length
  return {
    id: channel.id,
    name: channel.name,
    thumbnailUrl: channel.thumbnailUrl,
    thumbnails: channel.thumbnails,
    counts: { videos: count('video'), shorts: count('short'), streams: count('stream') },
    gone: states('gone'),
    private: states('private'),
    unlisted: states('unlisted'),
    retryLater: states('retryLater'),
    notChecked: states('notChecked'),
    total: own.length,
    // 2.2.0 mockup: the size of its files (not in the API yet).
    ...{ size: own.reduce((sum, video) => sum + video.fileSize, 0) },
  }
}

const TB = 1e12

/**
 * 2.2.0 mockup (docs/PLAN_2.2.0.md): the storage, from the fixtures' file sizes; the disk 1 TB, 412 GB free (or almost
 * full, or not saying, as /__mock sets it); TonTuyau's own data made up.
 */
/** 2.2.0 mockup: the disk space settings, kept with the others (not in the API's type yet). */
function space(state: AdminMockState): { minFreeGb: number; holdBelowMinFree: boolean } {
  const settings = state.settings as YouTubeSettings & { minFreeGb?: number; holdBelowMinFree?: boolean }
  return { minFreeGb: settings.minFreeGb ?? 50, holdBelowMinFree: settings.holdBelowMinFree ?? true }
}

/** The disk's free space, as /__mock sets it; null when it doesn't say. */
const freeOf = (state: AdminMockState) =>
  state.disk === 'unknown' ? null : state.disk === 'low' ? 0.04 * TB : 0.412 * TB

/** Downloads don't run (2.2.0 plan): under the threshold, the switch on. */
function held(state: AdminMockState): boolean {
  const free = freeOf(state)
  const { minFreeGb, holdBelowMinFree } = space(state)
  return holdBelowMinFree && minFreeGb > 0 && free !== null && free < minFreeGb * 1e9
}

function storage(db: MockDb, state: AdminMockState) {
  const types = (['video', 'short', 'stream'] as const).map((type) => {
    const own = db.videos.filter((video) => video.type === type)
    return { type, bytes: own.reduce((sum, video) => sum + video.fileSize, 0), count: own.length }
  })
  const free = freeOf(state)
  const { minFreeGb, holdBelowMinFree } = space(state)
  return {
    disk: free === null ? null : { total: TB, free },
    types,
    minFree: minFreeGb * 1e9,
    holdDownloads: holdBelowMinFree,
  }
}

function dashboard(db: MockDb, state: AdminMockState): Dashboard {
  const count = (type: VideoFixture['type']) => db.videos.filter((video) => video.type === type)
  const seconds = (videos: VideoFixture[]) => videos.reduce((sum, video) => sum + video.durationSeconds, 0)
  const all = seconds(db.videos) || 1
  const latest = state.runs.filter((run) => run.counts.created > 0 && run.finishedAt).at(-1)
  return {
    ...{ storage: storage(db, state) },
    lastSync: latest?.finishedAt ?? null,
    totals: {
      channels: db.channels.length,
      videos: count('video').length,
      shorts: count('short').length,
      streams: count('stream').length,
      gone: db.videos.filter((video) => stateOf(video) === 'gone').length,
    },
    playTime: {
      totalSeconds: all,
      types: (['video', 'short', 'stream'] as const).map((type) => {
        const videos = count(type)
        return {
          type,
          count: videos.length,
          seconds: seconds(videos),
          share: seconds(videos) / all,
          averageSeconds: videos.length ? Math.round(seconds(videos) / videos.length) : 0,
        }
      }),
    },
  }
}

/** The new videos per day (or week, month), from the runs that added some: the import, then the syncs. */
function imports(state: AdminMockState, period: ImportPeriod, now: number): ImportHistory {
  const steps = period === 'day' ? 24 : period === 'week' ? 7 : period === 'month' ? 30 : 52
  const step = period === 'day' ? 'run' : period === 'year' ? 'week' : period === 'all' ? 'month' : 'day'
  const bars = Array.from({ length: steps }, (_, index) => {
    const start = new Date(now - (steps - index) * (period === 'year' ? 7 * DAY : DAY))
    start.setUTCHours(0, 0, 0, 0)
    const added = state.runs.filter(
      (run) => run.finishedAt && Math.abs(Date.parse(run.finishedAt) - start.getTime()) < DAY / 2,
    )
    const total = added.reduce((sum, run) => sum + run.counts.created, 0)
    return {
      start: start.toISOString(),
      total,
      byType: {
        videos: Math.ceil(total * 0.7),
        shorts: Math.floor(total * 0.2),
        streams: total - Math.ceil(total * 0.7) - Math.floor(total * 0.2),
      },
      notRecorded: 0,
    }
  })
  return { step, timeZone: 'Europe/Paris', total: bars.reduce((sum, bar) => sum + bar.total, 0), bars }
}

function tasks(state: AdminMockState, now: number): Tasks {
  const last = (task: RunTask) => state.runs.filter((run) => run.task === task).at(-1) ?? null
  const running = state.runs.find((run) => run.state === 'running') ?? null
  return {
    lock: running ? { task: running.task, trigger: running.trigger, since: running.startedAt } : null,
    runningHere: running?.id ?? null,
    // The backend's list (TaskService): the pages add their own cards for the one-off tasks.
    tasks: (['import', 'youTubeSync', 'youTubeFullSync', 'refreshAll'] as const).map((task) => ({
      task,
      lastRun: last(task),
      unavailable:
        task === 'import'
          ? 'TubeArchivist isn’t set (TONTUYAU_TA_URL, TONTUYAU_TA_TOKEN).'
          : (task === 'youTubeSync' || task === 'youTubeFullSync') && held(state)
            ? `Under ${space(state).minFreeGb} GB free on the videos folder’s disk: the downloads wait until space is freed (the YouTube page sets it).`
            : null,
      next: task === 'youTubeSync' ? new Date(now + state.settings.syncEveryMinutes * 60_000).toISOString() : null,
    })),
    onHold: state.runs.filter((run) => run.state === 'onHold'),
    queue: state.runs.filter((run) => run.state === 'queued'),
  }
}

function log(run: Run): RunLog {
  const lines = [
    `${run.task} started (${run.trigger}).`,
    ...run.notes,
    run.state === 'succeeded' ? 'Done.' : `Ended: ${run.state}.`,
  ]
  return {
    lines: lines.map((message, index) => ({
      id: index + 1,
      at: new Date(Date.parse(run.startedAt) + index * 1000).toISOString(),
      level: run.error && index === lines.length - 1 ? 'error' : 'information',
      message,
    })),
    complete: run.state !== 'running' && run.state !== 'queued',
  }
}

function failures(db: MockDb, channelId: string, now: number): DownloadFailure[] {
  const own = db.videos.filter((video) => video.channelId === channelId).slice(0, 2)
  return own.map((video, index) => ({
    videoId: video.id,
    title: video.title,
    reason: index === 0 ? 'This video is available to this channel’s members' : 'Requested format is not available',
    tries: index === 0 ? 3 : 1,
    skipped: index === 0,
    firstFailedAt: at(5 - index, now),
    lastFailedAt: at(1 - index * 0.5, now),
    youTubeUrl: `https://www.youtube.com/watch?v=${video.id}`,
  }))
}

const notFound = (detail: string): AdminAnswer => ({
  status: 404,
  body: { type: 'https://tontuyau/errors/not-found', title: 'Not found', status: 404, detail },
})

function page<T>(items: T[]) {
  return { items, nextCursor: null, total: items.length }
}

/**
 * Answers /api/auth/* and /api/admin/*; null for any other path (the public handler's). Logged out, the admin
 * endpoints answer 401 as the backend's.
 */
export function handleAdmin(
  db: MockDb,
  state: AdminMockState,
  method: string,
  url: URL,
  body: unknown,
  now: number = Date.now(),
): AdminAnswer | null {
  const path = url.pathname.replace(/\/+$/, '')
  const params = url.searchParams
  if (path === '/api/auth/me') {
    return state.loggedIn
      ? { status: 200, body: { username: 'admin' } }
      : { status: 401, body: { title: 'Not logged in', status: 401, detail: 'Nobody is logged in.' } }
  }
  if (path === '/api/auth/login' && method === 'POST') {
    state.loggedIn = true
    return { status: 204 }
  }
  if (path === '/api/auth/logout' && method === 'POST') {
    state.loggedIn = false
    return { status: 204 }
  }
  if (!path.startsWith('/api/admin/')) return null
  if (!state.loggedIn) return { status: 401, body: { title: 'Not logged in', status: 401, detail: 'Log in first.' } }

  const [resource, id, sub] = path.split('/').slice(3).map(decodeURIComponent)
  const channel = id ? db.channels.find((candidate) => candidate.id === id) : undefined

  if (resource === 'dashboard' && id === undefined) return { status: 200, body: dashboard(db, state) }
  if (resource === 'dashboard' && id === 'channels') {
    const q = (params.get('q') ?? '').toLowerCase()
    const rows = db.channels
      .map((candidate) => totals(db, candidate))
      .filter((row) => row.name.toLowerCase().includes(q) && (params.get('gone') !== 'true' || row.gone > 0))
      .sort((a, b) => {
        // Any column, as the backend sorts them (2.2.0 mockup: the size too); names A to Z, numbers biggest first.
        const key = params.get('sort') ?? 'total'
        const value = (row: ChannelTotals): number | string =>
          key === 'name'
            ? row.name.toLowerCase()
            : key in row.counts
              ? row.counts[key as keyof ChannelTotals['counts']]
              : ((row as unknown as Record<string, number>)[key] ?? 0)
        const [x, y] = [value(a), value(b)]
        const order = x < y ? -1 : x > y ? 1 : 0
        return params.get('order') === 'asc' ? order : -order
      })
    return { status: 200, body: page(rows) }
  }
  if (resource === 'dashboard' && id === 'imports') {
    return { status: 200, body: imports(state, (params.get('period') ?? 'month') as ImportPeriod, now) }
  }

  if (resource === 'tasks' && id === undefined) return { status: 200, body: tasks(state, now) }
  if (resource === 'tasks' && sub === 'runs' && method === 'POST') {
    const run = makeRun(
      id as RunTask,
      {
        state: 'succeeded',
        target: params.get('target'),
        counts: { ...noCounts, created: id === 'downloadVideo' ? 1 : 0 },
      },
      now,
    )
    state.runs.push(run)
    return { status: 202, body: run }
  }

  if (resource === 'runs' && id === undefined) {
    const task = params.get('task')
    return { status: 200, body: page([...state.runs].reverse().filter((run) => !task || run.task === task)) }
  }
  if (resource === 'runs' && id !== undefined) {
    const run = state.runs.find((candidate) => candidate.id === id)
    if (!run) return notFound(`No run ${id}.`)
    if (sub === undefined) return { status: 200, body: run }
    if (sub === 'log') return { status: 200, body: log(run) }
    if (method === 'POST' && (sub === 'cancel' || sub === 'resume' || sub === 'hold')) {
      run.state = sub === 'cancel' ? 'canceled' : sub === 'hold' ? 'onHold' : 'succeeded'
      return { status: 200, body: run }
    }
  }

  if (resource === 'channels' && id !== undefined) {
    if (!channel) return notFound(`No channel ${id}.`)
    const own = db.videos.filter((video) => video.channelId === channel.id)
    if (sub === undefined) {
      const counts = (state: YouTubeState) => own.filter((video) => stateOf(video) === state).length
      const answer: AdminChannel = {
        channel: {
          id: channel.id,
          name: channel.name,
          subscribers: channel.subscribers,
          thumbnailUrl: channel.thumbnailUrl,
          thumbnails: channel.thumbnails,
          videoCounts: totals(db, channel).counts,
        },
        youTube: { state: 'onYouTube', checkedAt: at(0.4, now), reason: null, refreshedAt: at(0.4, now) },
        videos: {
          notChecked: counts('notChecked'),
          onYouTube: counts('onYouTube'),
          unlisted: counts('unlisted'),
          private: counts('private'),
          retryLater: counts('retryLater'),
          gone: counts('gone'),
        },
        subscription: state.subscriptions.get(channel.id) ?? null,
        // 2.2.0 mockup: its files' sizes by type.
        ...{
          size: {
            videos: own.filter((video) => video.type === 'video').reduce((sum, video) => sum + video.fileSize, 0),
            shorts: own.filter((video) => video.type === 'short').reduce((sum, video) => sum + video.fileSize, 0),
            streams: own.filter((video) => video.type === 'stream').reduce((sum, video) => sum + video.fileSize, 0),
          },
        },
      }
      return { status: 200, body: answer }
    }
    if (sub === 'videos') {
      const wanted = params.get('state')
      const q = (params.get('q') ?? '').toLowerCase()
      const items: AdminVideo[] = own
        .filter((video) => (!wanted || stateOf(video) === wanted) && video.title.toLowerCase().includes(q))
        .sort((a, b) => b.published.localeCompare(a.published))
        .map((video) => ({
          video: card(db, video),
          youTube: { state: stateOf(video), checkedAt: at(0.4, now), reason: null, refreshedAt: at(0.4, now) },
        }))
      return { status: 200, body: page(items) }
    }
    if (sub === 'failed-downloads') return { status: 200, body: failures(db, channel.id, now) }
    if (sub === 'subscription' && method === 'PUT') {
      const tabs = body as ChannelTabs
      const before = state.subscriptions.get(channel.id)
      const subscription: Subscription = {
        tabs: { videos: tabs.videos, shorts: tabs.shorts, streams: tabs.streams },
        since: before?.since ?? at(0, now),
        by: before?.by ?? 'admin',
        origin: before?.origin ?? 'tonTuyau',
      }
      state.subscriptions.set(channel.id, subscription)
      return { status: 200, body: subscription }
    }
    if (sub === 'subscription' && method === 'DELETE') {
      state.subscriptions.delete(channel.id)
      return { status: 204 }
    }
  }

  if (resource === 'youtube' && id === undefined) {
    const overview: YouTubeOverview = { cookie: state.cookie, ytdlp: state.ytdlp.running, deno: '2.9.7' }
    return { status: 200, body: overview }
  }
  if (resource === 'youtube' && id === 'cookie' && method === 'DELETE') {
    state.cookie = null
    return { status: 204 }
  }
  if (resource === 'youtube' && id === 'cookie' && sub === 'check') {
    if (state.cookie) state.cookie = { ...state.cookie, checkedAt: new Date(now).toISOString() }
    return { status: 200, body: state.cookie }
  }
  if (resource === 'youtube' && id === 'settings') {
    if (method === 'PUT') {
      state.settings = {
        ...state.settings,
        ...(body as Partial<YouTubeSettings>),
        changedBy: 'admin',
        changedAt: new Date(now).toISOString(),
      }
    }
    return { status: 200, body: state.settings }
  }
  if (resource === 'youtube' && id === 'ytdlp' && sub === undefined) {
    if (method === 'PUT') {
      const choice = body as { kind: YtdlpState['choice']['kind']; version: string | null }
      state.ytdlp = {
        ...state.ytdlp,
        choice: {
          kind: choice.kind,
          version: choice.version,
          chosenBy: 'admin',
          chosenAt: new Date(now).toISOString(),
        },
      }
    }
    return { status: 200, body: state.ytdlp }
  }
  if (resource === 'youtube' && id === 'ytdlp' && sub === 'versions') {
    const versions: YtdlpVersions = {
      official: ['2026.10.10', '2026.09.27', '2026.09.12', '2026.08.19'].map((version, index) => ({
        version,
        publishedAt: at(12 + index * 14, now),
      })),
      nightly: ['2026.10.21.232011', '2026.10.19.231502'].map((version, index) => ({
        version,
        publishedAt: at(1 + index * 2, now),
      })),
    }
    return { status: 200, body: versions }
  }

  if (resource === 'api-keys' && id === undefined) {
    if (method === 'POST') {
      const request = body as { name: string; kind: ApiKey['kind'] }
      const key: ApiKey = {
        id: state.nextKey++,
        name: request.name,
        prefix: `tt_m${state.nextKey}x`,
        kind: request.kind,
        enabled: true,
        createdAt: new Date(now).toISOString(),
        createdBy: 'admin',
        lastUsedAt: null,
      }
      state.keys.push(key)
      return { status: 201, body: { key, secret: `${key.prefix}-mock-secret-${key.id}` } }
    }
    return { status: 200, body: state.keys }
  }
  if (resource === 'api-keys' && id !== undefined) {
    const key = state.keys.find((candidate) => candidate.id === Number(id))
    if (!key) return notFound(`No API key ${id}.`)
    if (method === 'DELETE') {
      state.keys = state.keys.filter((candidate) => candidate !== key)
      return { status: 204 }
    }
    if (sub === 'enable' || sub === 'disable') {
      key.enabled = sub === 'enable'
      return { status: 200, body: key }
    }
  }
  return notFound(`No endpoint ${method} ${path} in the mock.`)
}
