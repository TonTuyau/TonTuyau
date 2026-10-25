// The mock's accounts (2.3.0): logging in as a local account, the roles, the admin's accounts page and one's own
// password, as contract v2.13 says. In memory, for the dev server only.
import type { Account, AccountChange, Me, NewAccount, Role } from '../src/lib/api/types.ts'

export interface MockAnswer {
  status: number
  body?: unknown
}

/** A local account's password is kept as typed: a mock, never a real store. */
interface MockAccount extends Account {
  password: string | null
}

export interface AccountsMockState {
  list: MockAccount[]
  nextId: number
  /** The account logged in, or null. */
  session: number | null
}

const unauthorized: MockAnswer = { status: 401, body: problem(401, 'Not logged in', 'Nobody is logged in.') }

function problem(status: number, title: string, detail: string) {
  return { title, status, detail }
}

/** An admin, a user, a disabled user, and one of the directory (password = the name, for the local ones). */
export function accountsMockState(now: number = Date.now()): AccountsMockState {
  const ago = (days: number) => new Date(now - days * 86_400_000).toISOString()
  const local = (id: number, username: string, role: Role, days: number, login: number | null): MockAccount => ({
    id,
    username,
    displayName: null,
    role,
    source: 'local',
    disabled: false,
    createdAt: ago(days),
    lastLoginAt: login === null ? null : ago(login),
    password: username,
  })
  return {
    list: [
      local(1, 'admin', 'admin', 40, 0.01),
      local(2, 'camille', 'user', 12, 2),
      { ...local(3, 'invité', 'user', 9, null), disabled: true },
      {
        id: 4,
        username: 'marie',
        displayName: 'Marie Dubois',
        role: 'user',
        source: 'ldap',
        disabled: false,
        createdAt: ago(5),
        lastLoginAt: ago(0.2),
        password: null,
      },
    ],
    nextId: 5,
    session: null,
  }
}

/** The account logged in, as the backend keeps it: gone or disabled since, nobody. */
export function sessionAccount(state: AccountsMockState): Account | null {
  const account = state.list.find((candidate) => candidate.id === state.session)
  return account && !account.disabled ? account : null
}

/** `/__mock`: logs in as the first enabled account of that role, or out. */
export function logInAs(state: AccountsMockState, role: Role | null): void {
  state.session = role === null ? null : (state.list.find((a) => a.role === role && !a.disabled)?.id ?? null)
}

/** An account as the API shows it: never its password. */
const view = (account: MockAccount): Account => ({
  id: account.id,
  username: account.username,
  displayName: account.displayName,
  role: account.role,
  source: account.source,
  disabled: account.disabled,
  createdAt: account.createdAt,
  lastLoginAt: account.lastLoginAt,
})

const me = (account: Account): Me => ({
  username: account.username,
  displayName: account.displayName,
  role: account.role,
  source: account.source,
})

function login(state: AccountsMockState, body: unknown, now: number): MockAnswer {
  const { username, password } = (body ?? {}) as { username?: string; password?: string }
  const account = state.list.find((a) => a.source === 'local' && a.username === username)
  if (!account || account.disabled || account.password !== password) {
    return { status: 401, body: problem(401, 'Login refused', 'Wrong username or password.') }
  }
  account.lastLoginAt = new Date(now).toISOString()
  state.session = account.id
  return { status: 204 }
}

/** The last enabled local admin: it can't be demoted, disabled nor deleted. */
function isLastLocalAdmin(state: AccountsMockState, account: MockAccount): boolean {
  const admins = state.list.filter((a) => a.source === 'local' && a.role === 'admin' && !a.disabled)
  return admins.length <= 1 && admins[0]?.id === account.id
}

/** A local account changes its own password, the current one first. */
function changeOwnPassword(state: AccountsMockState, current: Account, body: unknown): MockAnswer {
  const own = state.list.find((a) => a.id === current.id)
  const { current: typed, password } = (body ?? {}) as { current?: string; password?: string }
  if (!own) return unauthorized
  if (own.source !== 'local') {
    return {
      status: 409,
      body: problem(409, 'Conflict', "Your account is the directory's: its password is changed there."),
    }
  }
  if (own.password !== typed) {
    return {
      status: 400,
      body: { ...problem(400, 'Invalid request', 'current: is wrong.'), errors: { current: ['is wrong.'] } },
    }
  }
  own.password = password ?? ''
  return { status: 204 }
}

