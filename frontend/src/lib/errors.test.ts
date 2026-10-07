import { ApiError } from './api/client'
import { errorKind, isConnectionProblem, isRetryable, missingResource } from './errors'

const server = (status: number) => new ApiError({ title: 'Problem', status, detail: 'from the backend' }, true)
const bare = (status: number) => new ApiError({ title: 'Bad Gateway', status })

describe('errorKind (what an API failure means for the page)', () => {
  it.each([
    [server(400), 'invalid'],
    [server(404), 'notFound'],
    [server(500), 'server'],
    [server(503), 'server'],
    [server(416), 'other'],
    [server(405), 'other'],
    [bare(502), 'unreachable'],
    [bare(504), 'unreachable'],
    [bare(500), 'unreachable'],
    [bare(404), 'notFound'],
  ] as const)('%o → %s', (error, kind) => {
    expect(errorKind(error, true)).toBe(kind)
  })

  it('tells a lost network from a server that does not answer', () => {
    const network = new ApiError({ title: 'Network error', status: 0 })

    expect(errorKind(network, false)).toBe('offline')
    expect(errorKind(network, true)).toBe('unreachable')
  })

  it('offers a retry where it can help, and retries by itself only for connection problems', () => {
    expect(['invalid', 'notFound'].filter((kind) => isRetryable(kind as never))).toEqual([])
    expect(isRetryable('server')).toBe(true)
    expect(isRetryable('unreachable')).toBe(true)
    expect(isConnectionProblem('offline')).toBe(true)
    expect(isConnectionProblem('unreachable')).toBe(true)
    expect(isConnectionProblem('server')).toBe(false)
  })
})

describe('missingResource', () => {
  it('words a 404 as not found and a 400 as a bad link, for videos and channels', () => {
    expect(missingResource(server(404), 'video')).toEqual({ title: 'watch.notFound', hint: 'watch.notFoundHint' })
    expect(missingResource(server(400), 'video')).toEqual({ title: 'watch.invalidId', hint: 'watch.invalidIdHint' })
    expect(missingResource(server(404), 'channel')?.title).toBe('channel.notFound')
    expect(missingResource(server(400), 'channel')?.title).toBe('channel.invalidId')
  })

  it('leaves every other failure to the error state (incl. a bare 404 from something that is not the backend)', () => {
    expect(missingResource(server(500), 'video')).toBeNull()
    expect(missingResource(bare(404), 'video')).toBeNull()
    expect(missingResource(new ApiError({ title: 'Network error', status: 0 }), 'channel')).toBeNull()
  })
})
