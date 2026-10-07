import { render, screen, waitFor } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { router } from '../lib/router/router.svelte'
import { fakeApi } from '../test/fakeApi'
import SearchBox from './SearchBox.svelte'

const suggestions = {
  suggestions: [
    { text: 'Tom Cooks', kind: 'channel', id: 'UCtom' },
    { text: 'Tomates farcies', kind: 'video', id: 'vidTomates1' },
    { text: 'Tomahawk steak', kind: 'video', id: 'vidTomahawk' },
  ],
}

async function typeAndWait(text: string) {
  const user = userEvent.setup()
  const input = screen.getByRole('combobox', { name: 'Search' })
  await user.type(input, text)
  await screen.findByRole('option', { name: /Tom Cooks/ })
  return { user, input }
}

describe('SearchBox', () => {
  it('asks /api/search/suggest and lists the suggestions in the API order', async () => {
    const backend = fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })

    await typeAndWait('tom')

    const options = screen.getAllByRole('option').map((option) => option.textContent?.replace(/\s+/g, ' ').trim())
    expect(options).toEqual(['Tom Cooks Channel', 'Tomates farcies Video', 'Tomahawk steak Video'])
    expect(backend.requests('/api/search/suggest').at(-1)?.searchParams.get('q')).toBe('tom')
  })

  it('debounces: one request for a burst of typing', async () => {
    const backend = fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 50 })

    await typeAndWait('tomat')

    expect(backend.requests('/api/search/suggest')).toHaveLength(1)
  })

  it('moves with ↓ ↑ (wrapping) and searches a channel suggestion’s text with Enter, like YouTube', async () => {
    fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })
    const { user, input } = await typeAndWait('tom')

    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('option', { name: /Tom Cooks/ })).toHaveAttribute('aria-selected', 'true')
    expect(input.getAttribute('aria-activedescendant')).toBe(screen.getByRole('option', { name: /Tom Cooks/ }).id)
    await user.keyboard('{ArrowUp}')
    expect(screen.getByRole('option', { name: /Tomahawk/ })).toHaveAttribute('aria-selected', 'true')
    await user.keyboard('{ArrowDown}{Enter}')

    expect(router.route).toEqual({ name: 'results' })
    expect(router.query.get('q')).toBe('Tom Cooks')
    expect(input).toHaveValue('Tom Cooks')
    expect(screen.queryByRole('option')).not.toBeInTheDocument()
  })

  it('searches a video suggestion’s text too (no watch page, no other parameter)', async () => {
    fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })
    const { user, input } = await typeAndWait('tom')

    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}')

    expect(location.pathname + location.search).toBe('/results?q=Tomates+farcies')
    expect(input).toHaveValue('Tomates farcies')
  })

  it('searches a suggestion picked with the mouse', async () => {
    fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })
    const { user, input } = await typeAndWait('tom')

    await user.click(screen.getByRole('option', { name: /Tomahawk steak/ }))

    expect(location.pathname + location.search).toBe('/results?q=Tomahawk+steak')
    expect(input).toHaveValue('Tomahawk steak')
  })

  it('closes the list with Esc and keeps the text', async () => {
    fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })
    const { user, input } = await typeAndWait('tom')

    await user.keyboard('{Escape}')

    expect(screen.queryByRole('option')).not.toBeInTheDocument()
    expect(input).toHaveAttribute('aria-expanded', 'false')
    expect(input).toHaveValue('tom')
  })

  it('searches the typed text with Enter when no suggestion is picked', async () => {
    fakeApi({ '/api/search/suggest': suggestions })
    render(SearchBox, { debounceMs: 0 })
    const { user } = await typeAndWait('tom ')

    await user.keyboard('{Enter}')

    await waitFor(() => expect(location.pathname + location.search).toBe('/results?q=tom'))
  })
})
