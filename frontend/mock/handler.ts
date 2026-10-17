// The mock backend: answers docs/API_CONTRACT.md's /api endpoints from the fixtures, like the real backend
// would (counts, joins, sorting, filtering, cursors, ProblemDetails). Pure: no I/O, so tests call it directly.
import type {
  ChannelCard,
  ChannelDetail,
  Comment,
  Comments,
  Home,
  Page,
  ProblemDetails,
  SearchAll,
  Suggestion,
  VideoCard,
  VideoDetail,
  VideoType,
} from '../src/lib/api/types.ts'
import type { ChannelFixture, MockDb, VideoFixture } from './db.ts'

export interface MockResponse {
  status: number
  headers: Record<string, string>
  body: string
}

class Problem extends Error {
  readonly problem: ProblemDetails
  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title)
    this.problem = problem
  }
}

const json = (body: unknown, status = 200): MockResponse => ({
  status,
  headers: { 'content-type': 'application/json; charset=utf-8' },
  body: JSON.stringify(body),
})

export function problemResponse(problem: ProblemDetails): MockResponse {
  return {
    status: problem.status,
    headers: { 'content-type': 'application/problem+json' },
    body: JSON.stringify(problem),
  }
}

function invalid(field: string, message: string): Problem {
  return new Problem({
    type: 'https://tontuyau/errors/validation',
    title: 'Invalid request',
    status: 400,
    detail: `${field}: ${message}`,
    errors: { [field]: [message] },
  })
}

export function notFound(detail: string): ProblemDetails {
  return { type: 'https://tontuyau/errors/not-found', title: 'Not found', status: 404, detail }
}

// --- Parameters ------------------------------------------------------------------------------------------
function oneOf<T extends string>(params: URLSearchParams, name: string, allowed: readonly T[], fallback: T): T {
  const value = params.get(name)
  if (value === null) return fallback
  if (!(allowed as readonly string[]).includes(value)) {
    throw invalid(name, `must be one of ${allowed.join(', ')}.`)
  }
  return value as T
}

function optionalOneOf<T extends string>(params: URLSearchParams, name: string, allowed: readonly T[]): T | null {
  return params.has(name) ? oneOf(params, name, allowed, allowed[0] as T) : null
}

function integer(params: URLSearchParams, name: string, min: number, max: number, fallback: number): number {
  const value = params.get(name)
  if (value === null) return fallback
  const number = Number(value)
  if (!Number.isInteger(number) || number < min || number > max) {
    throw invalid(name, `must be an integer between ${min} and ${max}.`)
  }
  return number
}

function text(params: URLSearchParams, name: string, min: number, max: number): string {
  // Trimmed first, like the backend: blank text is "not given".
  const value = (params.get(name) ?? '').trim()
  if (value.length < min || value.length > max) {
    throw invalid(name, min > 0 ? `is required, ${min}–${max} characters.` : `must be at most ${max} characters.`)
  }
  return value
}

// Id formats, as the backend checks them (M4-Q7): a malformed id is an invalid parameter (400), not a 404.
const videoIdFormat = /^[A-Za-z0-9_-]{11}$/
const channelIdFormat = /^UC[A-Za-z0-9_-]{22}$/

function videoId(name: string, value: string | null): string | null {
  if (value !== null && !videoIdFormat.test(value)) throw invalid(name, "isn't a video id.")
  return value
}

/** Three synthetic channels of the M5a fixtures predate the check and keep their (review) ids: known ids pass. */
function channelId(lib: Library, name: string, value: string | null): string | null {
  if (value !== null && !channelIdFormat.test(value) && !lib.channelsById.has(value)) {
    throw invalid(name, "isn't a channel id.")
  }
  return value
}

const videoTypes = ['video', 'short', 'stream'] as const
const videoSorts = ['latest', 'popular', 'oldest'] as const

// --- Cursors (opaque to the front; tied to the filters + sort they were made with) -----------------------
/** What a cursor carries: the offset, and the `published` cut-off computed on the first page (v1.2). */
interface CursorState {
  o: number
  c?: number
}

function encodeCursor(state: CursorState, key: string): string {
  return Buffer.from(JSON.stringify({ ...state, k: key })).toString('base64url')
}

function badCursor(): Problem {
  return invalid('cursor', 'is not valid for these filters and sort.')
}

