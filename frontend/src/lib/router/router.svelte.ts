// A small History API router: the current path/query as runes, navigate(), back/forward via popstate,
// and in-app <a href> clicks turned into navigations (no Link component needed). A navigation starts at the top of
// the page, but for a link marked `data-keep-scroll` (2.1.1: the filter chips, which only change what the page lists).
import { SvelteURLSearchParams } from 'svelte/reactivity'
import { matchRoute, mergeQuery, type Route } from './routes'

/** Paths the server answers itself (API, files, images): links to them are never intercepted. */
const serverPaths = ['/api/', '/media/', '/art/', '/__mock']

class Router {
  path = $state(globalThis.location?.pathname ?? '/')
  search = $state(globalThis.location?.search ?? '')
  route: Route = $derived(matchRoute(this.path))
  query: URLSearchParams = $derived(new SvelteURLSearchParams(this.search))

  /**
   * Goes to an in-app URL. `replace` rewrites the current history entry (used for as-you-type filters); `keepScroll`
   * stays where the page is (a filter of what it lists), instead of going back to its top.
   */
  navigate(to: string, options: { replace?: boolean; keepScroll?: boolean } = {}): void {
    const url = new URL(to, location.href)
    const target = url.pathname + url.search + url.hash
    if (target === location.pathname + location.search + location.hash) {
      return
    }
    if (options.replace) {
      history.replaceState(null, '', target)
    } else {
      history.pushState(null, '', target)
      if (!options.keepScroll) {
        window.scrollTo({ top: 0 })
      }
    }
    this.sync()
  }

  /** Sets one query parameter (null removes it), keeping the others. */
  setQuery(changes: Record<string, string | null>, options: { replace?: boolean } = {}): void {
    this.navigate(`${this.path}${mergeQuery(this.search, changes)}`, options)
  }

  /** Re-reads location (after popstate or a navigation). */
  sync(): void {
    this.path = location.pathname
    this.search = location.search
  }

  /** Starts listening to back/forward and link clicks. Returns the cleanup. */
  start(): () => void {
    const onPopState = () => this.sync()
    const onClick = (event: MouseEvent) => {
      const anchor = interceptable(event)
      if (anchor) {
        event.preventDefault()
        this.navigate(anchor.pathname + anchor.search + anchor.hash, {
          keepScroll: anchor.hasAttribute('data-keep-scroll'),
        })
      }
    }
    window.addEventListener('popstate', onPopState)
    document.addEventListener('click', onClick)
    this.sync()
    return () => {
      window.removeEventListener('popstate', onPopState)
      document.removeEventListener('click', onClick)
    }
  }
}

function interceptable(event: MouseEvent): HTMLAnchorElement | null {
  if (
    event.defaultPrevented ||
    event.button !== 0 ||
    event.metaKey ||
    event.ctrlKey ||
    event.shiftKey ||
    event.altKey
  ) {
    return null
  }
  const anchor = event.target instanceof Element ? event.target.closest('a') : null
  if (!anchor || !anchor.href || anchor.hasAttribute('download')) {
    return null
  }
  if ((anchor.target && anchor.target !== '_self') || anchor.origin !== location.origin) {
    return null
  }
  if (serverPaths.some((prefix) => anchor.pathname.startsWith(prefix))) {
    return null
  }
  // In-page anchors (the skip link) keep the browser's own behaviour.
  if (anchor.hash && anchor.pathname === location.pathname && anchor.search === location.search) {
    return null
  }
  return anchor
}

export const router = new Router()
