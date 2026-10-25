import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { Me } from '../lib/api/types'
import { auth } from '../lib/auth.svelte'
import { router } from '../lib/router/router.svelte'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import MyAccountPage from './MyAccountPage.svelte'

const camille: Me = { username: 'camille', displayName: null, role: 'user', source: 'local' }
const marie: Me = { username: 'marie', displayName: 'Marie Dubois', role: 'user', source: 'ldap' }

/** Logged in as `me`; the password route records what's sent and answers `answer`. */
async function as(me: Me, answer: () => unknown = () => new Response(null, { status: 204 })) {
  const sent: unknown[] = []
  fakeApi({
    '/api/auth/me': me,
    '/api/auth/me/password': (_url: URL, init?: RequestInit) => {
      sent.push(JSON.parse(String(init?.body)))
      return answer()
    },
  })
  await auth.load()
  return sent
}

async function change(current: string, password: string, again = password) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Current password'), current)
  await user.type(screen.getByLabelText('New password'), password)
  await user.type(screen.getByLabelText('Password again'), again)
  await user.click(screen.getByRole('button', { name: 'Change it' }))
}

describe('MyAccountPage (2.3.0)', () => {
  beforeEach(() => goTo('/account'))

  it('says who is logged in, and changes a local account’s password', async () => {
    const sent = await as(camille)
    render(MyAccountPage)

    expect(screen.getByRole('heading', { name: 'My account', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('camille')).toBeInTheDocument()
    expect(screen.getByText('Local')).toBeInTheDocument()
    await change('camille', 'un long mot')

    expect(await screen.findByRole('status')).toHaveTextContent('Changed.')
    expect(sent).toEqual([{ current: 'camille', password: 'un long mot' }])
  })

  it('checks the new password first: long enough, typed the same twice', async () => {
    const sent = await as(camille)
    const user = userEvent.setup()
    render(MyAccountPage)

    await change('camille', 'court')
    expect(screen.getByRole('alert')).toHaveTextContent('At least 8 characters.')
    await user.clear(screen.getByLabelText('New password'))
    await user.clear(screen.getByLabelText('Password again'))
    await user.type(screen.getByLabelText('New password'), 'un long mot')
    await user.type(screen.getByLabelText('Password again'), 'un autre mot')
    await user.click(screen.getByRole('button', { name: 'Change it' }))
    expect(screen.getByRole('alert')).toHaveTextContent("The two passwords aren't the same.")
    expect(sent).toEqual([])
  })

  it('tells a directory account its password is the directory’s, without a form', async () => {
    await as(marie)
    render(MyAccountPage)

    expect(screen.getByText('Marie Dubois')).toBeInTheDocument()
    expect(screen.getByText("Your account is the directory's: its password is changed there.")).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Change it' })).toBeNull()
  })

  it('sends nobody logged in to the login page, then back here', async () => {
    fakeApi({
      '/api/auth/me': problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(MyAccountPage)

    await vi.waitFor(() => expect(router.path + router.search).toBe('/login?next=%2Faccount'))
  })
})
