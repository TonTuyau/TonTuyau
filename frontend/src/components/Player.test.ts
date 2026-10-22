import { fireEvent, render, screen, within } from '@testing-library/svelte'
import type { Playability } from '../lib/codec'
import { i18n } from '../lib/i18n/i18n.svelte'
import { videoDetail } from '../test/data'
import Player from './Player.svelte'

describe('Player (codec check, R2-13)', () => {
  afterEach(() => i18n.setLang('en'))

  it('shows the player with the API media URL when the browser can play the type', () => {
    const probe = vi.fn((): Playability => 'probably')
    const { container } = render(Player, { video: videoDetail(), probe })

    expect(probe).toHaveBeenCalledWith('video/mp4; codecs="av01.0.08M.08, opus"')
    expect(container.querySelector('video')).toHaveAttribute('src', '/media/from-the-api/vid00000001')
  })

  it('shows "can\'t play <codec>" and the download link when the browser can\'t', () => {
    const { container } = render(Player, { video: videoDetail(), probe: (): Playability => 'no' })

    expect(container.querySelector('video')).toBeNull()
    expect(screen.getByText("This browser can't play AV1 + Opus")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Download/ })).toHaveAttribute(
      'href',
      '/media/from-the-api/vid00000001/download',
    )
  })

  it('uses the real browser check through canPlayType (mocked)', () => {
    vi.spyOn(HTMLMediaElement.prototype, 'canPlayType').mockReturnValue('')
    const { container } = render(Player, {
      video: videoDetail({ mimeType: 'video/mp4; codecs="hvc1.1.6.L120.90, mp4a.40.2"', tracks: [] }),
    })

    expect(container.querySelector('video')).toBeNull()
    expect(screen.getByText(/can't play video\/mp4; codecs="hvc1/)).toBeInTheDocument()
  })

  it('shows "file unavailable" without player nor download when mediaAvailable is false', () => {
    const probe = vi.fn((): Playability => 'probably')
    const { container } = render(Player, { video: videoDetail({ mediaAvailable: false }), probe })

    expect(screen.getByText('File unavailable')).toBeInTheDocument()
    expect(container.querySelector('video')).toBeNull()
    expect(screen.queryByRole('link', { name: /Download/ })).not.toBeInTheDocument()
  })

  it('falls back to the download message when the browser fails to decode the file', async () => {
    const { container } = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })

    await fireEvent.error(container.querySelector('video') as HTMLVideoElement)

    expect(screen.getByText("The browser couldn't play this file")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Download/ })).toBeInTheDocument()
  })

  describe('canPlayType "maybe" (M5a-16)', () => {
    // What Chromium answers for an ambiguous codec string (no profile), checked in Chrome 152.
    const maybeType = 'video/mp4; codecs="avc1"'
    // The file's type only: no HLS of its own (C3: the player would take it for the parts), as jsdom.
    const stub = (answer: string) =>
      vi
        .spyOn(HTMLMediaElement.prototype, 'canPlayType')
        .mockImplementation((type) => (type.startsWith('video/') ? (answer as CanPlayTypeResult) : ''))

    it('plays, with the notice and the download under the player', () => {
      const canPlayType = stub('maybe')
      const { container } = render(Player, { video: videoDetail({ mimeType: maybeType }) })

      expect(canPlayType).toHaveBeenCalledWith(maybeType)
      expect(container.querySelector('video')).toHaveAttribute('src', '/media/from-the-api/vid00000001')
      const notice = screen.getByRole('note')
      expect(notice).toHaveTextContent(
        'This video format may be incompatible with your browser. You can also download the video.',
      )
      expect(within(notice).getByRole('link', { name: /Download/ })).toHaveAttribute(
        'href',
        '/media/from-the-api/vid00000001/download',
      )
      // Under the player, not inside it.
      expect(container.querySelector('.player')?.contains(notice)).toBe(false)
    })

    it('says it in French', () => {
      stub('maybe')
      i18n.setLang('fr')
      render(Player, { video: videoDetail({ mimeType: maybeType }) })

      expect(screen.getByRole('note')).toHaveTextContent(
        'Le format de cette vidéo est peut-être incompatible avec votre navigateur. Vous pouvez aussi télécharger la vidéo.',
      )
    })

    it('shows no notice for "probably"', () => {
      stub('probably')
      const { container } = render(Player, { video: videoDetail() })

      expect(container.querySelector('video')).not.toBeNull()
      expect(screen.queryByRole('note')).not.toBeInTheDocument()
    })

    it('keeps the "can\'t play" message for "" (no MSE either)', () => {
      stub('')
      vi.stubGlobal('MediaSource', undefined)
      const { container } = render(Player, { video: videoDetail({ mimeType: maybeType, tracks: [] }) })
      vi.unstubAllGlobals()

      expect(container.querySelector('video')).toBeNull()
      expect(screen.queryByRole('note')).not.toBeInTheDocument()
      expect(screen.getByText(/can't play video\/mp4; codecs="avc1"/)).toBeInTheDocument()
    })

    it('replaces the notice with the "couldn\'t play" message on a decode error', async () => {
      stub('maybe')
      const { container } = render(Player, { video: videoDetail({ mimeType: maybeType }) })

      await fireEvent.error(container.querySelector('video') as HTMLVideoElement)

      expect(screen.queryByRole('note')).not.toBeInTheDocument()
      expect(screen.getByText("The browser couldn't play this file")).toBeInTheDocument()
    })

    it('puts the notice over the top of the video in the shorts player', () => {
      stub('maybe')
      const { container } = render(Player, {
        video: videoDetail({ mimeType: maybeType, type: 'short' }),
        variant: 'shorts',
      })

      expect(container.querySelector('.player')?.contains(screen.getByRole('note'))).toBe(true)
    })
  })

  describe("the video's shape (M5: no black bars)", () => {
    const box = (container: HTMLElement) => container.querySelector('.player') as HTMLElement
    const probably = (): Playability => 'probably'
    const sized = (width: number, height: number) =>
      videoDetail({
        tracks: [
          { kind: 'video', codec: 'h264', width, height, bitrate: 1 },
          { kind: 'audio', codec: 'aac', width: null, height: null, bitrate: 1 },
        ],
      })

    it.each([
      [1080, 1920, '1080 / 1920'],
      [640, 480, '640 / 480'],
      [1920, 816, '1920 / 816'],
      [3840, 2160, '3840 / 2160'],
    ])('takes the ratio of the API video track %ix%i', (width, height, ratio) => {
      const { container } = render(Player, { video: sized(width, height), probe: probably })

      expect(box(container).style.getPropertyValue('--video-ratio')).toBe(ratio)
      expect(box(container)).toHaveClass('fit')
    })

    it("starts 16:9 without a video track, then takes the loaded file's shape", async () => {
      const audioOnly = videoDetail({
        tracks: [{ kind: 'audio', codec: 'opus', width: null, height: null, bitrate: 1 }],
      })
      const { container } = render(Player, { video: audioOnly, probe: probably })
      expect(box(container).style.getPropertyValue('--video-ratio')).toBe('16 / 9')

      const element = container.querySelector('video') as HTMLVideoElement
      Object.defineProperty(element, 'videoWidth', { value: 480 })
      Object.defineProperty(element, 'videoHeight', { value: 360 })
      await fireEvent(element, new Event('loadedmetadata'))

      expect(box(container).style.getPropertyValue('--video-ratio')).toBe('480 / 360')
    })

    it('keeps the API ratio when the file reports another one (the API is the source)', async () => {
      const { container } = render(Player, { video: sized(1080, 1920), probe: probably })
      const element = container.querySelector('video') as HTMLVideoElement
      Object.defineProperty(element, 'videoWidth', { value: 640 })
      Object.defineProperty(element, 'videoHeight', { value: 360 })
      await fireEvent(element, new Event('loadedmetadata'))

      expect(box(container).style.getPropertyValue('--video-ratio')).toBe('1080 / 1920')
    })

    it('uses the plain landscape box for a message (nothing to fit)', () => {
      const { container } = render(Player, { video: sized(1080, 1920), probe: (): Playability => 'no' })

      expect(box(container)).toHaveClass('landscape')
      expect(box(container).style.getPropertyValue('--video-ratio')).toBe('')
    })
  })

  describe('small / large player button', () => {
    it('shows on the watch player when the page handles it, and asks for the other size', async () => {
      const onsizechange = vi.fn()
      const { rerender } = render(Player, {
        video: videoDetail(),
        probe: (): Playability => 'probably',
        size: 'small',
        onsizechange,
      })

      const button = screen.getByRole('button', { name: 'Large player' })
      expect(button).toHaveAttribute('aria-pressed', 'false')
      await fireEvent.click(button)
      expect(onsizechange).toHaveBeenCalledWith('large')

      await rerender({ size: 'large' })
      await fireEvent.click(screen.getByRole('button', { name: 'Small player' }))
      expect(onsizechange).toHaveBeenLastCalledWith('small')
    })

    it('is not there without a handler, nor in the shorts player', () => {
      render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })
      render(Player, {
        video: videoDetail({ type: 'short' }),
        probe: (): Playability => 'probably',
        variant: 'shorts',
        onsizechange: vi.fn(),
      })

      expect(screen.queryByRole('button', { name: /player/ })).not.toBeInTheDocument()
    })

    it('is labelled in French', () => {
      i18n.setLang('fr')
      render(Player, { video: videoDetail(), probe: (): Playability => 'probably', onsizechange: vi.fn() })

      expect(screen.getByRole('button', { name: 'Grand lecteur' })).toBeInTheDocument()
    })
  })

  describe('a file missing on the share (mediaAvailable true, 404 when played)', () => {
    it('asks the server about the media URL on an error and shows "file unavailable" without download', async () => {
      const head = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 404 }))
      const onunavailable = vi.fn()
      const { container } = render(Player, {
        video: videoDetail(),
        probe: (): Playability => 'probably',
        onunavailable,
      })

      await fireEvent.error(container.querySelector('video') as HTMLVideoElement)

      expect(await screen.findByText('File unavailable')).toBeInTheDocument()
      expect(screen.getByText("The video file can't be found on the share right now.")).toBeInTheDocument()
      expect(screen.queryByRole('link', { name: /Download/ })).not.toBeInTheDocument()
      expect(head).toHaveBeenCalledWith('/media/from-the-api/vid00000001', {
        method: 'HEAD',
        signal: expect.any(AbortSignal),
      })
      expect(onunavailable).toHaveBeenCalledOnce()
    })

    it('keeps "couldn\'t play" (with the download) when the file is there, or the server can\'t be asked', async () => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(new Response(null, { status: 206 }))
      const first = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })
      await fireEvent.error(first.container.querySelector('video') as HTMLVideoElement)
      expect(await screen.findByText("The browser couldn't play this file")).toBeInTheDocument()
      first.unmount()

      vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new TypeError('Failed to fetch'))
      const second = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })
      await fireEvent.error(second.container.querySelector('video') as HTMLVideoElement)
      expect(await screen.findByText("The browser couldn't play this file")).toBeInTheDocument()
      expect(screen.getByRole('link', { name: /Download/ })).toBeInTheDocument()
    })
  })

  // 1.1.0, the Firefox shorts fix: a <video> taken out of the page kept downloading (and its connection) until
  // garbage-collected.
  describe('letting go of its video', () => {
    it('empties the <video> when the player goes away, so its download stops at once', () => {
      const load = vi.spyOn(HTMLMediaElement.prototype, 'load')
      const pause = vi.spyOn(HTMLMediaElement.prototype, 'pause')
      const { container, unmount } = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })
      const media = container.querySelector('video') as HTMLVideoElement
      expect(media).toHaveAttribute('src', '/media/from-the-api/vid00000001')

      unmount()

      expect(media).not.toHaveAttribute('src')
      expect(pause.mock.contexts).toEqual([media])
      expect(load.mock.contexts).toEqual([media])
    })

    it('empties it too when a message takes its place (a decode error)', async () => {
      vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 200 }))
      const load = vi.spyOn(HTMLMediaElement.prototype, 'load')
      const { container } = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })
      const media = container.querySelector('video') as HTMLVideoElement

      await fireEvent.error(media)

      expect(await screen.findByText("The browser couldn't play this file")).toBeInTheDocument()
      expect(media).not.toHaveAttribute('src')
      expect(load.mock.contexts).toEqual([media])
    })

    it("says it couldn't play the file when the server never answers about it", async () => {
      const timeout = new AbortController()
      const timeoutSpy = vi.spyOn(AbortSignal, 'timeout').mockReturnValue(timeout.signal)
      vi.spyOn(globalThis, 'fetch').mockImplementation(
        (_input, init) =>
          new Promise((_resolve, reject) => init?.signal?.addEventListener('abort', () => reject(init.signal?.reason))),
      )
      const { container } = render(Player, { video: videoDetail(), probe: (): Playability => 'probably' })

      await fireEvent.error(container.querySelector('video') as HTMLVideoElement)
      timeout.abort(new DOMException('The operation timed out.', 'TimeoutError'))

      expect(await screen.findByText("The browser couldn't play this file")).toBeInTheDocument()
      expect(timeoutSpy).toHaveBeenCalledWith(10_000)
    })
  })
})
