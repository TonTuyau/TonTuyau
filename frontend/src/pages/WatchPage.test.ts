import { fireEvent, render, screen, within } from '@testing-library/svelte'
import type { Playability } from '../lib/codec'
import { i18n } from '../lib/i18n/i18n.svelte'
import { PLAYER_SIZE_STORAGE_KEY, playerSize } from '../lib/playerSize.svelte'
import { page, videoCard, videoDetail } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import WatchPage from './WatchPage.svelte'

const probably = (): Playability => 'probably'

describe('WatchPage', () => {
  afterEach(() => i18n.setLang('en'))

  it('shows the video facts from the API and the download link', async () => {
    fakeApi({
      '/api/videos/vid00000001': videoDetail({ active: false }),
      '/api/channels/UCtest/videos': page([videoCard({ id: 'other', title: 'Another one' })]),
    })
    goTo('/watch?v=vid00000001')

    render(WatchPage, { probe: probably })

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('Crème brûlée : la recette')
    expect(screen.getByText('Unavailable on YouTube')).toHaveAttribute(
      'title',
      'TubeArchivist can no longer reach this video on YouTube; this is the archived copy.',
    )
    expect(screen.getByText(/this is the archived copy/, { selector: '.hint' })).toBeInTheDocument()
    expect(screen.getByText(/74,198 views/)).toBeInTheDocument()
    expect(screen.getByLabelText('3,166 likes')).toHaveTextContent('3.2K')
    expect(screen.getByRole('link', { name: /Download · 1.3 GB/ })).toHaveAttribute(
      'href',
      '/media/from-the-api/vid00000001/download',
    )
    expect(screen.getByRole('link', { name: '#cuisine' })).toHaveAttribute('href', '/results?q=cuisine')
    expect(document.title).toBe('Crème brûlée : la recette - TonTuyau')
  })

  it('shows the channel subscribers from the API under its name, in both languages', async () => {
    fakeApi({ '/api/videos/vid00000001': videoDetail(), '/api/channels/UCtest/videos': page([]) })
    goTo('/watch?v=vid00000001')
    render(WatchPage, { probe: probably })

    const channel = await screen.findByRole('link', { name: /Tom Cooks/ })
    expect(channel).toHaveTextContent('224K subscribers')

    i18n.setLang('fr')
    expect(await within(channel).findByText(/^224\sk abonnés$/)).toBeInTheDocument()
  })

  it("asks the API for the channel's latest of the same type without this video, and shows them as sent", async () => {
    const backend = fakeApi({
      '/api/videos/vid00000001': videoDetail({ type: 'short' }),
      // Whatever the API sends is shown: the front filters nothing out, even this video's id.
      '/api/channels/UCtest/videos': page([
        videoCard({ id: 'vid00000001', title: 'Sent anyway' }),
        videoCard({ id: 'x', title: 'Other' }),
      ]),
    })
    goTo('/watch?v=vid00000001')

    render(WatchPage, { probe: probably })

    const side = await screen.findByRole('complementary', { name: 'More from Tom Cooks' })
    expect(await within(side).findByText('Other')).toBeInTheDocument()
    expect(within(side).getByText('Sent anyway')).toBeInTheDocument()
    expect(within(side).queryByText('Now playing')).not.toBeInTheDocument()
    const params = backend.requests('/api/channels/UCtest/videos')[0]?.searchParams
    expect(params?.get('type')).toBe('short')
    expect(params?.get('exclude')).toBe('vid00000001')
    expect(params?.get('sort')).toBe('latest')
  })

  it('reveals categories and file details when the description is expanded', async () => {
    fakeApi({ '/api/videos/vid00000001': videoDetail(), '/api/channels/UCtest/videos': page([]) })
    goTo('/watch?v=vid00000001')
    render(WatchPage, { probe: probably })

    const more = await screen.findByRole('button', { name: '…more' })
    more.click()

    expect(await screen.findByText('Howto & Style')).toBeInTheDocument()
    expect(screen.getByText(/AV1 · 3840×2160/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Show less' })).toHaveAttribute('aria-expanded', 'true')
  })

  it('shows the not-found state for an unknown video (404), with no retry, and names the tab', async () => {
    fakeApi({ '/api/videos/unknownVid1': problemResponse(404, 'No video with id unknownVid1.', 'Not found') })
    goTo('/watch?v=unknownVid1')

    render(WatchPage)

    expect(await screen.findByText("This video isn't available")).toBeInTheDocument()
    expect(screen.getByText("There's no video with this id in the library.")).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument()
    expect(document.title).toBe("This video isn't available - TonTuyau")
  })

  it('says the link is wrong for a malformed id (the API answers 400)', async () => {
    fakeApi({ '/api/videos/nope': problemResponse(400, "id: isn't a video id.", 'Invalid request') })
    goTo('/watch?v=nope')

    render(WatchPage)

    expect(await screen.findByText("This video link isn't valid")).toBeInTheDocument()
    expect(screen.getByText("The id in the address isn't a YouTube video id.")).toBeInTheDocument()
  })

  it('says so when the channel has nothing else to show beside the video', async () => {
    fakeApi({ '/api/videos/vid00000001': videoDetail(), '/api/channels/UCtest/videos': page([]) })
    goTo('/watch?v=vid00000001')

    render(WatchPage, { probe: probably })

    expect(await screen.findByText('No other videos from this channel')).toBeInTheDocument()
  })

  it("says so when there's no ?v=, without calling the API", () => {
    const backend = fakeApi({})
    goTo('/watch')

    render(WatchPage)

    expect(screen.getByText('No video selected')).toBeInTheDocument()
    expect(backend.spy).not.toHaveBeenCalled()
  })

  describe("the channel's other videos: infinite scroll", () => {
    // jsdom has no IntersectionObserver: this one records what is observed and lets the test bring it into view.
    let observers: { callback: IntersectionObserverCallback; targets: Element[] }[] = []
    beforeEach(() => {
      observers = []
      vi.stubGlobal(
        'IntersectionObserver',
        class {
          readonly entry: { callback: IntersectionObserverCallback; targets: Element[] }
          constructor(callback: IntersectionObserverCallback) {
            this.entry = { callback, targets: [] }
            observers.push(this.entry)
          }
          observe(target: Element) {
            this.entry.targets.push(target)
          }
          disconnect() {
            this.entry.targets = []
          }
        },
      )
    })
    afterEach(() => vi.unstubAllGlobals())

    /** Scrolls the side list's end into view (its sentinel intersects). */
    function scrollToEnd() {
      const watching = observers.filter((o) => o.targets.length > 0)
      expect(watching).toHaveLength(1)
      for (const { callback, targets } of watching) {
        callback(
          targets.map((target) => ({ isIntersecting: true, target }) as unknown as IntersectionObserverEntry),
          {} as IntersectionObserver,
        )
      }
    }

    // Two videos of the same channel; each one's side list has two pages, told apart by `exclude`.
    const backend = () =>
      fakeApi({
        '/api/videos/vid00000001': videoDetail(),
        '/api/videos/vid00000002': videoDetail({ id: 'vid00000002', title: 'The second video' }),
        '/api/channels/UCtest/videos': (url: URL) => {
          const of = url.searchParams.get('exclude') === 'vid00000002' ? 'B' : 'A'
          return url.searchParams.get('cursor')
            ? page([videoCard({ id: `${of}2`, title: `${of} page 2` })], 2)
            : page([videoCard({ id: `${of}1`, title: `${of} page 1` })], 2, `${of}-cursor`)
        },
      })
    const side = () => screen.getByRole('complementary', { name: 'More from Tom Cooks' })
    const titles = () =>
      within(side())
        .queryAllByRole('heading', { level: 3 })
        .map((heading) => heading.textContent)

    it('loads the next page when the end of the list comes into view', async () => {
      const api = backend()
      goTo('/watch?v=vid00000001')
      render(WatchPage, { probe: probably })
      await screen.findByText('A page 1')

      scrollToEnd()

      expect(await screen.findByText('A page 2')).toBeInTheDocument()
      expect(titles()).toEqual(['A page 1', 'A page 2'])
      const second = api.requests('/api/channels/UCtest/videos')[1]?.searchParams
      expect(second?.get('cursor')).toBe('A-cursor')
      expect(second?.get('exclude')).toBe('vid00000001')
      // The last page: no more sentinel, no button.
      expect(within(side()).queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()
    })

    it('another video starts its list again from page 1, without the previous pages', async () => {
      const api = backend()
      goTo('/watch?v=vid00000001')
      render(WatchPage, { probe: probably })
      await screen.findByText('A page 1')
      scrollToEnd()
      await screen.findByText('A page 2')

      // A card of the list was clicked: the router moves to the other video.
      goTo('/watch?v=vid00000002')

      expect(await screen.findByText('B page 1')).toBeInTheDocument()
      expect(titles()).toEqual(['B page 1'])
      const first = api.requests('/api/channels/UCtest/videos').at(-1)?.searchParams
      expect(first?.get('cursor')).toBeNull()
      expect(first?.get('exclude')).toBe('vid00000002')

      scrollToEnd()

      expect(await screen.findByText('B page 2')).toBeInTheDocument()
      expect(titles()).toEqual(['B page 1', 'B page 2'])
      expect(api.requests('/api/channels/UCtest/videos').at(-1)?.searchParams.get('cursor')).toBe('B-cursor')
    })

    it('keeps the "Show more" button, and a retry when the next page fails', async () => {
      let fail = true
      fakeApi({
        '/api/videos/vid00000001': videoDetail(),
        '/api/channels/UCtest/videos': (url: URL) => {
          if (!url.searchParams.get('cursor')) {
            return page([videoCard({ id: 'A1', title: 'A page 1' })], 2, 'A-cursor')
          }
          return fail
            ? problemResponse(503, 'Elasticsearch is down.')
            : page([videoCard({ id: 'A2', title: 'A page 2' })], 2)
        },
      })
      goTo('/watch?v=vid00000001')
      render(WatchPage, { probe: probably })
      await screen.findByText('A page 1')

      within(side()).getByRole('button', { name: 'Show more' }).click()

      expect(await within(side()).findByText(/Elasticsearch is down/)).toBeInTheDocument()
      fail = false
      within(side()).getByRole('button', { name: 'Retry' }).click()
      expect(await screen.findByText('A page 2')).toBeInTheDocument()
      expect(titles()).toEqual(['A page 1', 'A page 2'])
    })
  })

  describe('small / large player (M5)', () => {
    afterEach(() => playerSize.reload())
    const layout = () => document.querySelector('.watch') as HTMLElement
    const backend = () =>
      fakeApi({
        '/api/videos/vid00000001': videoDetail(),
        '/api/channels/UCtest/videos': page([videoCard({ id: 'o' })]),
      })

    it('starts small (the default view), and the button on the player makes it large, remembered', async () => {
      backend()
      goTo('/watch?v=vid00000001')
      render(WatchPage, { probe: probably })

      await fireEvent.click(await screen.findByRole('button', { name: 'Large player' }))

      expect(layout()).toHaveAttribute('data-player-size', 'large')
      expect(layout()).toHaveClass('large')
      expect(localStorage.getItem(PLAYER_SIZE_STORAGE_KEY)).toBe('large')
      await fireEvent.click(screen.getByRole('button', { name: 'Small player' }))
      expect(layout()).toHaveAttribute('data-player-size', 'small')
      expect(localStorage.getItem(PLAYER_SIZE_STORAGE_KEY)).toBe('small')
    })

    it('opens large when that was the last choice', async () => {
      localStorage.setItem(PLAYER_SIZE_STORAGE_KEY, 'large')
      playerSize.reload()
      backend()
      goTo('/watch?v=vid00000001')
      render(WatchPage, { probe: probably })

      await screen.findByRole('heading', { level: 1 })
      expect(layout()).toHaveAttribute('data-player-size', 'large')
    })
  })

  it('drops the download button when the player finds the file missing on the share', async () => {
    const api = fakeApi({ '/api/videos/vid00000001': videoDetail(), '/api/channels/UCtest/videos': page([]) })
    api.spy.mockImplementation(async (input, init) => {
      const url = new URL(String(input), 'http://localhost')
      if (init?.method === 'HEAD') return new Response(null, { status: 404 })
      return url.pathname === '/api/videos/vid00000001' ? Response.json(videoDetail()) : Response.json(page([]))
    })
    goTo('/watch?v=vid00000001')
    const { container } = render(WatchPage, { probe: probably })
    expect(await screen.findByRole('link', { name: /Download · 1.3 GB/ })).toBeInTheDocument()

    await fireEvent.error(container.querySelector('video') as HTMLVideoElement)

    expect(await screen.findByText('File unavailable')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Download/ })).not.toBeInTheDocument()
  })
})
