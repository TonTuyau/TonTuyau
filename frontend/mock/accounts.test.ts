// @vitest-environment node
// The mock's accounts (2.3.0): logging in, the roles, the admin's changes and one's own password, as contract v2.13.
import { describe, expect, it } from 'vitest'
import type { Account, LdapSettings, LdapTest, Me } from '../src/lib/api/types.ts'
import { accountsMockState, handleAccounts, logInAs } from './accounts.ts'

const NOW = Date.parse('2026-10-26T08:00:00Z')

function setup() {
  const state = accountsMockState(NOW)
  const call = <T>(method: string, path: string, body?: unknown) => {
    const answer = handleAccounts(state, method, new URL(path, 'http://mock.local'), body, NOW)
    return { status: answer?.status, body: answer?.body as T }
  }
  const login = (username: string, password: string) =>
    call('POST', '/api/auth/login', { username, password, remember: false }).status
  return { state, call, login }
}

describe('the accounts mock', () => {
  it('logs in an enabled local account with its password; refuses the others', () => {
    const { call, login } = setup()

    expect(login('admin', 'wrong')).toBe(401)
    expect(login('invité', 'invité')).toBe(401) // disabled
    expect(login('marie', 'wrong')).toBe(401) // the directory's, a wrong password
    expect(login('camille', 'camille')).toBe(204)
    expect(call<Me>('GET', '/api/auth/me').body).toEqual({
      username: 'camille',
      displayName: null,
      role: 'user',
      source: 'local',
    })
  })

  it('keeps the admin API to admins: 401 nobody, 403 a user', () => {
    const { state, call } = setup()

    expect(call('GET', '/api/admin/accounts').status).toBe(401)
    logInAs(state, 'user')
    expect(call('GET', '/api/admin/accounts').status).toBe(403)
    logInAs(state, 'admin')
    expect(call<Account[]>('GET', '/api/admin/accounts').body.map((a) => a.username)).toEqual([
      'admin',
      'camille',
      'invité',
      'marie',
    ])
  })

  it('refuses a name taken whatever the case, and leaving no enabled local admin', () => {
    const { state, call } = setup()
    logInAs(state, 'admin')
    const account = (username: string) => ({ username, password: 'x'.repeat(8), role: 'user' })

    expect(call('POST', '/api/admin/accounts', account('Camille')).status).toBe(409)
    expect(call('POST', '/api/admin/accounts', account('léa')).status).toBe(201)
    expect(call('PUT', '/api/admin/accounts/1', { role: 'user' }).status).toBe(409)
    expect(call('DELETE', '/api/admin/accounts/1').status).toBe(409)
    expect(call('PUT', '/api/admin/accounts/4', { role: 'admin' }).status).toBe(409) // a directory account's role
    expect(call('PUT', '/api/admin/accounts/4', { disabled: true }).status).toBe(200)
  })

  it('changes one’s own password with the current one', () => {
    const { call, login } = setup()

    login('camille', 'camille')
    expect(call('PUT', '/api/auth/me/password', { current: 'nope', password: 'nouveau mot' }).status).toBe(400)
    expect(call('PUT', '/api/auth/me/password', { current: 'camille', password: 'nouveau mot' }).status).toBe(204)
    expect(login('camille', 'camille')).toBe(401)
    expect(login('camille', 'nouveau mot')).toBe(204)
  })

  it('says whether the library is for accounts only, and lets an admin set it', () => {
    const { state, call } = setup()

    expect(call('GET', '/api/auth/access').body).toMatchObject({ accountsOnly: false })
    logInAs(state, 'admin')
    expect(call('PUT', '/api/admin/settings/access', {}).status).toBe(400)
    expect(call('PUT', '/api/admin/settings/access', { accountsOnly: true }).body).toMatchObject({
      accountsOnly: true,
      changedBy: 'admin',
    })
    expect(call('GET', '/api/auth/access').body).toMatchObject({ accountsOnly: true })
    logInAs(state, 'user')
    expect(call('PUT', '/api/admin/settings/access', { accountsOnly: false }).status).toBe(403)
  })

  it('logs a directory account in by its filters, and changes its password in the directory', () => {
    const { state, call, login } = setup()

    expect(login('zoe', 'zoe')).toBe(401) // in no group
    expect(login('camille', 'camille')).toBe(204) // the local account, not the directory's
    expect(call<Me>('GET', '/api/auth/me').body.source).toBe('local')
    expect(login('paul', 'paul')).toBe(204)
    expect(call<Me>('GET', '/api/auth/me').body).toMatchObject({ role: 'admin', source: 'ldap' })
    expect(call('PUT', '/api/auth/me/password', { current: 'paul', password: 'nouveau mot' }).status).toBe(204)
    expect(login('paul', 'nouveau mot')).toBe(204)
    state.ldapUp = false
    expect(login('marie', 'marie')).toBe(401)
  })

  it('tests the LDAP settings, each test on its own, and lists the names that clash', () => {
    const { state, call } = setup()
    logInAs(state, 'admin')
    const settings = call<LdapSettings>('GET', '/api/admin/settings/ldap').body
    const test = (body: object) =>
      call<LdapTest>('POST', '/api/admin/settings/ldap/test', { ...settings, ...body }).body

    expect(settings.clashes).toEqual(['camille'])
    expect(test({ test: 'admins' }).found?.accounts.map((found) => found.username)).toEqual(['paul'])
    expect(test({ test: 'users' }).found?.accounts.find((found) => found.username === 'camille')?.clash).toBe(true)
    expect(test({ test: 'name', username: 'marie' }).user).toMatchObject({ role: 'user', refused: null })
    expect(call('GET', '/api/auth/access').body).toMatchObject({ passwordResetUrl: settings.passwordResetUrl })
  })
})
