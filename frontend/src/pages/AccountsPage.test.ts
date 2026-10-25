import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { Account } from '../lib/api/types'
import { auth } from '../lib/auth.svelte'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import AccountsPage from './AccountsPage.svelte'

const account = (overrides: Partial<Account> = {}): Account => ({
  id: 1,
  username: 'admin',
  displayName: null,
  role: 'admin',
  source: 'local',
  disabled: false,
  createdAt: '2026-09-15T08:00:00Z',
  lastLoginAt: '2026-10-25T07:00:00Z',
  ...overrides,
})

const accounts = (): Account[] => [
  account(),
  account({ id: 2, username: 'camille', role: 'user', lastLoginAt: null }),
  account({ id: 4, username: 'marie', displayName: 'Marie Dubois', role: 'user', source: 'ldap' }),
]

/** The table's row of an account. */
const rowOf = (name: string) =>
  within(screen.getByRole('table', { name: 'The accounts' }))
    .getAllByRole('row')
    .find((row) => within(row).queryAllByText(name, { selector: 'b' }).length > 0) as HTMLElement

/** A route answering GET with `get` and recording what else is sent, answered by `other`. */
function route(get: () => unknown, sent: { method: string; body: unknown }[], other: (body: unknown) => unknown) {
  return (_url: URL, init?: RequestInit) => {
    if ((init?.method ?? 'GET') === 'GET') return get()
    const body = init?.body === undefined ? undefined : JSON.parse(String(init.body))
    sent.push({ method: init?.method ?? 'GET', body })
    return other(body)
  }
}

async function asAdmin() {
  fakeApi({ '/api/auth/me': { username: 'admin', displayName: null, role: 'admin', source: 'local' } })
  await auth.load()
}