/** The cursor's state, or null on a first page (no cursor). */
function decodeCursor(params: URLSearchParams, key: string): CursorState | null {
  const cursor = params.get('cursor')
  if (cursor === null) return null
  try {
    const parsed = JSON.parse(Buffer.from(cursor, 'base64url').toString()) as { o: unknown; c?: unknown; k: unknown }
    if (typeof parsed.o === 'number' && parsed.k === key && (parsed.c === undefined || typeof parsed.c === 'number')) {
      return parsed.c === undefined ? { o: parsed.o } : { o: parsed.o, c: parsed.c }
    }
  } catch {
    // Falls through to the 400.
  }
  throw badCursor()
}

function paginate<T>(
  items: T[],
  params: URLSearchParams,
  key: string,
  defaultSize = 24,
  carry: Omit<CursorState, 'o'> = {},
): Page<T> {
  const size = integer(params, 'size', 1, 100, defaultSize)
  const offset = decodeCursor(params, key)?.o ?? 0
  const end = offset + size
  return {
    items: items.slice(offset, end),
    nextCursor: end < items.length ? encodeCursor({ o: end, ...carry }, key) : null,
    total: items.length,
  }
}

/** The key a cursor is valid for: every parameter but the cursor and the size. */
function cursorKey(path: string, params: URLSearchParams): string {
  const copy = new URLSearchParams(params)
  copy.delete('cursor')
  copy.delete('size')
  copy.sort()
  return `${path}?${copy.toString()}`
}

// --- Text matching (accent-insensitive, like the backend's folding) ---------------------------------------
export function fold(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
}

const words = (value: string) =>
  fold(value)
    .split(/[^\p{L}\p{N}]+/u)
    .filter(Boolean)

function startsAWord(haystack: string, prefix: string): boolean {
  const folded = fold(prefix).trim()
  return folded !== '' && (fold(haystack).startsWith(folded) || words(haystack).some((word) => word.startsWith(folded)))
}

// --- Shapes ----------------------------------------------------------------------------------------------
class Library {
  readonly channelsById: Map<string, ChannelFixture>
  readonly db: MockDb

  constructor(db: MockDb) {
    this.db = db
    this.channelsById = new Map(db.channels.map((channel) => [channel.id, channel]))
  }

  channelOf(video: VideoFixture): ChannelFixture {
    const channel = this.channelsById.get(video.channelId)
    if (!channel) throw new Error(`Fixture video ${video.id} has unknown channel ${video.channelId}`)
    return channel
  }

  card(video: VideoFixture): VideoCard {
    const channel = this.channelOf(video)
    return {
      id: video.id,
      title: video.title,
      type: video.type,
      published: video.published,
      durationSeconds: video.durationSeconds,
      views: video.views,
      active: video.active,
      youTubeState: video.youTubeState ?? stateOf(video),
      thumbnailUrl: video.thumbnailUrl,
      thumbnails: video.thumbnails,
      channel: {
        id: channel.id,
        name: channel.name,
        thumbnailUrl: channel.thumbnailUrl,
        thumbnails: channel.thumbnails,
      },
    }
  }

  detail(video: VideoFixture): VideoDetail {
    const { channelId, ...rest } = video
    void channelId // joined into `channel` by card()
    const card = this.card(video)
    return {
      ...rest,
      ...card,
      channel: { ...card.channel, subscribers: this.channelOf(video).subscribers },
      chapters: video.id === this.db.videos[0]?.id ? demoChapters(video.durationSeconds) : [],
    }
  }

  channelCard(channel: ChannelFixture): ChannelCard {
    return {
      id: channel.id,
      name: channel.name,
      subscribers: channel.subscribers,
      thumbnailUrl: channel.thumbnailUrl,
      thumbnails: channel.thumbnails,
      videoCounts: this.counts(channel.id),
    }
  }

  counts(channelId: string): ChannelCard['videoCounts'] {
    if (this.db.videoCounts) {
      return this.db.videoCounts[channelId] ?? { videos: 0, shorts: 0, streams: 0 }
    }
    const own = this.db.videos.filter((video) => video.channelId === channelId)
    const count = (type: VideoType) => own.filter((video) => video.type === type).length
    return { videos: count('video'), shorts: count('short'), streams: count('stream') }
  }

  channelDetail(channel: ChannelFixture): ChannelDetail {
    return { ...channel, ...this.channelCard(channel) }
  }
}

