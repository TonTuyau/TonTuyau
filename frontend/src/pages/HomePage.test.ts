import { render, screen, within } from '@testing-library/svelte'
import { fakeApi, problemResponse } from '../test/fakeApi'
import { videoCard } from '../test/data'
import HomePage from './HomePage.svelte'

describe('HomePage', () => {
  it('shows the sections and their items exactly in the API order (no client sorting)', async () => {
    // Deliberately not in "latest first" order, and streams before shorts: the front must keep it.
    fakeApi({
      '/api/home': {
        sections: [
          {
            type: 'video',
            items: [
              videoCard({ id: 'old', title: 'Old one', published: '2010-01-01T00:00:00Z' }),
              videoCard({ id: 'new', title: 'New one', published: '2026-01-01T00:00:00Z' }),
            ],
          },
          { type: 'stream', items: [videoCard({ id: 's1', type: 'stream', title: 'A stream' })] },
          { type: 'short', items: [videoCard({ id: 'sh1', type: 'short', title: 'A short' })] },
        ],
      },
    })

    render(HomePage)

    const headings = await screen.findAllByRole('heading', { level: 2 })
    expect(headings.map((heading) => heading.textContent)).toEqual(['Videos', 'Streams', 'Shorts'])
    const videos = screen.getByRole('region', { name: 'Videos' })
    expect(
      within(videos)
        .getAllByRole('heading', { level: 3 })
        .map((h) => h.textContent),
    ).toEqual(['Old one', 'New one'])
    expect(within(screen.getByRole('region', { name: 'Shorts' })).getByTestId('short-card')).toBeInTheDocument()
  })

  it('shows only the sections the API sent', async () => {
    fakeApi({ '/api/home': { sections: [{ type: 'short', items: [videoCard({ type: 'short' })] }] } })

    render(HomePage)

    expect(await screen.findByRole('region', { name: 'Shorts' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Videos' })).not.toBeInTheDocument()
  })

  it('shows a loading state, then the empty state', async () => {
    fakeApi({ '/api/home': { sections: [] } })

    render(HomePage)

    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true')
    expect(await screen.findByText('The library is empty')).toBeInTheDocument()
  })

  it("shows the error's ProblemDetails detail and retries", async () => {
    let calls = 0
    fakeApi({
      '/api/home': () =>
        ++calls === 1 ? problemResponse(500, 'Elasticsearch is not reachable.', 'Internal error') : { sections: [] },
    })
    render(HomePage)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('The server ran into a problem')
    expect(alert).toHaveTextContent('Error 500 · Elasticsearch is not reachable.')
    within(alert).getByRole('button', { name: 'Retry' }).click()

    expect(await screen.findByText('The library is empty')).toBeInTheDocument()
  })
})
