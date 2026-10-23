import { router } from './router.svelte'

function link(url: string, attributes: Record<string, string> = {}): HTMLAnchorElement {
  const anchor = document.createElement('a')
  anchor.href = url
  for (const [name, value] of Object.entries(attributes)) {
    anchor.setAttribute(name, value)
  }
  anchor.textContent = 'link'
  document.body.append(anchor)
  return anchor
}

describe('router', () => {
  let stop: () => void
  beforeEach(() => {
    stop = router.start()
  })
  afterEach(() => {
    stop()
    document.body.innerHTML = ''
  })

  it('navigates with pushState and exposes the route and query', () => {
    const before = history.length

    router.navigate('/channel/UCx/shorts?sort=popular')

    expect(location.pathname).toBe('/channel/UCx/shorts')
    expect(history.length).toBe(before + 1)
    expect(router.route).toEqual({ name: 'channel', id: 'UCx', tab: 'shorts' })
    expect(router.query.get('sort')).toBe('popular')
  })

  it('replaces the entry when asked (as-you-type filters)', () => {
    router.navigate('/channels')
    const before = history.length

    router.setQuery({ q: 'cuis' }, { replace: true })

    expect(location.search).toBe('?q=cuis')
    expect(history.length).toBe(before)
  })

  it('follows back and forward', () => {
    router.navigate('/channels')
    router.navigate('/watch?v=abc')

    history.replaceState(null, '', '/channels')
    window.dispatchEvent(new PopStateEvent('popstate'))

    expect(router.route).toEqual({ name: 'channels' })
  })

  it('starts a navigation at the top of the page, but for a link that keeps the scroll (a filter, 2.1.1)', () => {
    const scrollTo = vi.spyOn(window, 'scrollTo').mockImplementation(() => {})

    link('/admin/tasks?task=youTubeSync', { 'data-keep-scroll': '' }).click()
    expect(location.search).toBe('?task=youTubeSync')
    expect(scrollTo).not.toHaveBeenCalled()

    link('/feed/streams').click()
    expect(scrollTo).toHaveBeenCalledWith({ top: 0 })
    scrollTo.mockRestore()
  })

  it('turns a click on an in-app link into a navigation', () => {
    link('/feed/streams').click()

    expect(location.pathname).toBe('/feed/streams')
    expect(router.route).toEqual({ name: 'feed', type: 'stream' })
  })

  it.each([
    ['a file download', '/media/abc/download', { download: '' }],
    ['a media URL', '/media/abc', {}],
    ['the API', '/api/home', {}],
    ['a new tab', '/channels', { target: '_blank' }],
    ['another origin', 'https://example.org/watch?v=1', {}],
  ])('leaves %s to the browser', (_label, url, attributes) => {
    const anchor = link(url, attributes)
    const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 })

    anchor.dispatchEvent(event)

    expect(event.defaultPrevented).toBe(false)
  })

  it('leaves ctrl/cmd-clicks to the browser (open in a new tab)', () => {
    const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, ctrlKey: true })

    link('/channels').dispatchEvent(event)

    expect(event.defaultPrevented).toBe(false)
  })
})
