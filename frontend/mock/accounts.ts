// The mock's accounts (2.3.0): logging in as a local account or as one of a fake LDAP directory's, the roles, the
// admin's accounts page, one's own password, who can watch, and the LDAP page (its settings, its tests), as contract
// v2.13 says. In memory, for the dev server only.
import type {
  Access,
  AccessSettings,
  Account,
  AccountChange,
  LdapSettings,
  LdapSettingsChange,
  LdapTest,
  LdapTestRequest,
  Me,
  NewAccount,
  Role,
} from '../src/lib/api/types.ts'

export interface MockAnswer {
  status: number
  body?: unknown
}

/** A local account's password is kept as typed: a mock, never a real store. */
interface MockAccount extends Account {
  password: string | null
}

/** One of the fake directory's people: its password, and the groups it's in. */
interface DirectoryPerson {
  uid: string
  cn: string
  groups: string[]
}

// As the user's own LLDAP is set (its defaults kept): the users in ou=people, two groups.
const BASE = 'dc=example,dc=com'
const ADMINS = `cn=tontuyau_admin,ou=groups,${BASE}`
const USERS = `cn=tontuyau,ou=groups,${BASE}`

/** The fake directory: an admin, two users, someone in no group, and a name a local account has too. */
export const directory: readonly DirectoryPerson[] = [
  { uid: 'paul', cn: 'Paul Martin', groups: [ADMINS] },
  { uid: 'marie', cn: 'Marie Dubois', groups: [USERS] },
  { uid: 'lucas', cn: 'Lucas Petit', groups: [USERS] },
  { uid: 'zoe', cn: 'Zoé Bernard', groups: [] },
  { uid: 'camille', cn: 'Camille Leroy', groups: [USERS] },
]

