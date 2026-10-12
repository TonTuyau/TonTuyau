// Link previews (1.3): what Discord, Messenger and the other chat apps show for a TonTuyau link. Their fetchers
// don't run the app, they read the page's <meta property="og:…"> tags: the front server writes them into index.html
// for the page asked, from the backend's own API.
// - A video (/watch?v=…) or a short (/shorts/…): TonTuyau, the channel (as the author, from the oEmbed document
//   linked here: Open Graph has no author), the title, the thumbnail and the video itself, played in the chat where
//   the app can (Discord).
// - A channel (/channel/…): TonTuyau, its name, its description and its avatar (small, beside the text: no player).
// - Anything else, or a video or channel the backend doesn't give: TonTuyau alone.
// Every address in the tags is absolute: TONTUYAU_PUBLIC_URL, the address the chat apps' fetchers reach. Every card
// has TonTuyau's teal as its colour (Discord's bar along the card).

export interface PreviewOptions {
  /** The backend's base URL, e.g. http://tontuyau-backend:8080. */
  backend: URL
  /** The public address, e.g. https://tontuyau.example.net. */
  publicUrl: URL
  /** Where failures are reported (the backend can't be reached). */
  log: (message: string) => void
}

export const SITE_NAME = 'TonTuyau'
/** The theme's --color-brand (src/theme/youtube.css). */
export const BRAND_COLOR = '#0f8b8d'

/** A channel's description in its card, at most (the apps cut long ones anyway, each at its own length). */
export const DESCRIPTION_MAX_LENGTH = 300

/** Past this, the page goes without its preview rather than making the visitor wait. */
const BACKEND_TIMEOUT_MS = 2000

/** The parts of GET /api/videos/{id} a preview uses. */
interface Video {
  id: string
  title: string
  type: 'video' | 'short' | 'stream'
  thumbnailUrl: string | null
  thumbnails: { width: number; url: string }[]
  channel: { id: string; name: string }
  mimeType: string
  tracks: { kind: 'video' | 'audio'; width: number | null; height: number | null }[]
  mediaUrl: string
  mediaAvailable: boolean
}

/** The parts of GET /api/channels/{id} a preview uses. */
interface Channel {
  id: string
  name: string
  description: string
  thumbnailUrl: string | null
  thumbnails: { width: number; url: string }[]
}

/** The page a path names, as far as previews care (the app's own router decides the rest). */
type Target = { kind: 'video'; id: string } | { kind: 'channel'; id: string } | { kind: 'site' }

/** The path segment, decoded; null when missing or malformed. */
function decoded(segment: string | undefined): string | null {
  if (segment === undefined) {
    return null
  }
  try {
    return decodeURIComponent(segment)
  } catch {
    return null
  }
}

export function targetOf(pathname: string, search: URLSearchParams): Target {
  const parts = pathname.split('/').filter((part) => part !== '')
  const id = decoded(parts[1])
  if (parts.length === 1 && parts[0] === 'watch') {
    const video = search.get('v')
    return video ? { kind: 'video', id: video } : { kind: 'site' }
  }
  if (parts.length === 2 && parts[0] === 'shorts' && id) {
    return { kind: 'video', id }
  }
  if (parts.length >= 2 && parts.length <= 3 && parts[0] === 'channel' && id) {
    return { kind: 'channel', id }
  }
  return { kind: 'site' }
}

/** For an HTML attribute (and text): the five characters that could end it or start markup. */
export function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

/** The backend's JSON for this path, or null: not found, an error, too slow, or unreachable (logged). */
async function fetchJson<T>(options: PreviewOptions, path: string): Promise<T | null> {
  try {
    const answer = await fetch(new URL(path, options.backend), { signal: AbortSignal.timeout(BACKEND_TIMEOUT_MS) })
    if (!answer.ok) {
      return null
    }
    return (await answer.json()) as T
  } catch (error) {
    options.log(`preview: GET ${path}: ${error instanceof Error ? error.message : String(error)}`)
    return null
  }
}

/** The biggest version of an image (the lists are sorted by width), else its one URL. */
function biggest(image: { thumbnailUrl: string | null; thumbnails: { width: number; url: string }[] }) {
  const last = image.thumbnails.at(-1)
  if (last !== undefined) {
    return { url: last.url, width: last.width }
  }
  return image.thumbnailUrl === null ? null : { url: image.thumbnailUrl, width: null }
}

/** An Open Graph tag (ogp.me). */
function property(name: string, content: string | number): string {
  return `<meta property="${name}" content="${escapeHtml(String(content))}">`
}

/** A named tag: the Twitter card's, the theme colour. */
function named(name: string, content: string | number): string {
  return `<meta name="${name}" content="${escapeHtml(String(content))}">`
}

/** The tags of every card: the site's name and colour. */
function common(): string[] {
  return [property('og:site_name', SITE_NAME), named('theme-color', BRAND_COLOR)]
}

