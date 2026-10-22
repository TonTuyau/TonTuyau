// What a refresh on the Tasks page reads (1.5): a video or a channel, pasted as YouTube shows it (a link) or as its
// id. Read here into the id the API takes; anything else is refused before asking. A channel's @handle isn't read:
// only YouTube knows which channel it is.

const videoId = /^[\w-]{11}$/
const channelId = /^UC[\w-]{22}$/

/** A video's id, from its id or a link (watch?v=, youtu.be/, /shorts/, /live/, /embed/); null otherwise. */
export function videoIdOf(input: string): string | null {
  const text = input.trim()
  if (videoId.test(text)) {
    return text
  }
  const url = linkOf(text)
  if (!url) {
    return null
  }
  if (url.hostname === 'youtu.be') {
    return idOf(url.pathname.slice(1), videoId)
  }
  if (!isYouTube(url.hostname)) {
    return null
  }
  const watched = url.searchParams.get('v')
  if (url.pathname === '/watch' && watched) {
    return idOf(watched, videoId)
  }
  const [kind, id] = url.pathname.split('/').slice(1)
  return kind === 'shorts' || kind === 'live' || kind === 'embed' ? idOf(id ?? '', videoId) : null
}

/** A channel's id, from its id (UC…) or its /channel/ link; null otherwise (an @handle included). */
export function channelIdOf(input: string): string | null {
  const text = input.trim()
  if (channelId.test(text)) {
    return text
  }
  const url = linkOf(text)
  if (!url || !isYouTube(url.hostname)) {
    return null
  }
  const [kind, id] = url.pathname.split('/').slice(1)
  return kind === 'channel' ? idOf(id ?? '', channelId) : null
}

const handle = /^@[A-Za-z0-9_.-]{3,30}$/

/**
 * A channel to add (2.0, "Add a channel"): its id (UC…), its @handle, or a link to either (/channel/UC…, /@name, a
 * tab of it included); null otherwise. The backend reads a handle's id on YouTube.
 */
export function channelTargetOf(input: string): string | null {
  const text = input.trim()
  if (channelId.test(text) || handle.test(text)) {
    return text
  }
  const url = linkOf(text)
  if (!url || !isYouTube(url.hostname)) {
    return null
  }
  const [first, second] = url.pathname.split('/').slice(1)
  if (first === 'channel') {
    return idOf(second ?? '', channelId)
  }
  return idOf(decodeURIComponent(first ?? ''), handle)
}

function linkOf(text: string): URL | null {
  try {
    return new URL(/^https?:\/\//i.test(text) ? text : `https://${text}`)
  } catch {
    return null
  }
}

function isYouTube(host: string): boolean {
  return host === 'youtube.com' || host === 'www.youtube.com' || host === 'm.youtube.com'
}

function idOf(text: string, pattern: RegExp): string | null {
  return pattern.test(text) ? text : null
}
