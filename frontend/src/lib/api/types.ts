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

/** `GET /api/auth/me`: who is logged in (a 401 when nobody is); 2.3.0: the role, where from, the directory's name. */
export type Me = Schemas['MeResponse']

/** An account's role (2.3.0): an admin (everything), or a user (watches the library). */
export type Role = Schemas['AccountRole']

/** Where an account is from (2.3.0): made here, or the LDAP directory's. */
export type AccountSource = Schemas['AccountSource']

/** `GET /api/admin/accounts` (2.3.0): each account, never its password. */
export type Account = Schemas['AccountResponse']

/** `POST /api/admin/accounts` (2.3.0): a local account. */
export type NewAccount = Schemas['NewAccountRequest']

/** `PUT /api/admin/accounts/{id}` (2.3.0): what changes (a directory account: `disabled` only). */
export type AccountChange = Schemas['AccountChangeRequest']

/** `GET /api/admin/dashboard` (1.2): the library's totals and play time, and every channel. */
export type Dashboard = Schemas['DashboardResponse']

/**
 * `GET /api/admin/dashboard/imports` (1.2): the new videos of a period, a bar per run, day, week or month of the
 * dashboard's time zone (`timeZone`, TONTUYAU_TZ), oldest first, empty ones too; `notRecorded` is the part of the runs of 1.1.
 */
export type ImportHistory = Schemas['ImportHistoryResponse']

export type ImportBar = Schemas['ImportBar']

export type ImportPeriod = NonNullable<QueryOf<'/api/admin/dashboard/imports'>['period']>

export type TypePlayTime = Schemas['TypePlayTime']

/**
 * The dashboard's storage (2.2.0): the videos folder's disk (null when it doesn't say), the videos' files by type, and
 * the YouTube page's threshold, in bytes.
 */
export type Storage = Schemas['Storage']

export type ChannelTotals = Schemas['ChannelTotals']

/**
 * `GET /api/admin/tasks` (1.4): the TubeArchivist import (2.0: how it last ran, or why it can't start), the refreshes
 * on hold, and the run going on (`lock`, in any process; `runningHere`, the one this backend can cancel).
 */
export type Tasks = Schemas['TasksResponse']

export type TaskInfo = Schemas['TaskResponse']

/** A run of the history (1.4). */
export type Run = Schemas['RunResponse']

export type RunTask = Run['task']

export type RunState = Run['state']

/** `GET /api/admin/runs/{id}/log` (1.4): lines after a given one, and whether more will come. */
export type RunLog = Schemas['RunLogResponse']

export type RunLogLine = Schemas['RunLogLineResponse']

/** `/api/admin/runs`: task, size, before (the previous page's last run). */
export type RunListParams = QueryOf<'/api/admin/runs'>

/** An API key (1.5), without its secret. */
export type ApiKey = Schemas['ApiKeyResponse']

export type ApiKeyKind = ApiKey['kind']

/** A key just created: its secret, given this once. */
export type CreatedApiKey = Schemas['CreatedApiKeyResponse']

/** The admin's YouTube page (1.5): the cookie (null: none) and the tools' versions. */
export type YouTubeOverview = Schemas['YouTubeResponse']

export type YouTubeCookie = Schemas['YouTubeCookieResponse']

export type YouTubeCookieState = YouTubeCookie['state']

/** Which yt-dlp is chosen and runs (1.5). */
export type YtdlpState = Schemas['YtdlpStateResponse']
export type YouTubeSettings = Schemas['YouTubeSettingsResponse']
export type AdminChannel = Schemas['AdminChannelResponse']
export type Subscription = Schemas['SubscriptionResponse']
export type ChannelTabs = Schemas['ChannelTabsResponse']
export type DownloadFailure = Schemas['DownloadFailureResponse']
export type Chapter = Schemas['Chapter']
export type Comments = Schemas['CommentsResponse']
export type CommentThread = Schemas['CommentThreadResponse']
export type Comment = Schemas['CommentResponse']
export type AdminVideo = Schemas['AdminVideoResponse']
export type YouTubeState = AdminVideo['youTube']['state']
export type YouTubeCommentsChoice = YouTubeSettings['comments']

export type YtdlpChoiceKind = YtdlpState['choice']['kind']

/** The yt-dlp versions that can be chosen, the latest first. */
export type YtdlpVersions = Schemas['YtdlpVersionsResponse']

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

/** `/api/admin/dashboard/channels` (alpha.3): q, sort, order, gone, cursor, size. */
export type DashboardChannelParams = Paged<QueryOf<'/api/admin/dashboard/channels'>>
export type DashboardChannelSort = NonNullable<DashboardChannelParams['sort']>

export type SearchFilters = Pick<QueryOf<'/api/search'>, 'type' | 'channel' | 'published' | 'duration' | 'sort'>

/** The search's parameters but `kind`, which each client method sets. Front convenience: `q` is required. */
export type SearchParams = Paged<Omit<QueryOf<'/api/search'>, 'kind'>> & { q: string }
