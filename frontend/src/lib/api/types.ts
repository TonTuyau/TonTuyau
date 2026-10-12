// The shapes of docs/API_CONTRACT.md (v1.4), as the backend's OpenAPI document describes them (QA10 M5-4).
// schema.d.ts is generated from the committed copy openapi.json (`pnpm run api:types`; `pnpm run api:openapi` copies
// the running backend's document first). A backend test fails while the copy differs from what the API serves.
// These are names for the generated types, plus the few front-side conveniences (marked). The mock API
// (frontend/mock/) imports them too, so the fixtures can't drift from the contract either.
import type { components, paths } from './schema.d.ts'

type Schemas = components['schemas']

/** The query parameters of an endpoint's GET, as the OpenAPI document lists them (with their enums). */
type QueryOf<P extends keyof paths> = paths[P] extends { get: { parameters: { query?: infer Q } } }
  ? NonNullable<Q>
  : never

export type VideoType = Schemas['VideoType']

/**
 * One version of an image (v1.4): lists are sorted by width, the original last with its real width; only versions
 * that exist are listed; no image = `[]`.
 */
export type ImageSize = Schemas['ImageSize']

/** The channel of a video card. */
export type ChannelRef = Schemas['VideoChannel']

export type VideoCard = Schemas['VideoCard']

export type Track = Schemas['Track']

/** The channel embedded in a video's detail: a ChannelRef plus its subscriber count (v1.1, M5a-2). */
export type VideoDetailChannel = Schemas['VideoDetailChannel']

export type VideoDetail = Schemas['VideoDetail']

export type VideoCounts = Schemas['VideoCounts']

export type ChannelCard = Schemas['ChannelCard']

export type ChannelDetail = Schemas['ChannelDetail']

export type Suggestion = Schemas['Suggestion']

/**
 * A cursor-paged list. Front convenience: one generic name for the document's `PageOfVideoCard` and
 * `PageOfChannelCard` (types.test.ts checks they're the same shapes).
 */
export interface Page<T> {
  items: T[]
  nextCursor: string | null
  total: number
}

export type HomeSection = Schemas['HomeSection']

export type Home = Schemas['HomeResponse']

export type SearchAll = Schemas['SearchAllResponse']

export type Suggestions = Schemas['SuggestResponse']

/** `POST /api/auth/login`'s body (1.2): `remember` is "Stay logged in" (30 days instead of 8 hours). */
export type LoginRequest = Schemas['LoginRequest']

/** `GET /api/auth/me`: who is logged in (a 401 when nobody is). */
export type Me = Schemas['MeResponse']

/**
 * `GET /api/admin/dashboard` (1.2): the library's totals and play time, the recent imports (sync runs that brought
 * videos, oldest first; `byType` null for runs of 1.1, which didn't record the types) and every channel.
 */
export type Dashboard = Schemas['DashboardResponse']

export type TypePlayTime = Schemas['TypePlayTime']

export type SyncImport = Schemas['SyncImport']

export type ChannelTotals = Schemas['ChannelTotals']

/** RFC 9457 ProblemDetails, as the backend sends it (`errors` on a 400). */
export type ProblemDetails = Schemas['HttpValidationProblemDetails']

export type VideoSort = NonNullable<QueryOf<'/api/videos'>['sort']>
export type ChannelSort = NonNullable<QueryOf<'/api/channels'>['sort']>
export type SearchSort = NonNullable<QueryOf<'/api/search'>['sort']>
export type SearchKind = NonNullable<QueryOf<'/api/search'>['kind']>
export type DurationFilter = NonNullable<QueryOf<'/api/search'>['duration']>
/** Search date filter (v1.2): published within the last 24 h / 7 / 30 / 365 days, computed by the backend. */
export type PublishedFilter = NonNullable<QueryOf<'/api/search'>['published']>

/** Front convenience: the first page passes `cursor: null` (the client leaves empty values out). */
type Paged<Q> = Omit<Q, 'cursor'> & { cursor?: string | null }

export type PageParams = Paged<{ cursor?: string; size?: number }>

/** `/api/videos`: type, sort, exclude (v1.3), cursor, size. */
export type VideoListParams = Paged<QueryOf<'/api/videos'>>

/** A channel's videos. Front convenience: `type` is always given (the backend's default is `video`). */
export type ChannelVideoListParams = Paged<QueryOf<'/api/channels/{id}/videos'>> & { type: VideoType }

export type ChannelListParams = Paged<QueryOf<'/api/channels'>>

export type SearchFilters = Pick<QueryOf<'/api/search'>, 'type' | 'channel' | 'published' | 'duration' | 'sort'>

/** The search's parameters but `kind`, which each client method sets. Front convenience: `q` is required. */
export type SearchParams = Paged<Omit<QueryOf<'/api/search'>, 'kind'>> & { q: string }
