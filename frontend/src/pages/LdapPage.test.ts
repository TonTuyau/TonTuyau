import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { LdapSettings, LdapTest } from '../lib/api/types'
import { fakeApi, goTo } from '../test/fakeApi'
import LdapPage from './LdapPage.svelte'

const settings = (overrides: Partial<LdapSettings> = {}): LdapSettings => ({
  enabled: true,
  url: 'ldap://ldap.example.com:3890',
  startTls: false,
  caCertificate: null,
  bindDn: 'uid=tontuyau,ou=people,dc=example,dc=com',
  bindPasswordSet: true,
  baseDn: 'ou=people,dc=example,dc=com',
  uidAttribute: 'uid',
  usernameAttribute: 'cn',
  usersFilter: '(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)',
  adminsFilter: '(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)',
  passwordAttribute: 'userPassword',
  passwordResetUrl: 'http://ldap.example.com:17170/$username/password',
  clashes: [],
  changedBy: 'admin',
  changedAt: '2026-10-20T08:00:00Z',
  ...overrides,
})

/** The settings route: GET answers `current`, PUT records the body and answers what was sent. */
function ldapRoute(current: LdapSettings, sent: unknown[]) {
  return (_url: URL, init?: RequestInit) => {
    if (init?.method !== 'PUT') return current
    const body = JSON.parse(String(init.body)) as Record<string, unknown>
    sent.push(body)
    return { ...current, ...body, bindPassword: undefined }
  }
}