/** On one line (the cards have no line breaks), cut after a whole word with an ellipsis when too long. */
export function shortened(text: string): string {
  const line = text.replace(/\s+/g, ' ').trim()
  if (line.length <= DESCRIPTION_MAX_LENGTH) {
    return line
  }
  const cut = line.slice(0, DESCRIPTION_MAX_LENGTH)
  const space = cut.lastIndexOf(' ')
  return `${(space > 0 ? cut.slice(0, space) : cut).trimEnd()}…`
}

function absolute(options: PreviewOptions, path: string): string {
  return new URL(path, options.publicUrl).href
}

function videoTags(options: PreviewOptions, video: Video): string[] {
  const page =
    video.type === 'short' ? `/shorts/${encodeURIComponent(video.id)}` : `/watch?v=${encodeURIComponent(video.id)}`
  const tags = [
    ...common(),
    property('og:type', 'video.other'),
    property('og:url', absolute(options, page)),
    property('og:title', video.title),
  ]
  const image = biggest(video)
  if (image !== null) {
    tags.push(property('og:image', absolute(options, image.url)))
    if (image.width !== null) {
      tags.push(property('og:image:width', image.width))
    }
  }
  // The file itself, for the apps that play it in the chat: Open Graph's tags, and the Twitter card's that Discord
  // reads too (a "player" card is what makes it show a player rather than the thumbnail).
  if (video.mediaAvailable) {
    const media = absolute(options, video.mediaUrl)
    const track = video.tracks.find((t) => t.kind === 'video')
    const size = track?.width && track.height ? { width: track.width, height: track.height } : null
    tags.push(property('og:video', media))
    if (options.publicUrl.protocol === 'https:') {
      tags.push(property('og:video:secure_url', media))
    }
    tags.push(property('og:video:type', video.mimeType))
    if (size !== null) {
      tags.push(property('og:video:width', size.width), property('og:video:height', size.height))
    }
    tags.push(
      named('twitter:card', 'player'),
      named('twitter:player:stream', media),
      named('twitter:player:stream:content_type', video.mimeType),
    )
    if (size !== null) {
      tags.push(named('twitter:player:width', size.width), named('twitter:player:height', size.height))
    }
  } else {
    tags.push(named('twitter:card', image === null ? 'summary' : 'summary_large_image'))
  }
  tags.push(
    `<link rel="alternate" type="application/json+oembed" href="${escapeHtml(
      absolute(options, `/oembed?v=${encodeURIComponent(video.id)}`),
    )}">`,
  )
  return tags
}

function channelTags(options: PreviewOptions, channel: Channel): string[] {
  const tags = [
    ...common(),
    property('og:type', 'website'),
    property('og:url', absolute(options, `/channel/${encodeURIComponent(channel.id)}`)),
    property('og:title', channel.name),
  ]
  const description = shortened(channel.description)
  if (description !== '') {
    tags.push(property('og:description', description))
  }
  const image = biggest(channel)
  if (image !== null) {
    tags.push(property('og:image', absolute(options, image.url)))
  }
  tags.push(named('twitter:card', 'summary'))
  return tags
}

function siteTags(options: PreviewOptions): string[] {
  return [
    ...common(),
    property('og:type', 'website'),
    property('og:url', absolute(options, '/')),
    property('og:title', SITE_NAME),
  ]
}

/** The preview's tags for this page, to put in its <head>. */
export async function previewTags(options: PreviewOptions, pathname: string, search: URLSearchParams): Promise<string> {
  const target = targetOf(pathname, search)
  let tags: string[] | null = null
  if (target.kind === 'video') {
    const video = await fetchJson<Video>(options, `/api/videos/${encodeURIComponent(target.id)}`)
    tags = video === null ? null : videoTags(options, video)
  } else if (target.kind === 'channel') {
    const channel = await fetchJson<Channel>(options, `/api/channels/${encodeURIComponent(target.id)}`)
    tags = channel === null ? null : channelTags(options, channel)
  }
  return (tags ?? siteTags(options)).join('\n    ')
}

/** index.html with the tags at the end of its <head> (or unchanged, if it has none). */
export function withTags(html: string, tags: string): string {
  const end = html.indexOf('</head>')
  return end === -1 ? html : `${html.slice(0, end)}  ${tags}\n  ${html.slice(end)}`
}

/**
 * GET /oembed?v={id}: the video's oEmbed document (oembed.com), linked from its page. It gives what Open Graph
 * can't: the channel as the author, linked to its page. Null when there's no such video.
 */
export async function oembed(options: PreviewOptions, search: URLSearchParams): Promise<object | null> {
  const id = search.get('v')
  if (!id) {
    return null
  }
  const video = await fetchJson<Video>(options, `/api/videos/${encodeURIComponent(id)}`)
  if (video === null) {
    return null
  }
  return {
    version: '1.0',
    type: 'link',
    title: video.title,
    author_name: video.channel.name,
    author_url: absolute(options, `/channel/${encodeURIComponent(video.channel.id)}`),
    provider_name: SITE_NAME,
    provider_url: absolute(options, '/'),
  }
}