function change(
  state: AccountsMockState,
  current: Account,
  account: MockAccount,
  method: string,
  body: unknown,
): MockAnswer {
  const what = (method === 'PUT' ? body : {}) as AccountChange
  const losing = method === 'DELETE' || what.disabled === true || what.role === 'user'
  if (account.id === current.id && (method === 'DELETE' || what.disabled === true)) {
    const detail = method === 'DELETE' ? "Your own account can't be deleted." : "Your own account can't be disabled."
    return { status: 409, body: problem(409, 'Conflict', detail) }
  }
  if (losing && isLastLocalAdmin(state, account)) {
    const detail = `“${account.username}” is the last enabled local admin: it stays, to log in when the directory can't.`
    return { status: 409, body: problem(409, 'Conflict', detail) }
  }
  if (method === 'DELETE') {
    state.list = state.list.filter((a) => a !== account)
    return { status: 204 }
  }
  if (account.source === 'ldap' && ((what.role ?? account.role) !== account.role || what.password != null)) {
    const detail = `“${account.username}” is the directory's: its role comes from its groups, its password is changed there.`
    return { status: 409, body: problem(409, 'Conflict', detail) }
  }
  account.role = what.role === 'admin' || what.role === 'user' ? what.role : account.role
  account.password = what.password ?? account.password
  account.disabled = what.disabled ?? account.disabled
  return { status: 200, body: view(account) }
}

/**
 * Answers /api/auth/* and /api/admin/accounts; null for any other path. The admin paths need an admin's session (401
 * nobody, 403 a user), as the backend's.
 */
export function handleAccounts(
  state: AccountsMockState,
  method: string,
  url: URL,
  body: unknown,
  now: number = Date.now(),
): MockAnswer | null {
  const path = url.pathname.replace(/\/+$/, '')
  const current = sessionAccount(state)
  if (path === '/api/auth/me' && method === 'GET') return current ? { status: 200, body: me(current) } : unauthorized
  if (path === '/api/auth/login' && method === 'POST') return login(state, body, now)
  if (path === '/api/auth/logout' && method === 'POST') {
    state.session = null
    return { status: 204 }
  }
  if (path === '/api/auth/me/password' && method === 'PUT') {
    return current ? changeOwnPassword(state, current, body) : unauthorized
  }

  if (path !== '/api/admin/accounts' && !path.startsWith('/api/admin/accounts/')) return null
  if (!current) return unauthorized
  if (current.role !== 'admin') return { status: 403, body: problem(403, 'Forbidden', 'This is for admins.') }

  if (path === '/api/admin/accounts' && method === 'GET') {
    return { status: 200, body: [...state.list].sort((a, b) => a.username.localeCompare(b.username)).map(view) }
  }
  if (path === '/api/admin/accounts' && method === 'POST') {
    const wanted = (body ?? {}) as NewAccount
    const name = (wanted.username ?? '').trim()
    if (state.list.some((a) => a.username.toLowerCase() === name.toLowerCase())) {
      return { status: 409, body: problem(409, 'Conflict', `An account is already called “${name}”.`) }
    }
    const account: MockAccount = {
      id: state.nextId++,
      username: name,
      displayName: null,
      role: wanted.role === 'admin' ? 'admin' : 'user',
      source: 'local',
      disabled: false,
      createdAt: new Date(now).toISOString(),
      lastLoginAt: null,
      password: wanted.password ?? '',
    }
    state.list.push(account)
    return { status: 201, body: view(account) }
  }
  const id = /^\/api\/admin\/accounts\/(\d+)$/.exec(path)?.[1]
  const account = state.list.find((a) => a.id === Number(id))
  if (id === undefined || !account) return { status: 404, body: problem(404, 'Not found', `No account ${id}.`) }
  if (method === 'PUT' || method === 'DELETE') return change(state, current, account, method, body)
  return { status: 405, body: problem(405, 'Method not allowed', `No ${method} here.`) }
}
