import { render, screen, within } from '@testing-library/svelte'
import { channelDetail, page, videoCard } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import { i18n } from '../lib/i18n/i18n.svelte'
import ChannelPage from './ChannelPage.svelte'

describe('ChannelPage', () => {
  it('shows only the tabs whose API count is > 0, even if lists would say otherwise', async () => {
    fakeApi({
      '/api/channels/UCtest': channelDetail({ videoCounts: { videos: 0, shorts: 5, streams: 2 } }),
      '/api/channels/UCtest/videos': page([videoCard({ type: 'short' })]),
    })

    render(ChannelPage, { id: 'UCtest', tab: null })

    const tabs = await screen.findByRole('navigation', { name: 'Channel content' })
    expect(
      within(tabs)
        .getAllByRole('link')
        .map((tab) => tab.textContent),
    ).toEqual(['Shorts', 'Streams'])
    expect(within(tabs).getByRole('link', { name: 'Shorts' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByText('224K subscribers • 5 shorts • 2 streams')).toBeInTheDocument()
  })

  it('asks the API for the tab type and the sort from the URL', async () => {
    const backend = fakeApi({
      '/api/channels/UCtest': channelDetail({ videoCounts: { videos: 3, shorts: 0, streams: 9 } }),
      '/api/channels/UCtest/videos': page([videoCard({ type: 'stream', title: 'A stream' })]),
    })
    goTo('/channel/UCtest/streams?sort=oldest')

    render(ChannelPage, { id: 'UCtest', tab: 'streams' })

    await screen.findByText('A stream')
    const [url] = backend.requests('/api/channels/UCtest/videos')
    expect(url?.searchParams.get('type')).toBe('stream')
    expect(url?.searchParams.get('sort')).toBe('oldest')
  })

  it('shows the banner, avatar and description from the API, and the inactive badge', async () => {
    fakeApi({
      '/api/channels/UCtest': channelDetail({ active: false }),
      '/api/channels/UCtest/videos': page([]),
    })

    const { container } = render(ChannelPage, { id: 'UCtest', tab: null })

    expect(await screen.findByRole('heading', { name: 'Tom Cooks', level: 1 })).toBeInTheDocument()
    expect([...container.querySelectorAll('img')].map((img) => img.getAttribute('src'))).toEqual([
      '/art/from-the-api/UCtest-banner',
      '/art/from-the-api/UCtest-thumb',
    ])
    expect(screen.getByText('A cooking channel.')).toBeInTheDocument()
    expect(screen.getByText('Channel unavailable on YouTube')).toBeInTheDocument()
  })

  it('says so when the channel has nothing yet, without asking for a list', async () => {
    const backend = fakeApi({
      '/api/channels/UCtest': channelDetail({ videoCounts: { videos: 0, shorts: 0, streams: 0 }, bannerUrl: null }),
    })

    render(ChannelPage, { id: 'UCtest', tab: null })

    expect(await screen.findByText('This channel has no videos yet')).toBeInTheDocument()
    expect(backend.requests('/api/channels/UCtest/videos')).toHaveLength(0)
  })

  it('shows the not-found state for an unknown channel (404), with no retry', async () => {
    fakeApi({ '/api/channels/UCunknownunknownunknown1': problemResponse(404, 'No channel.', 'Not found') })

    render(ChannelPage, { id: 'UCunknownunknownunknown1', tab: null })

    expect(await screen.findByText("This channel isn't available")).toBeInTheDocument()
    expect(screen.getByText("There's no channel with this id in the library.")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to Home' })).toHaveAttribute('href', '/')
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument()
    expect(document.title).toBe("This channel isn't available - TonTuyau")
  })

  it('says the link is wrong for a malformed channel id (the API answers 400), in French too', async () => {
    fakeApi({ '/api/channels/UCnope': problemResponse(400, "id: isn't a channel id.", 'Invalid request') })
    i18n.setLang('fr')

    render(ChannelPage, { id: 'UCnope', tab: null })

    expect(await screen.findByText("Ce lien de chaîne n'est pas valide")).toBeInTheDocument()
    i18n.setLang('en')
  })

  it('keeps the error state with Retry for a server error on the channel', async () => {
    fakeApi({ '/api/channels/UCtest': problemResponse(500, 'Boom.', 'Internal error') })

    render(ChannelPage, { id: 'UCtest', tab: null })

    expect(await screen.findByRole('alert')).toHaveTextContent('The server ran into a problem')
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it("gives the banner and the avatar the API's versions (v1.4)", async () => {
    fakeApi({ '/api/channels/UCtest': channelDetail(), '/api/channels/UCtest/videos': page([]) })

    const { container } = render(ChannelPage, { id: 'UCtest', tab: null })

    await screen.findByRole('heading', { level: 1 })
    const banner = container.querySelector('img.banner') as HTMLImageElement
    expect(banner).toHaveAttribute(
      'srcset',
      '/art/from-the-api/UCtest-banner/1280 1280w, /art/from-the-api/UCtest-banner 2560w',
    )
    expect(banner).toHaveAttribute('sizes', '(max-width: 1284px) 100vw, 1284px')
    const avatar = container.querySelector('.head .avatar img') as HTMLImageElement
    expect(avatar).toHaveAttribute(
      'srcset',
      '/art/from-the-api/UCtest-thumb/tiny 48w, /art/from-the-api/UCtest-thumb 900w',
    )
    expect(avatar).toHaveAttribute('sizes', '160px')
  })
})
