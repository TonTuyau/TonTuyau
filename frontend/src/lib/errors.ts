// What an API failure means for the person looking at the page. Only the HTTP status and whether the answer was
// the backend's own ProblemDetails decide it: the backend always answers errors with ProblemDetails (contract), so
// a bare 5xx (a text page, or nothing) comes from something in between: the dev server's proxy or a reverse proxy
// whose backend is down.
import type { ApiError } from './api/client'
import type { TextKey } from './i18n/types'

export type ErrorKind = 'offline' | 'unreachable' | 'invalid' | 'notFound' | 'server' | 'other'

export function errorKind(error: ApiError, online: boolean = globalThis.navigator?.onLine ?? true): ErrorKind {
  if (error.isNetworkError) {
    return online ? 'unreachable' : 'offline'
  }
  if (!error.fromServer && error.status >= 500) {
    return 'unreachable'
  }
  if (error.status === 400) {
    return 'invalid'
  }
  if (error.status === 404) {
    return 'notFound'
  }
  return error.status >= 500 ? 'server' : 'other'
}

/** Retrying can help: the server or the network may be back. A 400 or a 404 will answer the same. */
export function isRetryable(kind: ErrorKind): boolean {
  return kind !== 'invalid' && kind !== 'notFound'
}

/** The connection itself failed: worth retrying by itself as soon as the browser says it's back online. */
export function isConnectionProblem(kind: ErrorKind): boolean {
  return kind === 'offline' || kind === 'unreachable'
}

export const errorTexts: Record<ErrorKind, { title: TextKey; hint: TextKey }> = {
  offline: { title: 'error.offline', hint: 'error.offlineHint' },
  unreachable: { title: 'error.unreachable', hint: 'error.unreachableHint' },
  invalid: { title: 'error.invalid', hint: 'error.invalidHint' },
  notFound: { title: 'error.notFound', hint: 'error.notFoundHint' },
  server: { title: 'error.server', hint: 'error.serverHint' },
  other: { title: 'error.other', hint: 'error.otherHint' },
}

/**
 * A page about one video or channel whose id the API doesn't know (404) or refuses as malformed (400: the id is the
 * only thing these endpoints take): the not-found state with words for it, or null for any other failure.
 */
export function missingResource(
  error: ApiError,
  resource: 'video' | 'channel',
): { title: TextKey; hint: TextKey } | null {
  const page = resource === 'video' ? 'watch' : 'channel'
  if (error.fromServer && error.status === 404) {
    return { title: `${page}.notFound`, hint: `${page}.notFoundHint` }
  }
  if (error.fromServer && error.status === 400) {
    return { title: `${page}.invalidId`, hint: `${page}.invalidIdHint` }
  }
  return null
}
