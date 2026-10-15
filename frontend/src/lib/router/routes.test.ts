import { href, matchRoute, mergeQuery, safeNext, startTime } from './routes'

describe('routes', () => {
  it.each([
    ['/', { name: 'home' }],
    ['/feed/shorts', { name: 'feed', type: 'short' }],
    ['/feed/streams', { name: 'feed', type: 'stream' }],
    ['/feed/movies', { name: 'notFound' }],
    ['/channels', { name: 'channels' }],
    ['/channels/', { name: 'channels' }],
    ['/channel/UCabc', { name: 'channel', id: 'UCabc', tab: null }],
    ['/channel/UCabc/shorts', { name: 'channel', id: 'UCabc', tab: 'shorts' }],
    ['/channel/UCabc/playlists', { name: 'notFound' }],
    ['/channel/%E0%A4%A', { name: 'notFound' }],
    ['/watch', { name: 'watch' }],
    ['/shorts/abc-_123', { name: 'shorts', id: 'abc-_123' }],
    ['/shorts', { name: 'notFound' }],
    ['/shorts/a/b', { name: 'notFound' }],
    ['/shorts/%E0%A4%A', { name: 'notFound' }],
    ['/results', { name: 'results' }],
    ['/login', { name: 'login' }],
    ['/admin', { name: 'admin', page: 'dashboard' }],
    ['/admin/tasks', { name: 'admin', page: 'tasks' }],
    ['/admin/tasks/x', { name: 'notFound' }],
    ['/admin/runs/run-20261012T120000Z-3f9a1c', { name: 'admin', page: 'run', id: 'run-20261012T120000Z-3f9a1c' }],
    ['/admin/runs', { name: 'notFound' }],
    ['/admin/api-keys', { name: 'admin', page: 'apiKeys' }],
    ['/admin/youtube', { name: 'admin', page: 'youTube' }],
    ['/admin/channels/UCx7Lm2QpB0vK9sTq4aZ1wYc', { name: 'admin', page: 'channel', id: 'UCx7Lm2QpB0vK9sTq4aZ1wYc' }],
    ['/admin/api-keys/1', { name: 'notFound' }],
    ['/admin/runs/a/b', { name: 'notFound' }],
    ['/admin/runs/%E0%A4%A', { name: 'notFound' }],
    ['/admin/users', { name: 'notFound' }],
    ['/somewhere/else/entirely/deep', { name: 'notFound' }],
  ])('matches %s', (path, expected) => {
    expect(matchRoute(path)).toEqual(expected)
  })

  it('builds every in-app link', () => {
    expect(href.home()).toBe('/')
    expect(href.admin()).toBe('/admin')
    expect(href.adminTasks()).toBe('/admin/tasks')
    expect(href.adminTasks('full')).toBe('/admin/tasks?task=full')
    expect(href.adminRun('run-20261012T120000Z-3f9a1c')).toBe('/admin/runs/run-20261012T120000Z-3f9a1c')
    expect(href.short('a/b')).toBe('/shorts/a%2Fb')
    expect(href.watch('a b&c')).toBe('/watch?v=a+b%26c')
    expect(href.watch('abc', 83)).toBe('/watch?v=abc&t=83')
    expect(href.watch('abc', 0)).toBe('/watch?v=abc')
    expect(href.channel('UC/x', 'streams', 'popular')).toBe('/channel/UC%2Fx/streams?sort=popular')
    expect(href.channel('UCx')).toBe('/channel/UCx')
    expect(href.channels({ q: 'élé', sort: null })).toBe('/channels?q=%C3%A9l%C3%A9')
    expect(href.feed('short')).toBe('/feed/shorts')
    expect(href.results({ q: 'cuisine', channel: 'UCx', type: null })).toBe('/results?q=cuisine&channel=UCx')
  })

  it('sends the login page back to a page of this site only', () => {
    const origin = 'http://tontuyau.lan'
    expect(href.login('/watch?v=abc')).toBe('/login?next=%2Fwatch%3Fv%3Dabc')
    expect(href.login('/')).toBe('/login')
    expect(safeNext('/watch?v=abc&t=83', origin)).toBe('/watch?v=abc&t=83')
    expect(safeNext('/channel/UCx/shorts#top', origin)).toBe('/channel/UCx/shorts#top')
    for (const elsewhere of [null, '', 'watch', 'https://evil.example/', '//evil.example', '/\\evil.example']) {
      expect(safeNext(elsewhere, origin)).toBe('/')
    }
    expect(safeNext('/\t/evil.example', origin)).toBe('/')
    expect(safeNext('/login?next=%2Fchannels', origin)).toBe('/')
  })

  it('round-trips a channel link through the matcher', () => {
    expect(matchRoute(new URL(href.channel('UC-é/x', 'videos'), 'http://h').pathname)).toEqual({
      name: 'channel',
      id: 'UC-é/x',
      tab: 'videos',
    })
  })

  it('changes one query parameter and keeps the others', () => {
    expect(mergeQuery('?q=a&type=short', { type: null, sort: 'latest' })).toBe('?q=a&sort=latest')
    expect(mergeQuery('?q=a', { q: '' })).toBe('')
  })

  it.each([
    ['83', 83],
    ['83s', 83],
    ['1m23s', 83],
    ['1h2m3s', 3723],
    ['2h', 7200],
    ['0', 0],
  ])('reads the watch page start time %s', (value, seconds) => {
    expect(startTime(value)).toBe(seconds)
  })

  it.each([[null], [''], ['abc'], ['-5'], ['1:23'], ['1.5'], ['12x']])(
    "ignores a start time that isn't one: %s",
    (value) => {
      expect(startTime(value)).toBeNull()
    },
  )
})
