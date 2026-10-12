// Who is logged in (1.2). Browsing needs no login. The session is an HttpOnly cookie the page never sees: the app asks
// /api/auth/me once at start (a 401 means nobody), and the login page and the account menu change it.
import { ApiError, api } from './api/client'

export type AuthStatus = 'unknown' | 'anonymous' | 'authenticated'

/** A login's outcome: in, refused (any reason: one message), too many attempts (429), or another failure. */
export type LoginResult = 'ok' | 'refused' | 'tooMany' | ApiError

class Auth {
  status = $state<AuthStatus>('unknown')
  username = $state<string | null>(null)
  #loading: Promise<void> | null = null

  /** Asks the backend who is logged in, once. Any failure counts as "nobody". */
  load(): Promise<void> {
    this.#loading ??= api.me().then(
      (me) => this.#set(me.username),
      () => this.#set(null),
    )
    return this.#loading
  }

  async login(username: string, password: string, remember: boolean): Promise<LoginResult> {
    try {
      await api.login({ username, password, remember })
    } catch (error) {
      if (!(error instanceof ApiError)) {
        throw error
      }
      return error.status === 401 ? 'refused' : error.status === 429 ? 'tooMany' : error
    }
    this.#set(username)
    return 'ok'
  }

  /** Logs out; a failure (network) leaves the session as it was and throws. */
  async logout(): Promise<void> {
    await api.logout()
    this.#set(null)
  }

  /** The backend answered 401 to a logged-in page: the session ended (expired, or logged out in another tab). */
  expired(): void {
    this.#set(null)
  }

  /** Back to "not asked yet" (tests). */
  reset(): void {
    this.status = 'unknown'
    this.username = null
    this.#loading = null
  }

  #set(username: string | null): void {
    this.username = username
    this.status = username === null ? 'anonymous' : 'authenticated'
  }
}

export const auth = new Auth()