describe('LdapPage (2.3.0 mockup)', () => {
  beforeEach(() => goTo('/admin/ldap'))

  it('shows the settings saved, the service account’s password never, and the names that clash', async () => {
    fakeApi({ '/api/admin/settings/ldap': settings({ clashes: ['camille'] }) })
    render(LdapPage)

    expect(await screen.findByLabelText('URL')).toHaveValue('ldap://ldap.example.com:3890')
    expect(screen.getByLabelText('Admins filter')).toHaveValue(
      '(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)',
    )
    expect(screen.getByLabelText('Password reset URL (optional)')).toHaveValue(
      'http://ldap.example.com:17170/$username/password',
    )
    const password = screen.getByLabelText('Password')
    expect(password).toHaveValue('')
    expect(password).toHaveAttribute('placeholder', 'Stored: empty keeps it')
    expect(screen.getByRole('status')).toHaveTextContent('a name the directory has too: camille.')
  })

  it('names what’s missing, a URL that isn’t one, a filter without its parentheses; sends nothing then', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    fakeApi({ '/api/admin/settings/ldap': ldapRoute(settings(), sent) })
    render(LdapPage)
    const form = await screen.findByRole('form', { name: 'The directory' })
    const save = within(form).getByRole('button', { name: 'Save' })

    await user.clear(within(form).getByLabelText('Base DN'))
    await user.clear(within(form).getByLabelText('Users filter'))
    await user.click(save)
    expect(within(form).getByRole('alert')).toHaveTextContent('Needed to use the directory: Base DN, Users filter.')

    await user.type(within(form).getByLabelText('Base DN'), 'ou=people,dc=example,dc=com')
    await user.type(within(form).getByLabelText('Users filter'), 'memberof=cn=tontuyau,ou=groups,dc=example,dc=com')
    await user.click(save)
    expect(within(form).getByRole('alert')).toHaveTextContent('A filter is written between parentheses')

    await user.clear(within(form).getByLabelText('Users filter'))
    await user.type(within(form).getByLabelText('Users filter'), '(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)')
    await user.clear(within(form).getByLabelText('URL'))
    await user.type(within(form).getByLabelText('URL'), 'http://ldap.example.com')
    await user.click(save)
    expect(within(form).getByRole('alert')).toHaveTextContent('ldap://host:port or ldaps://host:port')
    expect(sent).toEqual([])
  })

  it('saves; the service account’s password only when one is typed', async () => {
    const user = userEvent.setup()
    const sent: Record<string, unknown>[] = []
    fakeApi({ '/api/admin/settings/ldap': ldapRoute(settings(), sent) })
    render(LdapPage)
    const form = await screen.findByRole('form', { name: 'The directory' })

    await user.click(within(form).getByRole('checkbox', { name: /StartTLS/ }))
    await user.click(within(form).getByRole('button', { name: 'Save' }))
    expect(await within(form).findByRole('status')).toHaveTextContent(
      'Saved: the directory is used from the next login.',
    )
    expect(sent[0]).toMatchObject({ startTls: true })
    expect(sent[0]).not.toHaveProperty('bindPassword')

    await user.type(within(form).getByLabelText('Password'), 's3cret')
    await user.click(within(form).getByRole('button', { name: 'Save' }))
    await vi.waitFor(() => expect(sent[1]).toMatchObject({ bindPassword: 's3cret' }))
    expect(within(form).getByLabelText('Password')).toHaveValue('')
  })

  it('runs each test on its own: one button each, one result each', async () => {
    const user = userEvent.setup()
    const asked: Record<string, unknown>[] = []
    fakeApi({
      '/api/admin/settings/ldap': settings(),
      '/api/admin/settings/ldap/test': (_url: URL, init?: RequestInit) => {
        const body = JSON.parse(String(init?.body)) as Record<string, unknown>
        asked.push(body)
        return body.test === 'admins'
          ? {
              connected: true,
              error: null,
              found: {
                total: 2,
                accounts: [
                  { username: 'eclegv', displayName: 'Eclegv', clash: false },
                  { username: 'tontuyau', displayName: null, clash: false },
                ],
              },
              user: null,
            }
          : { connected: true, error: null, found: null, user: null }
      },
    })
    render(LdapPage)
    const connection = await screen.findByRole('group', { name: 'The connection' })
    const admins = screen.getByRole('group', { name: 'The admins filter' })

    await user.click(within(connection).getByRole('button', { name: 'Test' }))
    expect(await within(connection).findByRole('status')).toHaveTextContent('Connected, the service account bound.')
    expect(within(admins).queryByRole('status')).toBeNull()

    await user.click(within(admins).getByRole('button', { name: 'Test' }))
    const found = await within(admins).findByRole('status')
    expect(found).toHaveTextContent('2 accounts')
    expect(within(found).getByText('eclegv (Eclegv)')).toBeInTheDocument()
    expect(within(found).getByText('tontuyau')).toBeInTheDocument()
    // The connection's result stays as it was.
    expect(within(connection).getByRole('status')).toHaveTextContent('Connected')
    expect(asked.map((body) => body.test)).toEqual(['connection', 'admins'])
    expect(asked[1]).toMatchObject({ adminsFilter: '(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)' })
  })

  it('marks the names that clash, and says how many more there are', async () => {
    const user = userEvent.setup()
    const answer: LdapTest = {
      connected: true,
      error: null,
      found: { total: 102, accounts: [{ username: 'camille', displayName: 'Camille Leroy', clash: true }] },
      user: null,
    }
    fakeApi({ '/api/admin/settings/ldap': settings(), '/api/admin/settings/ldap/test': answer })
    render(LdapPage)
    const users = await screen.findByRole('group', { name: 'The users filter' })

    await user.click(within(users).getByRole('button', { name: 'Test' }))

    const result = await within(users).findByRole('status')
    expect(result).toHaveTextContent('102 accounts')
    expect(within(result).getByText('camille (Camille Leroy) · local name')).toHaveClass('clash')
    expect(result).toHaveTextContent('and 101 more')
  })

  it('looks a name up, no password: the role it would get, or why it’s refused', async () => {
    const user = userEvent.setup()
    const asked: Record<string, unknown>[] = []
    const answer: LdapTest = {
      connected: true,
      error: null,
      found: null,
      user: {
        dn: 'uid=zoe,ou=people,dc=example,dc=com',
        displayName: 'Zoé Bernard',
        user: false,
        admin: false,
        role: null,
        refused: 'neither the users filter nor the admins filter matches it.',
      },
    }
    fakeApi({
      '/api/admin/settings/ldap': settings(),
      '/api/admin/settings/ldap/test': (_url: URL, init?: RequestInit) => {
        asked.push(JSON.parse(String(init?.body)) as Record<string, unknown>)
        return answer
      },
    })
    render(LdapPage)
    const lookup = await screen.findByRole('group', { name: 'A name' })

    await user.click(within(lookup).getByRole('button', { name: 'Test' }))
    expect(within(lookup).getByRole('alert')).toHaveTextContent('Type a name to look up.')
    await user.type(within(lookup).getByRole('textbox', { name: 'A name' }), 'zoe{Enter}')

    const result = await within(lookup).findByRole('status')
    expect(result).toHaveTextContent('uid=zoe,ou=people,dc=example,dc=com')
    expect(result).toHaveTextContent("Admins filter Doesn't match")
    expect(result).toHaveTextContent("Users filter Doesn't match")
    expect(result).toHaveTextContent('Refused: neither the users filter nor the admins filter matches it.')
    expect(asked).toEqual([expect.objectContaining({ test: 'name', username: 'zoe' })])
    expect(asked[0]).not.toHaveProperty('password')
  })

  it('says when it can’t connect', async () => {
    const user = userEvent.setup()
    fakeApi({
      '/api/admin/settings/ldap': settings(),
      '/api/admin/settings/ldap/test': { connected: false, error: 'connection refused.', found: null, user: null },
    })
    render(LdapPage)
    const users = await screen.findByRole('group', { name: 'The users filter' })

    await user.click(within(users).getByRole('button', { name: 'Test' }))

    expect(await within(users).findByRole('status')).toHaveTextContent("Couldn't connect: connection refused.")
  })
})
