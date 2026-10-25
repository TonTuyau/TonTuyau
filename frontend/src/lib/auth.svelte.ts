// Who is logged in (1.2), with the role (2.3.0: an admin, or a user who watches). Browsing needs no login. The session
// is an HttpOnly cookie the page never sees: the app asks /api/auth/me once at start (a 401 means nobody), and the login
// page and the account menu change it.
import { ApiError, api } from './api/client'
import type { AccountSource, Me, Role } from './api/types'

export type AuthStatus = 'unknown' | 'anonymous' | 'authenticated'

/** A login's outcome: in, refused (any reason: one message), too many attempts (429), or another failure. */
export type LoginResult = 'ok' | 'refused' | 'tooMany' | ApiError

class Auth {
  status = $state<AuthStatus>('unknown')
  username = $state<string | null>(null)
  displayName = $state<string | null>(null)
  role = $state<Role | null>(null)
  source = $state<AccountSource | null>(null)
  #loading: Promise<void> | null = null

  get isAdmin(): boolean {
    return this.role === 'admin'
  }

  /** Asks the backend who is logged in, once. Any failure counts as "nobody". */
  load(): Promise<void> {
    this.#loading ??= api.me().then(
      (me) => this.#set(me),
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
    // Who it is now: the role decides what the menus offer.
    try {
      this.#set(await api.me())
    } catch {
      this.#set({ username, displayName: null, role: 'user', source: 'local' })
    }
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
    this.displayName = null
    this.role = null
    this.source = null
    this.#loading = null
  }

  #set(me: Me | null): void {
    this.username = me?.username ?? null
    this.displayName = me?.displayName ?? null
    // A backend before 2.3.0 says no role: every account was an admin then.
    this.role = me === null ? null : (me.role ?? 'admin')
    this.source = me === null ? null : (me.source ?? 'local')
    this.status = me === null ? 'anonymous' : 'authenticated'
  }
}

export const auth = new Auth()