const byPublishedDesc = (a: VideoFixture, b: VideoFixture) =>
  b.published.localeCompare(a.published) || a.id.localeCompare(b.id)

function sortVideos(videos: VideoFixture[], sort: (typeof videoSorts)[number]): VideoFixture[] {
  const copy = [...videos]
  if (sort === 'popular') return copy.sort((a, b) => b.views - a.views || byPublishedDesc(a, b))
  if (sort === 'oldest') return copy.sort((a, b) => -byPublishedDesc(a, b))
  return copy.sort(byPublishedDesc)
}

// --- Endpoints -------------------------------------------------------------------------------------------
function home(lib: Library, params: URLSearchParams): Home {
  const size = integer(params, 'size', 1, 100, 24)
  const sections = videoTypes
    .map((type) => ({
      type,
      items: sortVideos(
        lib.db.videos.filter((video) => video.type === type),
        'latest',
      )
        .slice(0, size)
        .map((video) => lib.card(video)),
    }))
    .filter((section) => section.items.length > 0)
  return { sections }
}

function listVideos(lib: Library, params: URLSearchParams, path: string, channelId?: string): Page<VideoCard> {
  const type = channelId ? oneOf(params, 'type', videoTypes, 'video') : optionalOneOf(params, 'type', videoTypes)
  const sort = oneOf(params, 'sort', videoSorts, 'latest')
  // `exclude` (channel lists v1.1, the library list v1.3) leaves one video out of the items and of `total`.
  const exclude = videoId('exclude', params.get('exclude'))
  const matching = lib.db.videos.filter(
    (video) =>
      (type === null || video.type === type) && (!channelId || video.channelId === channelId) && video.id !== exclude,
  )
  const page = paginate(sortVideos(matching, sort), params, cursorKey(path, params))
  return { ...page, items: page.items.map((video) => lib.card(video)) }
}

function listChannels(lib: Library, params: URLSearchParams, path: string): Page<ChannelCard> {
  // At most 100 characters (contract, QA10 M5-3).
  const q = fold(text(params, 'q', 0, 100).trim())
  const sort = oneOf(params, 'sort', ['name', 'subscribers'] as const, 'name')
  const matching = lib.db.channels.filter((channel) => q === '' || fold(channel.name).includes(q))
  const sorted = [...matching].sort((a, b) =>
    sort === 'subscribers'
      ? b.subscribers - a.subscribers || a.name.localeCompare(b.name, 'fr')
      : a.name.localeCompare(b.name, 'fr', { sensitivity: 'base' }),
  )
  const page = paginate(sorted, params, cursorKey(path, params))
  return { ...page, items: page.items.map((channel) => lib.channelCard(channel)) }
}

const durationRanges = { short: [0, 239], medium: [240, 1200], long: [1201, Infinity] } as const

const HOUR_MS = 3_600_000
/** `published` periods (v1.2): within the last 24 h / 7 / 30 / 365 days. */
const publishedPeriods = {
  today: 24 * HOUR_MS,
  week: 7 * 24 * HOUR_MS,
  month: 30 * 24 * HOUR_MS,
  year: 365 * 24 * HOUR_MS,
}
const publishedNames = ['today', 'week', 'month', 'year'] as const

function score(lib: Library, video: VideoFixture, terms: string[]): number {
  const title = fold(video.title)
  const channel = fold(lib.channelOf(video).name)
  const tags = fold(video.tags.join(' '))
  const description = fold(video.description)
  let total = 0
  for (const term of terms) {
    const inTitle = title.includes(term)
    const found = inTitle || channel.includes(term) || tags.includes(term) || description.includes(term)
    if (!found) return 0
    total += (inTitle ? 4 : 0) + (channel.includes(term) ? 2 : 0) + (tags.includes(term) ? 1 : 0) + 0.5
  }
  return total
}

