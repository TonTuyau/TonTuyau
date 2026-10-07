import { render, screen, within } from '@testing-library/svelte'
import { channelCard, channelDetail, page, videoCard } from '../test/data'
import { fakeApi, goTo } from '../test/fakeApi'
import SearchPage from './SearchPage.svelte'

const params = (url: URL | undefined) => Object.fromEntries(url?.searchParams ?? [])

describe('SearchPage', () => {
  it('kind=all: shows the top channels, then the videos, with the API total', async () => {
    const backend = fakeApi({
      '/api/search': {
        channels: [channelCard({ name: 'Tom Cooks' })],
        videos: page([videoCard({ title: 'Tomates farcies' })], 1234),
      },
    })
    goTo('/results?q=tom')

    render(SearchPage)

    expect(await screen.findByText('Tomates farcies')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Channels' })).toHaveTextContent('Tom Cooks')
    expect(screen.getByText('1,234 results')).toBeInTheDocument()
    expect(params(backend.requests('/api/search')[0])).toEqual({ q: 'tom', kind: 'all' })
  })

  it('sends every chosen filter with the contract names', async () => {
    const backend = fakeApi({
      '/api/search': page([videoCard()]),
      '/api/channels/UCtest': channelDetail(),
    })
    goTo('/results?q=tom&type=short&duration=medium&channel=UCtest&sort=popular&uploaded=year')

    render(SearchPage)

    await screen.findByText('Crème brûlée : la recette')
    const sent = params(backend.requests('/api/search')[0])
    expect(sent).toMatchObject({
      q: 'tom',
      kind: 'videos',
      type: 'short',
      duration: 'medium',
      channel: 'UCtest',
      sort: 'popular',
      published: 'year',
    })
    // The period goes by name (v1.2): no date computed in the front.
    expect(Object.keys(sent).filter((key) => key.startsWith('published'))).toEqual(['published'])
    // The channel filter chip shows the name the API gives for the id.
    const chips = screen.getByRole('list', { name: 'Active filters' })
    expect(await within(chips).findByRole('link', { name: 'Remove filter: Tom Cooks' })).toHaveAttribute(
      'href',
      '/results?q=tom&type=short&duration=medium&sort=popular&uploaded=year',
    )
  })

  it('type "channel" searches channels only', async () => {
    const backend = fakeApi({ '/api/search': page([channelCard({ name: 'Only channels' })]) })
    goTo('/results?q=tom&type=channel')

    render(SearchPage)

    expect(await screen.findByText('Only channels')).toBeInTheDocument()
    expect(params(backend.requests('/api/search')[0])).toEqual({ q: 'tom', kind: 'channels' })
  })

  it('shows the no-results state', async () => {
    fakeApi({ '/api/search': { channels: [], videos: page([]) } })
    goTo('/results?q=zzz')

    render(SearchPage)

    expect(await screen.findByText('No results for “zzz”')).toBeInTheDocument()
  })

  it('asks to type something when there is no query, without calling the API', () => {
    const backend = fakeApi({})
    goTo('/results')

    render(SearchPage)

    expect(screen.getByText('Type something in the search box')).toBeInTheDocument()
    expect(backend.spy).not.toHaveBeenCalled()
  })

  it('opens the filters panel with options that toggle URL parameters', async () => {
    fakeApi({ '/api/search': { channels: [], videos: page([videoCard()]) } })
    goTo('/results?q=tom&duration=long')
    render(SearchPage)
    await screen.findByText('Crème brûlée : la recette')

    screen.getByRole('button', { name: 'Filters' }).click()

    expect(await screen.findByRole('link', { name: 'Under 4 minutes' })).toHaveAttribute(
      'href',
      '/results?q=tom&duration=short',
    )
    // Choosing the selected option again removes it.
    expect(screen.getByRole('link', { name: 'Over 20 minutes' })).toHaveAttribute('href', '/results?q=tom')
  })
})
