// Typed client for docs/API_CONTRACT.md. Same code in mock mode and real mode: it always calls /api on the
// page's origin; the dev server decides whether that's the mock or the backend (vite.config.ts).
import type {
  AdminChannel,
  AdminVideo,
  ApiKey,
  ApiKeyKind,
  ChannelCard,
  ChannelDetail,
  ChannelListParams,
  ChannelVideoListParams,
  Comments,
  CreatedApiKey,
  Dashboard,
  Home,
  ImportHistory,
  ImportPeriod,
  LoginRequest,
  Me,
  Page,
  ProblemDetails,
  Run,
  RunListParams,
  RunLog,
  RunTask,
  SearchAll,
  SearchParams,
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

type QueryValue = string | number | null | undefined
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
async function remove(path: string): Promise<void> {
  await send(path, { method: 'DELETE', headers: { accept: 'application/json' } })
}

const segment = encodeURIComponent

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

  /** The admin dashboard (1.2): 401 when nobody is logged in. */
  dashboard(signal?: AbortSignal): Promise<Dashboard> {
    return get('/api/admin/dashboard', {}, signal)
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
   * Starts a task now (1.4): the run, just started. A refresh (1.5) takes its `target`, a video's or channel's id.
   * 409 when a run is going on, no deletion list waits, or the target isn't one of the library.
   */
  startTask(task: RunTask, target?: string): Promise<Run> {
    const query = target === undefined ? '' : `?target=${encodeURIComponent(target)}`
    return postFor(`/api/admin/tasks/${segment(task)}/runs${query}`)
  },

  /** Asks the run going on to stop (1.4): it ends canceled soon after. 409 when it's over or runs elsewhere. */
  cancelRun(id: string): Promise<Run> {
    return postFor(`/api/admin/runs/${segment(id)}/cancel`)
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

  /** Its videos with their state on YouTube, latest first; by state or words, page by page. */
  adminChannelVideos(
    id: string,
    query: { state?: YouTubeState; q?: string; cursor?: string },
    signal?: AbortSignal,
  ): Promise<Page<AdminVideo>> {
    return get(`/api/admin/channels/${segment(id)}/videos`, query, signal)
  },

  /** How YouTube is read (1.5): which comments the refreshes read (none, the top N, all). */
  youTubeSettings(signal?: AbortSignal): Promise<YouTubeSettings> {
    return get('/api/admin/youtube/settings', {}, signal)
  },

  saveYouTubeSettings(comments: YouTubeCommentsChoice, commentsMax: number): Promise<YouTubeSettings> {
    return putFor('/api/admin/youtube/settings', { comments, commentsMax })
  },

  /** Chooses which yt-dlp runs: applied in the background, from the next task. */
  chooseYtdlp(kind: YtdlpChoiceKind, version: string | null): Promise<YtdlpState> {
    return putFor('/api/admin/youtube/ytdlp', { kind, version })
  },
}

export type Api = typeof api