function searchVideos(lib: Library, params: URLSearchParams, path: string, q: string, now: number): Page<VideoCard> {
  const type = optionalOneOf(params, 'type', videoTypes)
  const channel = channelId(lib, 'channel', params.get('channel'))
  const published = optionalOneOf(params, 'published', publishedNames)
  const key = cursorKey(path, params)
  // The cut-off is computed on the first page and kept in the cursor, so later pages use the same one.
  const cursor = decodeCursor(params, key)
  if (published !== null && cursor !== null && cursor.c === undefined) throw badCursor()
  const after = published === null ? null : (cursor?.c ?? now - publishedPeriods[published])
  const duration = optionalOneOf(params, 'duration', ['short', 'medium', 'long'] as const)
  const sort = oneOf(params, 'sort', ['relevance', 'latest', 'popular'] as const, 'relevance')
  const terms = words(q)
  const scored = lib.db.videos
    .filter((video) => type === null || video.type === type)
    .filter((video) => channel === null || video.channelId === channel)
    .filter((video) => after === null || Date.parse(video.published) >= after)
    .filter((video) => {
      if (duration === null) return true
      const [min, max] = durationRanges[duration]
      return video.durationSeconds >= min && video.durationSeconds <= max
    })
    .map((video) => ({ video, score: score(lib, video, terms) }))
    .filter((entry) => entry.score > 0)
  const sorted =
    sort === 'relevance'
      ? scored.sort((a, b) => b.score - a.score || byPublishedDesc(a.video, b.video)).map((entry) => entry.video)
      : sortVideos(
          scored.map((entry) => entry.video),
          sort,
        )
  const page = paginate(sorted, params, key, 24, after === null ? {} : { c: after })
  return { ...page, items: page.items.map((video) => lib.card(video)) }
}

function matchingChannels(lib: Library, q: string): ChannelFixture[] {
  const terms = words(q)
  return lib.db.channels.filter((channel) => {
    const haystack = fold(`${channel.name} ${channel.tags.join(' ')} ${channel.description}`)
    return terms.length > 0 && terms.every((term) => haystack.includes(term))
  })
}

function search(
  lib: Library,
  params: URLSearchParams,
  path: string,
  now: number,
): SearchAll | Page<VideoCard> | Page<ChannelCard> {
  const q = text(params, 'q', 1, 200)
  const kind = oneOf(params, 'kind', ['all', 'videos', 'channels'] as const, 'all')
  if (kind === 'channels') {
    const channels = matchingChannels(lib, q).sort((a, b) => b.subscribers - a.subscribers)
    const page = paginate(channels, params, cursorKey(path, params))
    return { ...page, items: page.items.map((channel) => lib.channelCard(channel)) }
  }
  const videos = searchVideos(lib, params, path, q, now)
  if (kind === 'videos') return videos
  const channels = params.has('cursor')
    ? []
    : matchingChannels(lib, q)
        .sort((a, b) => b.subscribers - a.subscribers)
        .slice(0, 3)
        .map((channel) => lib.channelCard(channel))
  return { channels, videos }
}

const MAX_CHANNEL_SUGGESTIONS = 3

function suggest(lib: Library, params: URLSearchParams): { suggestions: Suggestion[] } {
  const q = text(params, 'q', 1, 100)
  const size = integer(params, 'size', 1, 20, 8)
  // v1.1: at most 3 channels first, then video titles fill the rest.
  const channels: Suggestion[] = lib.db.channels
    .filter((channel) => startsAWord(channel.name, q))
    .sort((a, b) => b.subscribers - a.subscribers)
    .slice(0, MAX_CHANNEL_SUGGESTIONS)
    .map((channel) => ({ text: channel.name, kind: 'channel', id: channel.id }))
  const videos: Suggestion[] = sortVideos(
    lib.db.videos.filter((video) => startsAWord(video.title, q)),
    'popular',
  ).map((video) => ({ text: video.title, kind: 'video', id: video.id }))
  return { suggestions: [...channels, ...videos].slice(0, size) }
}

