// The app's URLs, YouTube-style where it makes sense (/watch?v=…, /shorts/…, /results?q=…).
// matchRoute() reads a path; the href helpers build every in-app link, so URLs live in one place.
import type { VideoType } from '../api/types'

export type ChannelTab = 'videos' | 'shorts' | 'streams'

/** The admin area's pages (1.4: the Tasks page, a run's page; 1.5: YouTube, the API keys, a channel's page). */
export type AdminRoute =
  | { page: 'dashboard' }
  | { page: 'tasks' }
  | { page: 'run'; id: string }
  | { page: 'youTube' }
  | { page: 'apiKeys' }
  | { page: 'channel'; id: string }

export type Route =
  | { name: 'home' }
  | { name: 'feed'; type: VideoType }
  | { name: 'channels' }
  | { name: 'channel'; id: string; tab: ChannelTab | null }
  | { name: 'watch' }
  | { name: 'shorts'; id: string }
  | { name: 'results' }
  | { name: 'login' }
  | ({ name: 'admin' } & AdminRoute)
  | { name: 'notFound' }

/** URL segment ↔ API video type. */
export const feedSegments: Record<ChannelTab, VideoType> = { videos: 'video', shorts: 'short', streams: 'stream' }
export const tabOfType: Record<VideoType, ChannelTab> = { video: 'videos', short: 'shorts', stream: 'streams' }

function isTab(value: string | undefined): value is ChannelTab {
  return value === 'videos' || value === 'shorts' || value === 'streams'
}

function decode(segment: string): string | null {
  try {
    return decodeURIComponent(segment)
  } catch {
    return null
  }
}

export function matchRoute(pathname: string): Route {
  const parts = pathname.split('/').filter((part) => part !== '')
  const [first, second, third, ...rest] = parts
  if (rest.length > 0) {
    return { name: 'notFound' }
  }
  if (first === undefined) {
    return { name: 'home' }
  }
  if (first === 'feed' && isTab(second) && third === undefined) {
    return { name: 'feed', type: feedSegments[second] }
  }
  if (first === 'channels' && second === undefined) {
    return { name: 'channels' }
  }
  if (first === 'channel' && second !== undefined) {
    const id = decode(second)
    if (id === null || (third !== undefined && !isTab(third))) {
      return { name: 'notFound' }
    }
    return { name: 'channel', id, tab: third ?? null }
  }
  if (first === 'shorts' && second !== undefined && third === undefined) {
    const id = decode(second)
    return id === null ? { name: 'notFound' } : { name: 'shorts', id }
  }
  if (first === 'watch' && second === undefined) {
    return { name: 'watch' }
  }
  if (first === 'results' && second === undefined) {
    return { name: 'results' }
  }
  if (first === 'login' && second === undefined) {
    return { name: 'login' }
  }
  if (first === 'admin' && second === undefined) {
    return { name: 'admin', page: 'dashboard' }
  }
  if (first === 'admin' && second === 'tasks' && parts.length === 2) {
    return { name: 'admin', page: 'tasks' }
  }
  if (first === 'admin' && second === 'youtube' && parts.length === 2) {
    return { name: 'admin', page: 'youTube' }
  }
  if (first === 'admin' && second === 'api-keys' && parts.length === 2) {
    return { name: 'admin', page: 'apiKeys' }
  }
  if (first === 'admin' && second === 'runs' && third !== undefined && rest.length === 0) {
    const id = decode(third)
    return id === null ? { name: 'notFound' } : { name: 'admin', page: 'run', id }
  }
  if (first === 'admin' && second === 'channels' && third !== undefined && rest.length === 0) {
    const id = decode(third)
    return id === null ? { name: 'notFound' } : { name: 'admin', page: 'channel', id }
  }
  return { name: 'notFound' }
}

function withQuery(path: string, query: Record<string, string | null | undefined>): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value) {
      params.set(key, value)
    }
  }
  const text = params.toString()
  return text ? `${path}?${text}` : path
}

/** `search` with `changes` applied (null or '' removes a key), as "?a=1&b=2" or "". */
export function mergeQuery(search: string, changes: Record<string, string | null>): string {
  const params = new URLSearchParams(search)
  for (const [key, value] of Object.entries(changes)) {
    if (value === null || value === '') {
      params.delete(key)
    } else {
      params.set(key, value)
    }
  }
  const text = params.toString()
  return text ? `?${text}` : ''
}

export const href = {
  home: (): string => '/',
  feed: (type: VideoType, sort?: string | null): string => withQuery(`/feed/${tabOfType[type]}`, { sort }),
  channels: (query: { q?: string | null; sort?: string | null } = {}): string => withQuery('/channels', query),
  channel: (id: string, tab?: ChannelTab | null, sort?: string | null): string =>
    withQuery(`/channel/${encodeURIComponent(id)}${tab ? `/${tab}` : ''}`, { sort }),
  /** `start`: where the watch page starts the video, in seconds (`&t=`, front-only routing). */
  watch: (id: string, start?: number | null): string =>
    withQuery('/watch', { v: id, t: start ? String(Math.floor(start)) : null }),
  short: (id: string): string => `/shorts/${encodeURIComponent(id)}`,
  results: (query: Record<string, string | null | undefined>): string => withQuery('/results', query),
  /** `next`: where to go back after logging in (an in-app path, see safeNext). */
  login: (next?: string | null): string => withQuery('/login', { next: next && next !== '/' ? next : null }),
  /** The admin area (1.2): its dashboard. */
  admin: (): string => '/admin',
  /** The admin's Tasks page (1.4); `task`: the history of that task only. */
  adminTasks: (task?: string | null): string => withQuery('/admin/tasks', { task }),
  /** A run's page (1.4): its facts and its log. */
  adminRun: (id: string): string => `/admin/runs/${encodeURIComponent(id)}`,
  /** The admin's YouTube page (1.5): the cookie, yt-dlp. */
  adminYouTube: (): string => '/admin/youtube',
  /** The admin's API keys (1.5). */
  adminApiKeys: (): string => '/admin/api-keys',
  /** A channel's admin page (1.5): its state on YouTube, its videos', the refreshes. */
  adminChannel: (id: string): string => `/admin/channels/${encodeURIComponent(id)}`,
}

/**
 * Where the login page goes once logged in: `next` when it's a page of this site ("/watch?v=…"), else home. Never
 * another site ("//evil.example", "https://…", "/\\evil.example": read as the browser would) nor the login page.
 */
export function safeNext(
  next: string | null,
  origin: string = globalThis.location?.origin ?? 'http://localhost',
): string {
  if (!next?.startsWith('/')) {
    return href.home()
  }
  let url: URL
  try {
    url = new URL(next, origin)
  } catch {
    // Not even a URL ("//[" …): home.
    return href.home()
  }
  if (url.origin !== origin || matchRoute(url.pathname).name === 'login') {
    return href.home()
  }
  return url.pathname + url.search + url.hash
}

/**
 * The watch page's `t` parameter (a link to a time: `?v=<id>&t=83`), in seconds; null when absent or not a time.
 * Also reads YouTube's forms, so a pasted YouTube time works: `83s`, `1m23s`, `1h2m3s`.
 */
export function startTime(value: string | null): number | null {
  if (value === null) {
    return null
  }
  const text = value.trim()
  const match = /^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s?)?$/.exec(text)
  if (!match || text === '') {
    return null
  }
  const [, hours, minutes, seconds] = match
  return Number(hours ?? 0) * 3600 + Number(minutes ?? 0) * 60 + Number(seconds ?? 0)
}
