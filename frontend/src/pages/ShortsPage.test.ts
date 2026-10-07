import { fireEvent, render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import App from '../App.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { router } from '../lib/router/router.svelte'
import { shortsPlayer } from '../lib/shortsPlayer.svelte'
import { page, videoCard, videoDetail } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'

// The sequence: the short opened first (s1), then the library's shorts as the API lists them (s2, s3, then s4
// on page 2).
function shortsBackend() {
  const detail = (id: string) => videoDetail({ id, type: 'short', title: `Short ${id}`, likes: 1200 })
  return fakeApi({
    '/api/videos/s1': detail('s1'),
    '/api/videos/s2': detail('s2'),
    '/api/videos/s3': detail('s3'),
    '/api/videos/s4': detail('s4'),
    '/api/videos/v1': videoDetail({ id: 'v1', type: 'video' }),
    '/api/videos': (url: URL) =>
      url.searchParams.get('cursor') === 'p2'
        ? page([videoCard({ id: 's4', type: 'short' })], 3)
        : page([videoCard({ id: 's2', type: 'short' }), videoCard({ id: 's3', type: 'short' })], 3, 'p2'),
  })
}

const heading = () => screen.findByRole('heading', { level: 1 })

describe('ShortsPage (/shorts/<id>)', () => {
  afterEach(() => {
    shortsPlayer.reload()
    i18n.setLang('en')
  })

  it("plays the short and asks the API for the library's other shorts, without this one", async () => {
    const backend = shortsBackend()
    goTo('/shorts/s1')

    render(App)

    expect(await heading()).toHaveTextContent('Short s1')
    expect(document.title).toBe('Short s1 - TonTuyau')
    const params = backend.requests('/api/videos')[0]?.searchParams
    expect(params?.get('type')).toBe('short')
    expect(params?.get('sort')).toBe('latest')
    expect(params?.get('exclude')).toBe('s1')
    expect(params?.has('channel')).toBe(false)
    expect(backend.requests('/api/channels/UCtest/videos')).toEqual([])
    expect(screen.getByLabelText('1,200 likes')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open in the normal player' })).toHaveAttribute('href', '/watch?v=s1')
    expect(screen.getByRole('button', { name: 'Previous short' })).toBeDisabled()
  })

  it('moves down and up with the buttons, in the API order, replacing the URL', async () => {
    shortsBackend()
    goTo('/shorts/s1')
    const user = userEvent.setup()
    const historyLength = history.length
    render(App)
    await heading()

    const next = screen.getByRole('button', { name: 'Next short' })
    await vi.waitFor(() => expect(next).toBeEnabled())
    await user.click(next)

    expect(router.path).toBe('/shorts/s2')
    expect(await heading()).toHaveTextContent('Short s2')
    expect(history.length).toBe(historyLength)

    await user.click(screen.getByRole('button', { name: 'Previous short' }))
    expect(router.path).toBe('/shorts/s1')
  })

  it('moves with the ↓ ↑ keys, but not while typing in the search box', async () => {
    shortsBackend()
    goTo('/shorts/s1')
    const user = userEvent.setup()
    render(App)
    await heading()
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Next short' })).toBeEnabled())

    await user.keyboard('{ArrowDown}')
    expect(router.path).toBe('/shorts/s2')
    await user.keyboard('{ArrowUp}')
    expect(router.path).toBe('/shorts/s1')

    await user.click(screen.getByRole('combobox', { name: 'Search' }))
    await user.keyboard('{ArrowDown}')
    expect(router.path).toBe('/shorts/s1')
  })

  it('moves one short for one Firefox wheel notch (3 lines, DOM_DELTA_LINE)', async () => {
    shortsBackend()
    goTo('/shorts/s1')
    render(App)
    await heading()
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Next short' })).toBeEnabled())
    const stage = document.querySelector('.shorts-page') as HTMLElement

    await fireEvent.wheel(stage, { deltaY: 3, deltaMode: WheelEvent.DOM_DELTA_LINE })

    expect(router.path).toBe('/shorts/s2')
  })

  it('moves one short per wheel gesture', async () => {
    shortsBackend()
    goTo('/shorts/s1')
    render(App)
    await heading()
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Next short' })).toBeEnabled())
    const stage = document.querySelector('.shorts-page') as HTMLElement

    for (let event = 0; event < 20; event++) {
      await fireEvent.wheel(stage, { deltaY: 30 })
    }

    expect(router.path).toBe('/shorts/s2')
  })

  // 1.1.0, the Firefox stall: the shorts left behind must not keep downloading (and holding connections).
  it('keeps one <video> alive while scrolling through many shorts, each one left emptied', async () => {
    const ids = Array.from({ length: 12 }, (_, index) => `s${String(index + 1).padStart(2, '0')}`)
    fakeApi({
      ...Object.fromEntries(ids.map((id) => [`/api/videos/${id}`, videoDetail({ id, type: 'short', title: id })])),
      '/api/videos': page(
        ids.slice(1).map((id) => videoCard({ id, type: 'short' })),
        ids.length - 1,
      ),
    })
    vi.spyOn(HTMLMediaElement.prototype, 'canPlayType').mockReturnValue('probably')
    const load = vi.spyOn(HTMLMediaElement.prototype, 'load')
    goTo(`/shorts/${ids[0]}`)
    const user = userEvent.setup()
    render(App)
    const seen = new Set<HTMLVideoElement>()

    for (const id of ids) {
      expect(await heading()).toHaveTextContent(id)
      const alive = document.querySelectorAll('video')
      expect(alive).toHaveLength(1)
      seen.add(alive[0] as HTMLVideoElement)
      if (id !== ids.at(-1)) {
        await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Next short' })).toBeEnabled())
        await user.keyboard('{ArrowDown}')
      }
    }

    const left = [...seen].filter((media) => !media.isConnected)
    expect(seen.size).toBe(ids.length)
    expect(left).toHaveLength(ids.length - 1)
    for (const media of left) {
      expect(media).not.toHaveAttribute('src')
      expect(load.mock.contexts).toContain(media)
    }
  })

  it('loads the next page of the sequence before reaching its end', async () => {
    const backend = shortsBackend()
    goTo('/shorts/s1')
    render(App)
    await heading()

    await vi.waitFor(() => expect(backend.requests('/api/videos')).toHaveLength(2))
    expect(backend.requests('/api/videos')[1]?.searchParams.get('cursor')).toBe('p2')
  })

  it('opens a video that is not a short on the watch page', async () => {
    shortsBackend()
    goTo('/shorts/v1')
    render(App)

    await vi.waitFor(() => expect(router.path).toBe('/watch'))
    expect(router.query.get('v')).toBe('v1')
  })

  it('sends shorts to the watch page with the "normal" setting, without loading the vertical player', async () => {
    shortsPlayer.set('normal')
    shortsBackend()
    goTo('/shorts/s1')
    render(App)

    await vi.waitFor(() => expect(router.path).toBe('/watch'))
    expect(router.query.get('v')).toBe('s1')
    expect(document.querySelector('.shorts-page')).toBeNull()
  })

  it('shows the not-found state for an unknown short', async () => {
    fakeApi({ '/api/videos/unknownVid1': problemResponse(404, 'No video with id unknownVid1.', 'Not found') })
    goTo('/shorts/unknownVid1')
    render(App)

    expect(await screen.findByText("This video isn't available")).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Shorts navigation' })).not.toBeInTheDocument()
  })

  it('keeps the error state with Retry when the server fails', async () => {
    fakeApi({ '/api/videos/s0000000001': problemResponse(500, 'Boom.', 'Internal error') })
    goTo('/shorts/s0000000001')
    render(App)

    expect(await screen.findByRole('alert')).toHaveTextContent('The server ran into a problem')
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('labels the navigation in French', async () => {
    i18n.setLang('fr')
    shortsBackend()
    goTo('/shorts/s1')
    render(App)
    await heading()

    const nav = screen.getByRole('navigation', { name: 'Navigation dans les Shorts' })
    expect(within(nav).getByRole('button', { name: 'Short suivant' })).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Short précédent' })).toBeDisabled()
  })

  it("frames the short in its own shape (the API's video track)", async () => {
    fakeApi({
      '/api/videos/sq000000001': videoDetail({
        id: 'sq000000001',
        type: 'short',
        tracks: [{ kind: 'video', codec: 'av1', width: 2160, height: 2160, bitrate: 1 }],
      }),
      '/api/videos': page([]),
    })
    goTo('/shorts/sq000000001')
    render(App)

    await heading()
    const frame = document.querySelector('div.frame') as HTMLElement
    await vi.waitFor(() => expect(frame.style.getPropertyValue('--video-ratio')).toBe('2160 / 2160'))
  })

  // 1.1.0: the frame is the short's exact shape (no black bars), so the title overlay sits on the picture's bottom
  // edge, where the browser's controls are drawn too: it steps aside while they may be showing.
  describe('the title overlay', () => {
    async function open() {
      vi.spyOn(HTMLMediaElement.prototype, 'canPlayType').mockReturnValue('probably')
      fakeApi({
        '/api/videos/sq000000001': videoDetail({
          id: 'sq000000001',
          type: 'short',
          tracks: [{ kind: 'video', codec: 'h264', width: 1080, height: 1350, bitrate: 1 }],
        }),
        '/api/videos': page([]),
      })
      goTo('/shorts/sq000000001')
      render(App)
      await heading()
      return document.querySelector('div.frame') as HTMLElement
    }

    it("gives the frame the short's ratio as a number too, to size it within the page", async () => {
      const frame = await open()

      await vi.waitFor(() => expect(frame.style.getPropertyValue('--video-ratio-value')).toBe('0.8'))
      expect(frame.style.getPropertyValue('--video-ratio')).toBe('1080 / 1350')
    })

    it('steps aside while the video is paused (its controls show), and comes back when it plays', async () => {
      const frame = await open()
      const media = frame.querySelector('video') as HTMLVideoElement

      await fireEvent.pause(media)
      expect(frame).toHaveClass('controls')
      await fireEvent.play(media)
      expect(frame).not.toHaveClass('controls')
    })

    it('steps aside for a few seconds after a touch (touch screens show the controls on a tap)', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true })
      try {
        const frame = await open()

        await fireEvent.touchStart(frame, { touches: [{ clientY: 300 }] })
        expect(frame).toHaveClass('controls')
        await vi.advanceTimersByTimeAsync(3000)
        expect(frame).not.toHaveClass('controls')
      } finally {
        vi.useRealTimers()
      }
    })
  })
})