// --- Router ----------------------------------------------------------------------------------------------
/** `now` is the clock for the `published` periods (tests pass a fixed one). */
export function handleApi(db: MockDb, method: string, url: URL, now: number = Date.now()): MockResponse {
  const lib = new Library(db)
  const path = url.pathname.replace(/\/+$/, '')
  const params = url.searchParams
  if (method !== 'GET' && method !== 'HEAD') {
    return problemResponse({ title: 'Method not allowed', status: 405, detail: 'The API is read-only.' })
  }
  try {
    const segments = path.split('/').slice(2).map(decodeURIComponent)
    const [resource, id, sub, ...rest] = segments
    if (rest.length > 0) throw new Problem(notFound(`No endpoint ${path}.`))
    if (resource === 'home' && id === undefined) return json(home(lib, params))
    if (resource === 'videos' && id === undefined) return json(listVideos(lib, params, path))
    if (resource === 'videos' && id !== undefined && sub === undefined) {
      videoId('id', id)
      const video = db.videos.find((candidate) => candidate.id === id)
      if (!video) throw new Problem(notFound(`No video with id ${id}.`))
      return json(lib.detail(video))
    }
    if (resource === 'videos' && id !== undefined && sub === 'comments') {
      videoId('id', id)
      const video = db.videos.find((candidate) => candidate.id === id)
      if (!video) throw new Problem(notFound(`No video with id ${id}.`))
      return json(video.id === db.videos[0]?.id ? demoComments(params.get('sort') === 'newest') : neverRead)
    }
    if (resource === 'channels' && id === undefined) return json(listChannels(lib, params, path))
    if (resource === 'channels' && id !== undefined) {
      channelId(lib, 'id', id)
      const channel = lib.channelsById.get(id)
      if (!channel) throw new Problem(notFound(`No channel with id ${id}.`))
      if (sub === undefined) return json(lib.channelDetail(channel))
      if (sub === 'videos') return json(listVideos(lib, params, path, id))
    }
    if (resource === 'search' && id === undefined) return json(search(lib, params, path, now))
    if (resource === 'search' && id === 'suggest' && sub === undefined) return json(suggest(lib, params))
    throw new Problem(notFound(`No endpoint ${path}.`))
  } catch (error) {
    if (error instanceof Problem) return problemResponse(error.problem)
    throw error
  }
}

// ---------------------------------------------------------------- 1.5: the demo video's chapters and comments

/** The first fixture video shows chapters and comments (TonTuyau read them); the others have neither. */
/** A state on YouTube for a fixture without one (1.5): gone when inactive; a few private or unlisted, by id. */
function stateOf(video: VideoFixture): VideoCard['youTubeState'] {
  if (!video.active) return 'gone'
  const sum = [...video.id].reduce((total, char) => total + char.charCodeAt(0), 0)
  return sum % 23 === 0 ? 'private' : sum % 19 === 0 ? 'unlisted' : 'onYouTube'
}

function demoChapters(duration: number): VideoDetail['chapters'] {
  const parts: [number, number, string][] = [
    [0, 0.08, 'Intro'],
    [0.08, 0.34, 'The ingredients'],
    [0.34, 0.56, 'Step by step'],
    [0.56, 0.85, 'Common mistakes'],
    [0.85, 1, 'Tasting'],
  ]
  return parts.map(([from, to, title]) => ({
    startSeconds: Math.round(duration * from),
    endSeconds: Math.round(duration * to),
    title,
  }))
}

const neverRead: Comments = { readAt: null, youTubeCount: null, kept: 0, items: [], nextCursor: null, total: 0 }

function demoComments(newest: boolean): Comments {
  const comment = (id: string, author: string, text: string, days: number, more: Partial<Comment> = {}): Comment => ({
    id,
    text,
    author,
    published: new Date(Date.UTC(2026, 9, 14) - days * 86_400_000).toISOString(),
    likes: Math.round(400 / (days + 1)),
    pinned: false,
    byUploader: false,
    hearted: false,
    ...more,
  })
  const items: Comments['items'] = [
    {
      comment: comment('d1', 'Tom Cooks', 'Thanks for watching! The trick is at 1:23, take your time there.', 30, {
        pinned: true,
        byUploader: true,
      }),
      replies: [comment('d1r', '@jeanne', 'Tried it last night, it worked first time.', 29)],
    },
    {
      comment: comment('d2', '@hydro', 'I always wondered why mine split. Now I know: too hot, too fast.', 12, {
        hearted: true,
      }),
      replies: [],
    },
    {
      comment: comment(
        'd3',
        '@clara',
        'Long story: my grandmother made this every Sunday and never wrote anything down. '.repeat(5),
        3,
      ),
      replies: [
        comment('d3r1', 'Tom Cooks', 'That is the best kind of recipe.', 2, { byUploader: true }),
        comment('d3r2', '@pipe', 'Same with mine!', 1),
      ],
    },
  ]
  const ordered = newest
    ? [...items].sort((a, b) => (b.comment.published ?? '').localeCompare(a.comment.published ?? ''))
    : items
  return { readAt: '2026-10-14T08:00:00Z', youTubeCount: 1284, kept: 500, items: ordered, nextCursor: null, total: 3 }
}
