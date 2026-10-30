// Typed client for docs/API_CONTRACT.md. Same code in mock mode and real mode: it always calls /api on the
// page's origin; the dev server decides whether that's the mock or the backend (vite.config.ts).
import type {
  Access,
  AccessSettings,
  Account,
  AccountChange,
  LdapSettings,
  LdapSettingsChange,
  LdapTest,
  LdapTestRequest,
  AdminChannel,
  AdminVideo,
  ApiKey,
  ApiKeyKind,
  ChannelCard,
  ChannelDetail,
  ChannelListParams,
  ChannelTabs,
  ChannelTotals,
  ChannelVideoListParams,
  Comments,
  CreatedApiKey,
  DashboardChannelParams,
  DownloadFailure,
  Dashboard,
  Home,
  ImportHistory,
  ImportPeriod,
  LoginRequest,
  Me,
  NewAccount,
  Page,
  ProblemDetails,
  Run,
  RunListParams,
  RunLog,
  RunTask,
  SearchAll,
  SearchParams,
  Subscription,
  Suggestions,
  Tasks,
  VideoCard,
  VideoDetail,
  VideoListParams,
  YouTubeCommentsChoice,
  YouTubeCookie,
  YouTubeOverview,
  YouTubeSettings,
  YouTubeState,
  YtdlpChoiceKind,
  YtdlpState,
  YtdlpVersions,
} from './types'

/** An API failure. `problem` is the server's ProblemDetails, or one built here when there was none. */
export class ApiError extends Error {
  readonly problem: ProblemDetails
  /**
   * True when the answer was the backend's own ProblemDetails. A bare error page (a proxy whose backend is down) or
   * no answer at all (network) is false.
   */
  readonly fromServer: boolean

  constructor(problem: ProblemDetails, fromServer = false) {
    super(problem.detail ?? problem.title)
    this.name = 'ApiError'
    this.problem = problem
    this.fromServer = fromServer
  }

  get status(): number {
    return this.problem.status
  }

  /** True when the server was never reached (network down, dev server stopped). */
  get isNetworkError(): boolean {
    return this.problem.status === 0
  }
}

type QueryValue = string | number | boolean | null | undefined
type Query = Record<string, QueryValue>

/** Builds `path?k=v…`, leaving out empty values so the backend applies its own defaults. */
export function buildUrl(path: string, query: Query = {}): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value))
    }
  }
  const text = params.toString()
  return text ? `${path}?${text}` : path
}

async function readError(response: Response): Promise<ApiError> {
  const contentType = response.headers.get('content-type') ?? ''
  if (contentType.includes('json')) {
    try {
      const body = (await response.json()) as Partial<ProblemDetails>
      return new ApiError(
        { ...body, title: body.title ?? response.statusText, status: body.status ?? response.status },
        true,
      )
    } catch {
      // Not JSON after all: fall through to the generic problem.
    }
  }
  return new ApiError({ title: response.statusText || 'Error', status: response.status })
}

async function send(url: string, init: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch (error) {
    if (init.signal?.aborted) {
      throw error
    }
    throw new ApiError({ title: 'Network error', status: 0 })
  }
  if (!response.ok) {
    throw await readError(response)
  }
  return response
}

async function get<T>(path: string, query: Query = {}, signal?: AbortSignal): Promise<T> {
  const response = await send(buildUrl(path, query), { headers: { accept: 'application/json' }, signal })
  return (await response.json()) as T
}

