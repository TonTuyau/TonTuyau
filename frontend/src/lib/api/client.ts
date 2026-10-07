// Typed client for docs/API_CONTRACT.md. Same code in mock mode and real mode: it always calls /api on the
// page's origin; the dev server decides whether that's the mock or the backend (vite.config.ts).
import type {
  ChannelCard,
  ChannelDetail,
  ChannelListParams,
  ChannelVideoListParams,
  Home,
  Page,
  ProblemDetails,
  SearchAll,
  SearchParams,
  Suggestions,
  VideoCard,
  VideoDetail,
  VideoListParams,
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

async function get<T>(path: string, query: Query = {}, signal?: AbortSignal): Promise<T> {
  let response: Response
  try {
    response = await fetch(buildUrl(path, query), { headers: { accept: 'application/json' }, signal })
  } catch (error) {
    if (signal?.aborted) {
      throw error
    }
    throw new ApiError({ title: 'Network error', status: 0 })
  }
  if (!response.ok) {
    throw await readError(response)
  }
  return (await response.json()) as T
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
}

export type Api = typeof api
