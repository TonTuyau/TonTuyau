import { router } from '../lib/router/router.svelte'

// A fake backend for component tests: maps a path to the JSON the test wants the API to return, and records
// every request so tests can check the exact parameters sent.

type Answer = unknown | Response | ((url: URL) => unknown)

export function problemResponse(status: number, detail: string, title = 'Problem'): Response {
  return new Response(JSON.stringify({ type: 'https://tontuyau/errors/test', title, status, detail }), {
    status,
    headers: { 'content-type': 'application/problem+json' },
  })
}

export function fakeApi(routes: Record<string, Answer>) {
  const calls: URL[] = []
  const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = new URL(input instanceof Request ? input.url : String(input), 'http://localhost')
    calls.push(url)
    const route = routes[url.pathname]
    if (route === undefined) {
      return problemResponse(404, `No fake for ${url.pathname}`, 'Not found')
    }
    const answer = typeof route === 'function' ? (route as (url: URL) => unknown)(url) : route
    if (answer instanceof Response) {
      return answer.clone()
    }
    return new Response(JSON.stringify(answer), { status: 200, headers: { 'content-type': 'application/json' } })
  })
  return {
    spy,
    calls,
    /** The requests made to one path, as URLs (query included). */
    requests: (path: string) => calls.filter((url) => url.pathname === path),
  }
}

/** Puts the app at a URL before rendering (the router reads location). */
export function goTo(url: string): void {
  history.replaceState(null, '', url)
  router.sync()
}
