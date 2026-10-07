import { fakeApi, problemResponse } from '../../test/fakeApi'
import { api, ApiError, buildUrl } from './client'

describe('API client', () => {
  it('leaves empty parameters out so the backend applies its defaults', () => {
    expect(buildUrl('/api/channels', { q: '', sort: undefined, size: 24, cursor: null })).toBe('/api/channels?size=24')
    expect(buildUrl('/api/home')).toBe('/api/home')
  })

  it('sends the contract parameters', async () => {
    const backend = fakeApi({ '/api/search': { channels: [], videos: { items: [], nextCursor: null, total: 0 } } })

    await api.searchAll({ q: 'crème', type: 'short', duration: 'long', cursor: 'abc' })

    const [url] = backend.requests('/api/search')
    expect(Object.fromEntries(url?.searchParams ?? [])).toEqual({
      q: 'crème',
      type: 'short',
      duration: 'long',
      cursor: 'abc',
      kind: 'all',
    })
  })

  it('escapes ids in paths', async () => {
    const backend = fakeApi({})

    await expect(api.video('a/b')).rejects.toBeInstanceOf(ApiError)

    expect(backend.calls[0]?.pathname).toBe('/api/videos/a%2Fb')
  })

  it('turns a ProblemDetails answer into an ApiError with its detail', async () => {
    fakeApi({ '/api/channels': problemResponse(400, 'sort: must be one of name, subscribers.', 'Invalid request') })

    const error = await api.channels({ sort: 'name' }).catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(400)
    expect((error as ApiError).problem.detail).toBe('sort: must be one of name, subscribers.')
    expect((error as ApiError).fromServer).toBe(true)
  })

  it('handles an error without a JSON body (e.g. a proxy page)', async () => {
    fakeApi({ '/api/home': new Response('<html>Bad gateway</html>', { status: 502, statusText: 'Bad Gateway' }) })

    const error = (await api.home().catch((caught: unknown) => caught)) as ApiError

    expect(error.problem).toEqual({ title: 'Bad Gateway', status: 502 })
    expect(error.fromServer).toBe(false)
  })

  it('reports a network failure as status 0', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await api.home().catch((caught: unknown) => caught)) as ApiError

    expect(error.isNetworkError).toBe(true)
    expect(error.fromServer).toBe(false)
  })
})
