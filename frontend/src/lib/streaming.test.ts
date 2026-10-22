import type HlsType from 'hls.js'
import { BUFFER_SECONDS, playlistUrl, startPlayback as startOne, type Playback } from './streaming'

/** hls.js as far as the playback uses it: what it was given, and a fatal error on demand. */
class FakeHls {
  static supported = true
  static instances: FakeHls[] = []
  static readonly Events = { ERROR: 'hlsError' }
  static isSupported = () => FakeHls.supported
  readonly config: Record<string, unknown>
  source: string | null = null
  media: HTMLMediaElement | null = null
  destroyed = false
  private readonly handlers: ((event: string, data: { fatal: boolean; details: string }) => void)[] = []

  constructor(config: Record<string, unknown>) {
    this.config = config
    FakeHls.instances.push(this)
  }

  on(_event: string, handler: (event: string, data: { fatal: boolean; details: string }) => void) {
    this.handlers.push(handler)
  }

  loadSource(url: string) {
    this.source = url
  }

  attachMedia(media: HTMLMediaElement) {
    this.media = media
  }

  destroy() {
    this.destroyed = true
  }

  fail(fatal = true) {
    for (const handler of this.handlers) handler(FakeHls.Events.ERROR, { fatal, details: 'bufferAppendError' })
  }
}

const loadHls = () => Promise.resolve(FakeHls as unknown as typeof HlsType)
const session = () => 'viewing-0123456789abcdef'
const mediaUrl = '/media/vid00000001'

/** Every playback a test starts, stopped after it (else their pagehide listeners outlive it). */
const started: Playback[] = []
const startPlayback: typeof startOne = (...args) => {
  const playback = startOne(...args)
  started.push(playback)
  return playback
}

describe('startPlayback (2.0, C3)', () => {
  let fetchSpy: ReturnType<typeof vi.spyOn>
  let media: HTMLVideoElement

  beforeEach(() => {
    FakeHls.supported = true
    FakeHls.instances = []
    fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))
    media = document.createElement('video')
    media.play = vi.fn(() => Promise.resolve())
  })

  afterEach(() => {
    for (const playback of started.splice(0)) playback.stop()
    vi.unstubAllGlobals()
  })

  const withMediaSource = () => vi.stubGlobal('MediaSource', function MediaSource() {})

  it('plays in parts with hls.js where the browser has Media Source Extensions, 30 s ahead', async () => {
    withMediaSource()

    const playback = startPlayback(media, mediaUrl, { loadHls, session, start: 42 })
    await vi.waitFor(() => expect(FakeHls.instances).toHaveLength(1))

    const hls = FakeHls.instances[0] as FakeHls
    expect(playback.mode).toBe('parts')
    expect(hls.source).toBe('/media/vid00000001/hls/viewing-0123456789abcdef/index.m3u8')
    expect(hls.media).toBe(media)
    expect(hls.config).toMatchObject({
      maxBufferLength: BUFFER_SECONDS,
      maxMaxBufferLength: BUFFER_SECONDS,
      enableWorker: false,
      startPosition: 42,
    })
  })

  it('falls back to the whole file when the parts fail, where it was', async () => {
    withMediaSource()
    const onwhole = vi.fn()
    const playback = startPlayback(media, mediaUrl, { loadHls, session, onwhole })
    await vi.waitFor(() => expect(FakeHls.instances).toHaveLength(1))
    const hls = FakeHls.instances[0] as FakeHls

    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    hls.fail(false)
    expect(playback.mode).toBe('parts')
    hls.fail(true)
    expect(warn).toHaveBeenCalledOnce()

    expect(playback.mode).toBe('whole')
    expect(hls.destroyed).toBe(true)
    expect(media.getAttribute('src')).toBe(mediaUrl)
    expect(onwhole).toHaveBeenCalledOnce()
    expect(fetchSpy).toHaveBeenCalledWith('/media/vid00000001/hls/viewing-0123456789abcdef', {
      method: 'DELETE',
      keepalive: true,
    })
  })

  it('plays the whole file when hls.js says the browser can’t, or can’t be loaded', async () => {
    withMediaSource()
    FakeHls.supported = false
    const unsupported = startPlayback(media, mediaUrl, { loadHls, session })
    await vi.waitFor(() => expect(unsupported.mode).toBe('whole'))
    expect(media.getAttribute('src')).toBe(mediaUrl)

    const other = document.createElement('video')
    const unloaded = startPlayback(other, mediaUrl, { loadHls: () => Promise.reject(new Error('offline')), session })
    await vi.waitFor(() => expect(unloaded.mode).toBe('whole'))
    expect(other.getAttribute('src')).toBe(mediaUrl)
  })

  it('uses the browser’s own HLS where there is no Media Source Extensions, the whole file on an error', () => {
    vi.spyOn(media, 'canPlayType').mockImplementation((type) =>
      type === 'application/vnd.apple.mpegurl' ? 'maybe' : '',
    )

    const playback = startPlayback(media, mediaUrl, { loadHls, session })

    expect(playback.mode).toBe('native')
    expect(media.getAttribute('src')).toBe(playlistUrl(mediaUrl, 'viewing-0123456789abcdef'))
    media.dispatchEvent(new Event('error'))
    expect(playback.mode).toBe('whole')
    expect(media.getAttribute('src')).toBe(mediaUrl)
  })

  it('plays the whole file right away where the browser can play no parts at all', () => {
    const playback = startPlayback(media, mediaUrl, { loadHls, session })

    expect(playback.mode).toBe('whole')
    expect(media.getAttribute('src')).toBe(mediaUrl)
    playback.stop()
    expect(fetchSpy).not.toHaveBeenCalled()
  })

  it('tells the server when the page goes (the tab closed, another site)', async () => {
    withMediaSource()
    startPlayback(media, mediaUrl, { loadHls, session })
    await vi.waitFor(() => expect(FakeHls.instances).toHaveLength(1))

    window.dispatchEvent(new Event('pagehide'))
    window.dispatchEvent(new Event('pagehide'))

    expect((FakeHls.instances[0] as FakeHls).destroyed).toBe(true)
    expect(fetchSpy).toHaveBeenCalledOnce()
  })

  it('stops: hls.js destroyed and the server told, even before hls.js was loaded', async () => {
    withMediaSource()
    const playback = startPlayback(media, mediaUrl, { loadHls, session })
    await vi.waitFor(() => expect(FakeHls.instances).toHaveLength(1))

    playback.stop()

    expect((FakeHls.instances[0] as FakeHls).destroyed).toBe(true)
    expect(fetchSpy).toHaveBeenCalledOnce()

    const early = startPlayback(document.createElement('video'), mediaUrl, { loadHls, session })
    early.stop()
    await Promise.resolve()
    await Promise.resolve()
    expect(FakeHls.instances).toHaveLength(1)
  })
})
