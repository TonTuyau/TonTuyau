// The app's URLs, YouTube-style where it makes sense (/watch?v=…, /shorts/…, /results?q=…).
// matchRoute() reads a path; the href helpers build every in-app link, so URLs live in one place.
import type { VideoType } from '../api/types'

export type ChannelTab = 'videos' | 'shorts' | 'streams'

export type Route =
  | { name: 'home' }
  | { name: 'feed'; type: VideoType }
  | { name: 'channels' }
  | { name: 'channel'; id: string; tab: ChannelTab | null }
  | { name: 'watch' }
  | { name: 'shorts'; id: string }
  | { name: 'results' }
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
  watch: (id: string): string => withQuery('/watch', { v: id }),
  short: (id: string): string => `/shorts/${encodeURIComponent(id)}`,
  results: (query: Record<string, string | null | undefined>): string => withQuery('/results', query),
}