describe('AccountsPage (2.3.0)', () => {
  beforeEach(async () => {
    goTo('/admin/accounts')
    await asAdmin()
  })

  it('lists the accounts: role, where from, state, last login; the last local admin and one’s own stay', async () => {
    fakeApi({ '/api/admin/accounts': accounts() })
    render(AccountsPage)

    await screen.findByRole('table', { name: 'The accounts' })
    const admin = rowOf('admin')
    expect(admin).toHaveTextContent('you')
    expect(within(admin).getByRole('combobox', { name: 'Role of “admin”' })).toBeDisabled()
    expect(within(admin).queryByRole('button', { name: /Delete/ })).toBeNull()
    expect(within(admin).getByText('Your account')).toBeInTheDocument()

    const camille = rowOf('camille')
    expect(camille).toHaveTextContent('Local')
    expect(camille).toHaveTextContent('Never')
    expect(within(camille).getByRole('combobox', { name: 'Role of “camille”' })).toBeEnabled()

    // A directory account: its role is its groups', no password here.
    const marie = rowOf('marie')
    expect(marie).toHaveTextContent('Marie Dubois')
    expect(marie).toHaveTextContent('Directory')
    expect(marie).toHaveTextContent('from its groups')
    expect(within(marie).queryByRole('combobox')).toBeNull()
    expect(within(marie).queryByRole('button', { name: /New password/ })).toBeNull()
    expect(within(marie).getByRole('button', { name: 'Disable “marie”' })).toBeInTheDocument()
  })

  it('keeps the last local admin even when it isn’t one’s own account', async () => {
    fakeApi({
      '/api/admin/accounts': [
        account({ id: 9, username: 'root' }),
        account({ id: 1, username: 'admin', role: 'user' }),
      ],
    })
    render(AccountsPage)

    await screen.findByRole('table', { name: 'The accounts' })
    expect(within(rowOf('root')).getByText('The last local admin')).toBeInTheDocument()
    expect(within(rowOf('root')).queryByRole('button', { name: 'Disable “root”' })).toBeNull()
  })

  it('checks a new account before sending it: a name, a long enough password typed the same twice', async () => {
    const user = userEvent.setup()
    const sent: { method: string; body: unknown }[] = []
    let list = accounts()
    fakeApi({
      '/api/admin/accounts': route(
        () => list,
        sent,
        (body) => {
          const created = account({ id: 7, ...(body as { username: string; role: 'user' }) })
          list = [...list, created]
          return created
        },
      ),
    })
    render(AccountsPage)
    await screen.findByRole('table', { name: 'The accounts' })
    const form = screen.getByRole('form', { name: 'New account' })

    await user.click(within(form).getByRole('button', { name: 'Create the account' }))
    expect(within(form).getByRole('alert')).toHaveTextContent('Give it a name first.')
    await user.type(within(form).getByLabelText('Name'), 'léa')
    await user.type(within(form).getByLabelText('Password'), 'short')
    await user.click(within(form).getByRole('button', { name: 'Create the account' }))
    expect(within(form).getByRole('alert')).toHaveTextContent('At least 8 characters.')
    await user.clear(within(form).getByLabelText('Password'))
    await user.type(within(form).getByLabelText('Password'), 'un long mot')
    await user.type(within(form).getByLabelText('Password again'), 'un autre mot')
    await user.click(within(form).getByRole('button', { name: 'Create the account' }))
    expect(within(form).getByRole('alert')).toHaveTextContent("The two passwords aren't the same.")
    expect(sent).toEqual([])

    await user.clear(within(form).getByLabelText('Password again'))
    await user.type(within(form).getByLabelText('Password again'), 'un long mot')
    await user.selectOptions(within(form).getByLabelText('Role'), 'Admin')
    await user.click(within(form).getByRole('button', { name: 'Create the account' }))

    expect(await within(form).findByRole('status')).toHaveTextContent('“léa” can log in now.')
    expect(sent).toEqual([{ method: 'POST', body: { username: 'léa', password: 'un long mot', role: 'admin' } }])
    expect(rowOf('léa')).toBeInTheDocument()
  })

  it('says why a name is refused (taken, whatever the case)', async () => {
    const user = userEvent.setup()
    fakeApi({
      '/api/admin/accounts': (_url: URL, init?: RequestInit) =>
        init?.method === 'POST' ? problemResponse(409, 'An account is already called “Camille”.') : accounts(),
    })
    render(AccountsPage)
    await screen.findByRole('table', { name: 'The accounts' })
    const form = screen.getByRole('form', { name: 'New account' })

    await user.type(within(form).getByLabelText('Name'), 'Camille')
    await user.type(within(form).getByLabelText('Password'), 'un long mot')
    await user.type(within(form).getByLabelText('Password again'), 'un long mot')
    await user.click(within(form).getByRole('button', { name: 'Create the account' }))

    expect(await within(form).findByRole('alert')).toHaveTextContent('An account is already called “Camille”.')
  })

  it('changes a role, sets a new password, disables, deletes once asked', async () => {
    const user = userEvent.setup()
    const sent: { method: string; body: unknown }[] = []
    fakeApi({
      '/api/admin/accounts': accounts(),
      '/api/admin/accounts/2': route(accounts, sent, () => account({ id: 2, username: 'camille' })),
    })
    render(AccountsPage)
    await screen.findByRole('table', { name: 'The accounts' })

    await user.selectOptions(screen.getByRole('combobox', { name: 'Role of “camille”' }), 'Admin')
    await user.click(screen.getByRole('button', { name: 'New password for “camille”' }))
    const reset = screen.getByRole('form', { name: 'New password for “camille”' })
    await user.type(within(reset).getByLabelText('New password'), 'nouveau mot')
    await user.type(within(reset).getByLabelText('Password again'), 'nouveau mot')
    await user.click(within(reset).getByRole('button', { name: 'Set the password' }))
    await user.click(await screen.findByRole('button', { name: 'Disable “camille”' }))
    await user.click(await screen.findByRole('button', { name: 'Delete “camille”' }))
    const confirm = screen.getByRole('group', { name: 'Delete “camille”' })
    expect(confirm).toHaveTextContent("It can't log in any more")
    await user.click(within(confirm).getByRole('button', { name: 'Delete' }))

    await vi.waitFor(() =>
      expect(sent).toEqual([
        { method: 'PUT', body: { role: 'admin' } },
        { method: 'PUT', body: { password: 'nouveau mot' } },
        { method: 'PUT', body: { disabled: true } },
        { method: 'DELETE', body: undefined },
      ]),
    )
  })
})
