import { href, matchRoute, mergeQuery } from './routes'

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
    ['/somewhere/else/entirely/deep', { name: 'notFound' }],
  ])('matches %s', (path, expected) => {
    expect(matchRoute(path)).toEqual(expected)
  })

  it('builds every in-app link', () => {
    expect(href.home()).toBe('/')
    expect(href.short('a/b')).toBe('/shorts/a%2Fb')
    expect(href.watch('a b&c')).toBe('/watch?v=a+b%26c')
    expect(href.channel('UC/x', 'streams', 'popular')).toBe('/channel/UC%2Fx/streams?sort=popular')
    expect(href.channel('UCx')).toBe('/channel/UCx')
    expect(href.channels({ q: 'élé', sort: null })).toBe('/channels?q=%C3%A9l%C3%A9')
    expect(href.feed('short')).toBe('/feed/shorts')
    expect(href.results({ q: 'cuisine', channel: 'UCx', type: null })).toBe('/results?q=cuisine&channel=UCx')
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
})