/** A POST whose answer has no body (204). The backend takes JSON only. */
async function post(path: string, body?: unknown): Promise<void> {
  await send(path, {
    method: 'POST',
    headers: { accept: 'application/json', ...(body === undefined ? {} : { 'content-type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

/** A POST whose answer has a body (1.4: starting a task, canceling a run: 202 and the run; 1.5: a key). */
async function postFor<T>(path: string, body?: unknown): Promise<T> {
  const response = await send(path, {
    method: 'POST',
    headers: { accept: 'application/json', ...(body === undefined ? {} : { 'content-type': 'application/json' }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  return (await response.json()) as T
}

/** A PUT of JSON whose answer has a body (1.5: choosing yt-dlp). */
async function putFor<T>(path: string, body: unknown): Promise<T> {
  const response = await send(path, {
    method: 'PUT',
    headers: { accept: 'application/json', 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
  return (await response.json()) as T
}

/** A DELETE (1.5: an API key, the YouTube cookie): 204, no body. */
/** A PUT of JSON whose answer has no body (204). */
async function put(path: string, body: unknown): Promise<void> {
  await send(path, {
    method: 'PUT',
    headers: { accept: 'application/json', 'content-type': 'application/json' },
    body: JSON.stringify(body),
  })
}

async function remove(path: string): Promise<void> {
  await send(path, { method: 'DELETE', headers: { accept: 'application/json' } })
}

const segment = encodeURIComponent

/** The tabs ticked, as the API names them: `videos,shorts,streams` or some of them. */
function tabNames(tabs: ChannelTabs): string {
  return (['videos', 'shorts', 'streams'] as const).filter((tab) => tabs[tab]).join(',')
}

export const api = {
  home(size?: number, signal?: AbortSignal): Promise<Home> {
    return get('/api/home', { size }, signal)
  },

  videos(params: VideoListParams, signal?: AbortSignal): Promise<Page<VideoCard>> {
    return get('/api/videos', { ...params }, signal)
  },

  video(id: string, signal?: AbortSignal): Promise<VideoDetail> {
    return get(`/api/videos/${segment(id)}`, {}, signal)
  },

  channels(params: ChannelListParams, signal?: AbortSignal): Promise<Page<ChannelCard>> {
    return get('/api/channels', { ...params }, signal)
  },

  channel(id: string, signal?: AbortSignal): Promise<ChannelDetail> {
    return get(`/api/channels/${segment(id)}`, {}, signal)
  },

  channelVideos(id: string, params: ChannelVideoListParams, signal?: AbortSignal) {
    return get<Page<VideoCard>>(`/api/channels/${segment(id)}/videos`, { ...params }, signal)
  },

  searchAll(params: SearchParams, signal?: AbortSignal): Promise<SearchAll> {
    return get('/api/search', { ...params, kind: 'all' }, signal)
  },

  searchVideos(params: SearchParams, signal?: AbortSignal): Promise<Page<VideoCard>> {
    return get('/api/search', { ...params, kind: 'videos' }, signal)
  },

  searchChannels(params: SearchParams, signal?: AbortSignal): Promise<Page<ChannelCard>> {
    return get('/api/search', { ...params, kind: 'channels' }, signal)
  },

  suggest(q: string, size?: number, signal?: AbortSignal): Promise<Suggestions> {
    return get('/api/search/suggest', { q, size }, signal)
  },

  me(): Promise<Me> {
    return get('/api/auth/me')
  },

  login(request: LoginRequest): Promise<void> {
    return post('/api/auth/login', request)
  },

  logout(): Promise<void> {
    return post('/api/auth/logout')
  },

  /** Whether the library is for logged-in accounts only (2.3.0), open to anyone. */
  access(): Promise<Access> {
    return get('/api/auth/access')
  },

  /** A local account changes its own password (2.3.0): 400 when the current one is wrong, 409 for a directory one. */
  changeMyPassword(current: string, password: string): Promise<void> {
    return put('/api/auth/me/password', { current, password })
  },

  /** The admin dashboard (1.2): 401 when nobody is logged in. */
  dashboard(signal?: AbortSignal): Promise<Dashboard> {
    return get('/api/admin/dashboard', {}, signal)
  },

  /** The dashboard's channels with their counts (alpha.3): by name, with videos gone, sorted, page by page. */
  dashboardChannels(params: DashboardChannelParams, signal?: AbortSignal): Promise<Page<ChannelTotals>> {
    return get('/api/admin/dashboard/channels', params, signal)
  },

  /** The dashboard's import history of a period (1.2): 401 when nobody is logged in. */
  imports(period: ImportPeriod, signal?: AbortSignal): Promise<ImportHistory> {
    return get('/api/admin/dashboard/imports', { period }, signal)
  },

  /** The Tasks page's tasks and the run going on (1.4): 401 when nobody is logged in. */
  tasks(signal?: AbortSignal): Promise<Tasks> {
    return get('/api/admin/tasks', {}, signal)
  },

  /** The run history, latest first (1.4): `before` is the previous page's nextCursor. */
  runs(params: RunListParams, signal?: AbortSignal): Promise<Page<Run>> {
    return get('/api/admin/runs', { ...params }, signal)
  },

  /** A run (1.4): 404 when there's none with that id. */
  run(id: string, signal?: AbortSignal): Promise<Run> {
    return get(`/api/admin/runs/${segment(id)}`, {}, signal)
  },

  /**
   * Asks for a task (2.0: queued, started at once when nothing else goes on): the run. A refresh (1.5) takes its
   * `target`, a video's or channel's id; "Add a channel" (2.0) its id or @handle, and the tabs to subscribe to;
   * "Refresh everything" (2.0) the days within which what was read is skipped; a download (2.2) yt-dlp's format this
   * once; "Download everything" of a channel (2.3.0) the format and the retry without the cookie this once; a purge
   * (2.3.0) the channel's videos to purge, all of them without. 409 when the target isn't one of the library (a
   * refresh, a purge), or is already (a download, a channel subscribed to).
   */
  startTask(
    task: RunTask,
    target?: string,
    tabs?: ChannelTabs,
    days?: number,
    format?: string,
    options: { retryWithoutCookie?: boolean; videos?: string[]; filesOnly?: boolean } = {},
  ): Promise<Run> {
    const query = new URLSearchParams()
    if (target !== undefined) {
      query.set('target', target)
    }
    if (tabs !== undefined) {
      query.set('tabs', tabNames(tabs))
    }
    if (days !== undefined) {
      query.set('days', String(days))
    }
    if (format !== undefined) {
      query.set('format', format)
    }
    if (options.retryWithoutCookie !== undefined) {
      query.set('retryWithoutCookie', String(options.retryWithoutCookie))
    }
    if (options.videos !== undefined) {
      query.set('videos', options.videos.join(','))
    }
    if (options.filesOnly !== undefined) {
      query.set('filesOnly', String(options.filesOnly))
    }
    const search = query.size > 0 ? `?${query.toString()}` : ''
    return postFor(`/api/admin/tasks/${segment(task)}/runs${search}`)
  },

  /** Asks the run going on to stop (1.4): it ends canceled soon after. 409 when it's over or runs elsewhere. */
  cancelRun(id: string): Promise<Run> {
    return postFor(`/api/admin/runs/${segment(id)}/cancel`)
  },

  /** Puts the refresh going on on hold by hand (1.5): it waits for Resume now. 409 when it isn't a refresh going on. */
  holdRun(id: string): Promise<Run> {
    return postFor(`/api/admin/runs/${segment(id)}/hold`)
  },

  /**
   * Resumes a refresh on hold now (1.5): the same run goes on where it stopped; while another run goes on (2.3.0), back
   * in the queue, first. 409 when it isn't on hold.
   */
  resumeRun(id: string): Promise<Run> {
    return postFor(`/api/admin/runs/${segment(id)}/resume`)
  },

  /**
   * Runs a queued run now (2.3.0): first in the queue; the refresh going on is held to let it go, and goes on right
   * after it. 409 when it isn't queued.
   */
  runNow(id: string): Promise<Run> {
    return postFor(`/api/admin/runs/${segment(id)}/run-now`)
  },

  /** A run's log lines after the line `after` (0: from the first), and whether more will come (1.4). */
  runLog(id: string, after: number, signal?: AbortSignal): Promise<RunLog> {
    return get(`/api/admin/runs/${segment(id)}/log`, { after: after > 0 ? after : undefined }, signal)
  },

  /** The API keys (1.5), the latest created first; never their secrets. */
  apiKeys(signal?: AbortSignal): Promise<ApiKey[]> {
    return get('/api/admin/api-keys', {}, signal)
  },

  /** Creates a key: its secret comes this once. 409 when a key has the name already, whatever the case. */
  createApiKey(name: string, kind: ApiKeyKind): Promise<CreatedApiKey> {
    return postFor('/api/admin/api-keys', { name, kind })
  },

  enableApiKey(id: number): Promise<ApiKey> {
    return postFor(`/api/admin/api-keys/${id}/enable`)
  },

  disableApiKey(id: number): Promise<ApiKey> {
    return postFor(`/api/admin/api-keys/${id}/disable`)
  },

  deleteApiKey(id: number): Promise<void> {
    return remove(`/api/admin/api-keys/${id}`)
  },

  /** The admin's YouTube page (1.5): the cookie and the tools' versions. */
  youTube(signal?: AbortSignal): Promise<YouTubeOverview> {
    return get('/api/admin/youtube', {}, signal)
  },

  /** Tries the cookie against YouTube again (a few seconds). 404 when there's none. */
  checkYouTubeCookie(): Promise<YouTubeCookie> {
    return postFor('/api/admin/youtube/cookie/check')
  },

  removeYouTubeCookie(): Promise<void> {
    return remove('/api/admin/youtube/cookie')
  },

  /** Which yt-dlp is chosen and runs (1.5). */
  ytdlp(signal?: AbortSignal): Promise<YtdlpState> {
    return get('/api/admin/youtube/ytdlp', {}, signal)
  },

  /** The versions that can be chosen (from GitHub; 502 when it can't be reached). */
  ytdlpVersions(signal?: AbortSignal): Promise<YtdlpVersions> {
    return get('/api/admin/youtube/ytdlp/versions', {}, signal)
  },

  /** A video's comments as TonTuyau read them (1.5): a page of threads; `readAt` null when never read. */
  videoComments(
    id: string,
    query: { sort?: 'top' | 'newest'; cursor?: string },
    signal?: AbortSignal,
  ): Promise<Comments> {
    return get(`/api/videos/${segment(id)}/comments`, query, signal)
  },

  /** A channel's admin page (1.5): the channel, its state on YouTube, how many of its videos are in each state. */
  adminChannel(id: string, signal?: AbortSignal): Promise<AdminChannel> {
    return get(`/api/admin/channels/${segment(id)}`, {}, signal)
  },

  /** Subscribes to the channel (2.0), or changes which of its tabs are downloaded. 400 when none is ticked. */
  subscribe(id: string, tabs: ChannelTabs): Promise<Subscription> {
    return putFor(`/api/admin/channels/${segment(id)}/subscription`, tabs)
  },

  /** Unsubscribes (2.0): the channel and its videos stay, nothing more is downloaded. */
  unsubscribe(id: string): Promise<void> {
    return remove(`/api/admin/channels/${segment(id)}/subscription`)
  },

  /** Unblocks the syncs of its videos named, or all of them (2.3.0): back in the lists without their files. */
  unblockVideos(id: string, videos?: string[]): Promise<void> {
    const search = videos === undefined ? '' : `?${new URLSearchParams({ videos: videos.join(',') }).toString()}`
    return remove(`/api/admin/channels/${segment(id)}/blocked-videos${search}`)
  },

  /** Its videos the YouTube syncs couldn't download (2.0): why, how many times; the latest failed first. */
  failedDownloads(id: string, signal?: AbortSignal): Promise<DownloadFailure[]> {
    return get(`/api/admin/channels/${segment(id)}/failed-downloads`, {}, signal)
  },

  /** Its videos with their state on YouTube, latest first; by state or words, page by page. */
  adminChannelVideos(
    id: string,
    query: { state?: YouTubeState; q?: string; cursor?: string; blocked?: boolean },
    signal?: AbortSignal,
  ): Promise<Page<AdminVideo>> {
    return get(`/api/admin/channels/${segment(id)}/videos`, query, signal)
  },

  /** How YouTube is read (1.5): which comments the refreshes read (none, the top N, all). */
  youTubeSettings(signal?: AbortSignal): Promise<YouTubeSettings> {
    return get('/api/admin/youtube/settings', {}, signal)
  },

  saveYouTubeSettings(
    comments: YouTubeCommentsChoice,
    commentsMax: number,
    pauseSeconds: number,
    holdMinutes: number,
    syncEveryMinutes: number,
  ): Promise<YouTubeSettings> {
    return putFor('/api/admin/youtube/settings', { comments, commentsMax, pauseSeconds, holdMinutes, syncEveryMinutes })
  },

  /** The download settings (2.0): the format, the tabs a channel added gets; the reading settings stay as they are. */
  saveDownloadSettings(
    comments: YouTubeCommentsChoice,
    downloadFormat: string,
    defaultTabs: ChannelTabs,
    // 2.2.0: the disk space settings.
    space: { minFreeGb: number; holdBelowMinFree: boolean },
    // 2.3.0: a format matching none with the YouTube cookie, tried again without it.
    retryWithoutCookie: boolean,
  ): Promise<YouTubeSettings> {
    return putFor('/api/admin/youtube/settings', {
      comments,
      downloadFormat,
      defaultTabs,
      ...space,
      retryWithoutCookie,
    })
  },

  // 2.3.0: the accounts (an admin's).

  accounts(signal?: AbortSignal): Promise<Account[]> {
    return get('/api/admin/accounts', {}, signal)
  },

  /** A local account: 409 when the name is taken (whatever the case). */
  createAccount(account: NewAccount): Promise<Account> {
    return postFor('/api/admin/accounts', account)
  },

  /** 409 for the last enabled local admin, one's own account disabled, a directory account's role or password. */
  changeAccount(id: number, change: AccountChange): Promise<Account> {
    return putFor(`/api/admin/accounts/${id}`, change)
  },

  deleteAccount(id: number): Promise<void> {
    return remove(`/api/admin/accounts/${id}`)
  },

  /** Who can watch (2.3.0): everyone, or the logged-in accounts only. */
  accessSettings(signal?: AbortSignal): Promise<AccessSettings> {
    return get('/api/admin/settings/access', {}, signal)
  },

  saveAccessSettings(accountsOnly: boolean): Promise<AccessSettings> {
    return putFor('/api/admin/settings/access', { accountsOnly })
  },

  /** The LDAP directory (2.3.0): its settings, never the service account's password. */
  ldapSettings(signal?: AbortSignal): Promise<LdapSettings> {
    return get('/api/admin/settings/ldap', {}, signal)
  },

  saveLdapSettings(settings: LdapSettingsChange): Promise<LdapSettings> {
    return putFor('/api/admin/settings/ldap', settings)
  },

  /** One test of the settings as typed (not saved): the connection, a filter, or a name. */
  testLdap(request: LdapTestRequest): Promise<LdapTest> {
    return postFor('/api/admin/settings/ldap/test', request)
  },

  /** Chooses which yt-dlp runs: applied in the background, from the next task. */
  chooseYtdlp(kind: YtdlpChoiceKind, version: string | null): Promise<YtdlpState> {
    return putFor('/api/admin/youtube/ytdlp', { kind, version })
  },
}

export type Api = typeof api
