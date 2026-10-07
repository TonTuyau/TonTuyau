// The search page's URL parameters ↔ the API's /api/search parameters.
// Only translation of the user's choices: the API does the matching, filtering and sorting.
import type { DurationFilter, PublishedFilter, SearchFilters, SearchSort, VideoType } from './api/types'

export type TypeFilter = VideoType | 'channel'
/** "Upload date" (`uploaded=` in the page URL): the API's named period, sent as is (v1.2, M5a-1). */
export type UploadFilter = PublishedFilter

export interface ResultsQuery {
  q: string
  type: TypeFilter | null
  duration: DurationFilter | null
  uploaded: UploadFilter | null
  channel: string | null
  sort: SearchSort
}

export const typeOptions: readonly TypeFilter[] = ['video', 'short', 'stream', 'channel']
export const durationOptions: readonly DurationFilter[] = ['short', 'medium', 'long']
export const uploadOptions: readonly UploadFilter[] = ['today', 'week', 'month', 'year']
export const sortOptions: readonly SearchSort[] = ['relevance', 'latest', 'popular']

function oneOf<T extends string>(value: string | null, allowed: readonly T[]): T | null {
  return allowed.find((option) => option === value) ?? null
}

export function readResultsQuery(params: URLSearchParams): ResultsQuery {
  return {
    q: (params.get('q') ?? '').trim(),
    type: oneOf(params.get('type'), typeOptions),
    duration: oneOf(params.get('duration'), durationOptions),
    uploaded: oneOf(params.get('uploaded'), uploadOptions),
    channel: params.get('channel') || null,
    sort: oneOf(params.get('sort'), sortOptions) ?? 'relevance',
  }
}

/**
 * The video filters to send. `type: channel` isn't a video type: it switches the search to kind=channels.
 * The upload date goes as the period's name: the backend computes the cut-off, the front computes no date.
 */
export function apiFilters(query: ResultsQuery): SearchFilters {
  return {
    type: query.type && query.type !== 'channel' ? query.type : undefined,
    duration: query.duration ?? undefined,
    published: query.uploaded ?? undefined,
    channel: query.channel ?? undefined,
    sort: query.sort === 'relevance' ? undefined : query.sort,
  }
}
