// A video played in parts (2.0, C3, question 9): the server's HLS playlist of its keyframe-to-keyframe parts, fetched
// about 30 s ahead instead of the whole file as fast as the connection goes (lighter on the Cloudflare tunnel).
// hls.js where the browser has Media Source Extensions (Chrome, Firefox, Edge, recent Safari); the browser's own HLS
// where it hasn't (older iPhones); the whole file otherwise, and whenever the parts fail (a file without an index,
// the server's 409; a codec the browser can't play from MP4 parts): it always plays, only heavier.
import type HlsType from 'hls.js'

/** How far ahead the player fetches (the user's choice, question C3). */
export const BUFFER_SECONDS = 30

export type PlaybackMode = 'parts' | 'native' | 'whole'

export interface Playback {
  readonly mode: PlaybackMode
  /** Stops fetching; the server's viewing stops now instead of after its idle minute. */
  stop(): void
}

/** The parts' address on the server: under the video's file, one viewing per session. */
export function playlistUrl(mediaUrl: string, session: string): string {
  return `${mediaUrl}/hls/${session}/index.m3u8`
}

/** Whether the browser can play parts through Media Source Extensions (hls.js's own test, without loading it). */
export function hasMediaSource(): boolean {
  const scope = globalThis as { MediaSource?: unknown; ManagedMediaSource?: unknown }
  return typeof scope.MediaSource === 'function' || typeof scope.ManagedMediaSource === 'function'
}

export interface PlaybackOptions {
  /** Where to start, in seconds (a link to a time). */
  start?: number | null
  /** The parts failed: the whole file plays instead (the page may say so). */
  onwhole?: () => void
  /** hls.js, loaded when needed (a test gives its own). */
  loadHls?: () => Promise<typeof HlsType>
  /** A new viewing's id (a test gives its own). */
  session?: () => string
}

/**
 * Starts playing `mediaUrl` in `media`: in parts when the browser can, else the whole file. Synchronous when the
 * browser can't play parts at all (the whole file's address set at once); else hls.js is loaded first.
 */
export function startPlayback(media: HTMLVideoElement, mediaUrl: string, options: PlaybackOptions = {}): Playback {
  const session = (options.session ?? (() => crypto.randomUUID()))()
  const playlist = playlistUrl(mediaUrl, session)
  let hls: HlsType | undefined
  let stopped = false
  let mode: PlaybackMode = 'whole'

  const close = () => {
    // Not awaited: the viewing ends on the server either way (after its idle minute at worst).
    fetch(mediaUrl + `/hls/${session}`, { method: 'DELETE', keepalive: true }).catch(() => {})
  }

  const whole = () => {
    const at = media.currentTime
    const playing = !media.paused || media.autoplay
    hls?.destroy()
    hls = undefined
    if (mode !== 'whole') {
      close()
    }
    mode = 'whole'
    media.src = mediaUrl
    if (at > 0) {
      media.addEventListener('loadedmetadata', () => (media.currentTime = at), { once: true })
    }
    if (playing) {
      media.play()?.catch(() => {})
    }
    options.onwhole?.()
  }

  if (hasMediaSource()) {
    mode = 'parts'
    const load = options.loadHls ?? (() => import('hls.js/light').then((module) => module.default))
    load().then(
      (Hls) => {
        if (stopped) return
        if (!Hls.isSupported()) {
          whole()
          return
        }
        hls = new Hls({
          maxBufferLength: BUFFER_SECONDS,
          maxMaxBufferLength: BUFFER_SECONDS,
          backBufferLength: BUFFER_SECONDS,
          // No worker: the page's Content-Security-Policy keeps blob: workers out; the parts need no transmuxing.
          enableWorker: false,
          startPosition: options.start ?? -1,
        })
        hls.on(Hls.Events.ERROR, (_event, data) => {
          if (data.fatal && !stopped) {
            // Said in the console, for whoever wonders why this video plays whole.
            console.warn(`Playing in parts failed (${data.details}): the whole file plays instead.`, data.error)
            whole()
          }
        })
        hls.loadSource(playlist)
        hls.attachMedia(media)
      },
      () => {
        if (!stopped) whole()
      },
    )
  } else if (media.canPlayType('application/vnd.apple.mpegurl') !== '') {
    mode = 'native'
    media.addEventListener('error', () => mode === 'native' && !stopped && whole(), { once: true })
    media.src = playlist
  } else {
    media.src = mediaUrl
  }

  const stop = () => {
    if (stopped) return
    stopped = true
    globalThis.removeEventListener?.('pagehide', stop)
    hls?.destroy()
    hls = undefined
    if (mode !== 'whole') close()
  }
  // The tab closed or left for another site: the page goes without its components' cleanup, the server is told here.
  globalThis.addEventListener?.('pagehide', stop)

  return {
    get mode() {
      return mode
    },
    stop,
  }
}
