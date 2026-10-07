import { render, screen } from '@testing-library/svelte'
import { page, videoCard } from '../test/data'
import { fakeApi, goTo } from '../test/fakeApi'
import FeedPage from './FeedPage.svelte'

describe('FeedPage', () => {
  it('lists one type from /api/videos with the URL sort and the API total', async () => {
    const backend = fakeApi({ '/api/videos': page([videoCard({ type: 'short', title: 'A short' })], 5943) })
    goTo('/feed/shorts?sort=popular')

    render(FeedPage, { type: 'short' })

    expect(await screen.findByTestId('short-card')).toHaveTextContent('A short')
    expect(screen.getByText('5,943 results')).toBeInTheDocument()
    const [url] = backend.requests('/api/videos')
    expect(Object.fromEntries(url?.searchParams ?? [])).toEqual({ type: 'short', sort: 'popular' })
  })
})