export interface AccountsMockState {
  list: MockAccount[]
  nextId: number
  /** The account logged in, or null. */
  session: number | null
  /** Who can watch (`/__mock?private=on`: the logged-in accounts only). */
  access: AccessSettings
  ldap: LdapSettings
  /** The fake directory answers (`/__mock?ldap=down`: it doesn't). */
  ldapUp: boolean
  /** Its people's passwords (the name, at first), as they change them from My account. */
  directoryPasswords: Record<string, string>
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
    access: { accountsOnly: false, changedBy: null, changedAt: null },
    ldap: {
      enabled: true,
      url: 'ldap://ldap.example.com:3890',
      startTls: false,
      caCertificate: null,
      bindDn: `uid=tontuyau,ou=people,${BASE}`,
      bindPasswordSet: true,
      baseDn: `ou=people,${BASE}`,
      uidAttribute: 'uid',
      usernameAttribute: 'cn',
      usersFilter: `(memberof=${USERS})`,
      adminsFilter: `(memberof=${ADMINS})`,
      passwordAttribute: 'userPassword',
      passwordResetUrl: 'http://ldap.example.com:17170/$username/password',
      clashes: [],
      changedBy: 'admin',
      changedAt: ago(5),
    },
    ldapUp: true,
    directoryPasswords: Object.fromEntries(directory.map((person) => [person.uid, person.uid])),
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

/** A DN as compared: lower case, no space after the commas (`ou=groups, dc=example` is the same DN). */
const dnOf = (text: string) =>
  text
    .trim()
    .toLowerCase()
    .replace(/\s*,\s*/g, ',')

/**
 * Whether a filter matches a person: the mock knows `(memberof=<group DN>)`, alone or in `(|…)` / `(&…)`; any other
 * filter matches nobody (a real directory would know it).
 */
function matches(filter: string, person: DirectoryPerson): boolean {
  const text = filter.trim()
  const member = /^\(memberof=([^()]+)\)$/i.exec(text)
  if (member) return person.groups.some((group) => dnOf(group) === dnOf(member[1] ?? ''))
  const both = /^\(([|&])((?:\([^()]+\))+)\)$/.exec(text)
  if (!both) return false
  const parts = both[2]?.match(/\([^()]+\)/g) ?? []
  return both[1] === '|' ? parts.some((part) => matches(part, person)) : parts.every((part) => matches(part, person))
}

/** The role a directory person gets from the filters (the admins filter first); null: refused. */
function roleOf(person: DirectoryPerson, settings: Pick<LdapSettings, 'adminsFilter' | 'usersFilter'>): Role | null {
  if (matches(settings.adminsFilter, person)) return 'admin'
  return matches(settings.usersFilter, person) ? 'user' : null
}

const localHas = (state: AccountsMockState, name: string) =>
  state.list.some((a) => a.source === 'local' && a.username.toLowerCase() === name.toLowerCase())

function login(state: AccountsMockState, body: unknown, now: number): MockAnswer {
  const { username, password } = (body ?? {}) as { username?: string; password?: string }
  const refused: MockAnswer = { status: 401, body: problem(401, 'Login refused', 'Wrong username or password.') }
  const local = state.list.find((a) => a.source === 'local' && a.username === username)
  if (local) {
    if (local.disabled || local.password !== password) return refused
    local.lastLoginAt = new Date(now).toISOString()
    state.session = local.id
    return { status: 204 }
  }
  // The directory: on, answering, the person there with that password, a filter finding it, no local name.
  const person = directory.find((candidate) => candidate.uid === username)
  if (!state.ldap.enabled || !state.ldapUp || !person || !password) return refused
  if (state.directoryPasswords[person.uid] !== password || localHas(state, person.uid)) return refused
  const role = roleOf(person, state.ldap)
  if (role === null) return refused
  let account = state.list.find((a) => a.source === 'ldap' && a.username === person.uid)
  if (!account) {
    account = {
      id: state.nextId++,
      username: person.uid,
      displayName: person.cn,
      role,
      source: 'ldap',
      disabled: false,
      createdAt: new Date(now).toISOString(),
      lastLoginAt: null,
      password: null,
    }
    state.list.push(account)
  }
  if (account.disabled) return refused
  account.role = role
  account.lastLoginAt = new Date(now).toISOString()
  state.session = account.id
  return { status: 204 }
}

/** The local accounts whose name the directory has too. */
const clashes = (state: AccountsMockState) =>
  state.ldap.enabled
    ? state.list
        .filter((a) => a.source === 'local' && directory.some((p) => p.uid === a.username))
        .map((a) => a.username)
    : []

function test(state: AccountsMockState, request: LdapTestRequest): LdapTest {
  const none = { found: null, user: null }
  if (!state.ldapUp) return { connected: false, error: `${request.url}: no answer (timed out after 5 s).`, ...none }
  if (!request.bindPassword && !state.ldap.bindPasswordSet) {
    return { connected: false, error: 'the service account has no password.', ...none }
  }
  const connected = { connected: true, error: null }
  if (request.test === 'admins' || request.test === 'users') {
    const filter = request.test === 'admins' ? request.adminsFilter : request.usersFilter
    const people = directory.filter((person) => matches(filter, person))
    const accounts = people.map((p) => ({ username: p.uid, displayName: p.cn, clash: localHas(state, p.uid) }))
    return { ...connected, found: { total: people.length, accounts }, user: null }
  }
  if (request.test !== 'name') return { ...connected, ...none }
  const person = directory.find((candidate) => candidate.uid === request.username)
  if (!person) {
    const refused = 'no such name under the base DN.'
    return {
      ...connected,
      found: null,
      user: { dn: null, displayName: null, user: false, admin: false, role: null, refused },
    }
  }
  const found = {
    dn: `uid=${person.uid},${request.baseDn}`,
    displayName: person.cn,
    user: matches(request.usersFilter, person),
    admin: matches(request.adminsFilter, person),
  }
  if (localHas(state, person.uid)) {
    return { ...connected, found: null, user: { ...found, role: null, refused: 'a local account has that name.' } }
  }
  const role = roleOf(person, request)
  const refused = role === null ? 'neither the users filter nor the admins filter finds it.' : null
  return { ...connected, found: null, user: { ...found, role, refused } }
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
    // A directory account: its current password checked, then the service account changes it in the directory.
    if (!state.ldapUp) {
      return { status: 503, body: problem(503, "The directory doesn't answer", 'Try again later.') }
    }
    if (state.directoryPasswords[own.username] !== typed) {
      return {
        status: 400,
        body: { ...problem(400, 'Invalid request', 'current: is wrong.'), errors: { current: ['is wrong.'] } },
      }
    }
    state.directoryPasswords[own.username] = password ?? ''
    return { status: 204 }
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
 * Answers /api/auth/*, /api/admin/accounts and /api/admin/settings/access; null for any other path. The admin paths need an admin's session (401
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
  if (path === '/api/auth/access') {
    const passwordResetUrl = state.ldap.enabled ? state.ldap.passwordResetUrl : null
    return { status: 200, body: { accountsOnly: state.access.accountsOnly, passwordResetUrl } satisfies Access }
  }
  if (path === '/api/auth/login' && method === 'POST') return login(state, body, now)
  if (path === '/api/auth/logout' && method === 'POST') {
    state.session = null
    return { status: 204 }
  }
  if (path === '/api/auth/me/password' && method === 'PUT') {
    return current ? changeOwnPassword(state, current, body) : unauthorized
  }

  const ours =
    path === '/api/admin/accounts' || path.startsWith('/api/admin/accounts/') || path.startsWith('/api/admin/settings/')
  if (!ours) return null
  if (!current) return unauthorized
  if (current.role !== 'admin') return { status: 403, body: problem(403, 'Forbidden', 'This is for admins.') }

  if (path === '/api/admin/settings/ldap' && method === 'GET') {
    return { status: 200, body: { ...state.ldap, clashes: clashes(state) } }
  }
  if (path === '/api/admin/settings/ldap' && method === 'PUT') {
    const { bindPassword, ...change } = (body ?? {}) as LdapSettingsChange
    state.ldap = {
      ...state.ldap,
      ...change,
      bindPasswordSet: state.ldap.bindPasswordSet || (bindPassword ?? '') !== '',
      changedBy: current.username,
      changedAt: new Date(now).toISOString(),
    }
    return { status: 200, body: { ...state.ldap, clashes: clashes(state) } }
  }
  if (path === '/api/admin/settings/ldap/test' && method === 'POST') {
    return { status: 200, body: test(state, (body ?? {}) as LdapTestRequest) }
  }
  if (path === '/api/admin/settings/access' && method === 'GET') return { status: 200, body: state.access }
  if (path === '/api/admin/settings/access' && method === 'PUT') {
    const { accountsOnly } = (body ?? {}) as Partial<Access>
    if (typeof accountsOnly !== 'boolean') {
      return { status: 400, body: problem(400, 'Invalid request', 'accountsOnly: is required: true or false.') }
    }
    state.access = { accountsOnly, changedBy: current.username, changedAt: new Date(now).toISOString() }
    return { status: 200, body: state.access }
  }

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
