import { render, screen, waitFor } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { channelCard, page } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import ChannelsPage from './ChannelsPage.svelte'

describe('ChannelsPage', () => {
  it('lists channels in the API order with the API total and counts', async () => {
    fakeApi({
      '/api/channels': page(
        [
          channelCard({ id: 'b', name: 'Zebra', videoCounts: { videos: 0, shorts: 7, streams: 0 } }),
          channelCard({ id: 'a', name: 'Aardvark', videoCounts: { videos: 3, shorts: 0, streams: 2 } }),
        ],
        115,
      ),
    })

    render(ChannelsPage)

    const names = await screen.findAllByRole('heading', { level: 3 })
    expect(names.map((name) => name.textContent)).toEqual(['Zebra', 'Aardvark'])
    // `total` from the API, not the number of items received.
    expect(screen.getByText('115 channels')).toBeInTheDocument()
    expect(screen.getByText('7 shorts')).toBeInTheDocument()
    expect(screen.getByText('3 videos • 2 streams')).toBeInTheDocument()
  })

  it('sends the URL filter and sort to the API', async () => {
    const backend = fakeApi({ '/api/channels': page([channelCard()]) })
    goTo('/channels?q=tom&sort=subscribers')

    render(ChannelsPage)

    await screen.findByText('Tom Cooks')
    const [url] = backend.requests('/api/channels')
    expect(url?.searchParams.get('q')).toBe('tom')
    expect(url?.searchParams.get('sort')).toBe('subscribers')
    expect(screen.getByRole('link', { name: 'Subscribers' })).toHaveAttribute('aria-current', 'true')
  })

  it('filters as you type: the URL is updated and the API asked again', async () => {
    const backend = fakeApi({ '/api/channels': (url: URL) => page(url.searchParams.get('q') ? [] : [channelCard()]) })
    goTo('/channels')
    const user = userEvent.setup()
    render(ChannelsPage, { debounceMs: 0 })
    await screen.findByText('Tom Cooks')

    await user.type(screen.getByRole('searchbox', { name: 'Filter channels' }), 'élé')

    await waitFor(() => expect(location.search).toBe('?q=%C3%A9l%C3%A9'))
    expect(await screen.findByText('No channel matches “élé”')).toBeInTheDocument()
    expect(backend.requests('/api/channels').at(-1)?.searchParams.get('q')).toBe('élé')
  })

  it('loads the next page with the cursor the API gave', async () => {
    const backend = fakeApi({
      '/api/channels': (url: URL) =>
        url.searchParams.get('cursor') === 'opaque-2'
          ? page([channelCard({ id: 'two', name: 'Second page' })], 2)
          : page([channelCard({ id: 'one', name: 'First page' })], 2, 'opaque-2'),
    })
    const user = userEvent.setup()
    render(ChannelsPage)
    await screen.findByText('First page')

    await user.click(screen.getByRole('button', { name: 'Show more' }))

    expect(await screen.findByText('Second page')).toBeInTheDocument()
    expect(screen.getByText('First page')).toBeInTheDocument()
    expect(backend.requests('/api/channels').at(-1)?.searchParams.get('cursor')).toBe('opaque-2')
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()
  })

  it('shows the empty and error states', async () => {
    fakeApi({ '/api/channels': problemResponse(400, 'q: must be at most 200 characters.') })

    render(ChannelsPage)

    expect(await screen.findByRole('alert')).toHaveTextContent('q: must be at most 200 characters.')
  })
})
