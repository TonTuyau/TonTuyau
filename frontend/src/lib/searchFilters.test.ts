import { apiFilters, readResultsQuery, uploadOptions } from './searchFilters'

describe('search filters', () => {
  it('reads the URL, ignoring unknown values', () => {
    const query = readResultsQuery(new URLSearchParams('q=+creme+&type=movie&duration=long&sort=popular&channel=UCx'))

    expect(query).toEqual({
      q: 'creme',
      type: null,
      duration: 'long',
      uploaded: null,
      channel: 'UCx',
      sort: 'popular',
    })
  })

  it('sends only the filters chosen, with the API names', () => {
    const query = readResultsQuery(new URLSearchParams('q=a&type=short&uploaded=week'))

    expect(apiFilters(query)).toEqual({
      type: 'short',
      duration: undefined,
      published: 'week',
      channel: undefined,
      sort: undefined,
    })
  })

  it('never sends "channel" as a video type', () => {
    expect(apiFilters(readResultsQuery(new URLSearchParams('q=a&type=channel'))).type).toBeUndefined()
  })

  it.each(uploadOptions)('sends the upload date "%s" as the period name, computing no date', (uploaded) => {
    vi.useFakeTimers({ now: new Date('2026-10-02T18:00:00Z') })
    const sent = apiFilters(readResultsQuery(new URLSearchParams(`q=a&uploaded=${uploaded}`)))
    vi.useRealTimers()

    expect(sent.published).toBe(uploaded)
    expect(JSON.stringify(sent)).not.toMatch(/\d{4}-\d{2}-\d{2}/)
  })

  it('ignores an unknown upload date', () => {
    expect(apiFilters(readResultsQuery(new URLSearchParams('q=a&uploaded=decade'))).published).toBeUndefined()
  })
})
