import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { auth } from '../lib/auth.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { goTo, problemResponse } from '../test/fakeApi'
import LoginPage from './LoginPage.svelte'

/** A backend for the login: `me` says nobody, `login` answers `answer`; every request is kept with its body. */
function backend(answer: () => Response) {
  const requests: { path: string; method: string; body: unknown }[] = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const path = new URL(String(input), 'http://localhost').pathname
    requests.push({ path, method: init?.method ?? 'GET', body: init?.body ? JSON.parse(String(init.body)) : null })
    if (path === '/api/auth/login') {
      return answer()
    }
    return problemResponse(401, 'Nobody is logged in.')
  })
  return requests
}

const loggedIn = () => new Response(null, { status: 204 })
const location = () => globalThis.location.pathname + globalThis.location.search

async function logIn(username: string, password: string, remember = false) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Username'), username)
  await user.type(screen.getByLabelText('Password'), password)
  if (remember) {
    await user.click(screen.getByRole('checkbox', { name: 'Stay logged in' }))
  }
  await user.click(screen.getByRole('button', { name: 'Log in' }))
}

describe('LoginPage', () => {
  afterEach(() => i18n.setLang('en'))

  it('asks for a username and a password, in short words', () => {
    backend(loggedIn)
    render(LoginPage)

    expect(screen.getByRole('heading', { name: 'Log in' })).toBeInTheDocument()
    expect(screen.getByLabelText('Username')).toHaveFocus()
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password')
    expect(screen.getByRole('checkbox', { name: 'Stay logged in' })).not.toBeChecked()
    expect(screen.getByRole('button', { name: 'Log in' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to TonTuyau' })).toHaveAttribute('href', '/')
    expect(document.title).toBe('Log in - TonTuyau')
  })

  it('speaks French too', () => {
    i18n.setLang('fr')
    backend(loggedIn)
    render(LoginPage)

    expect(screen.getByRole('heading', { name: 'Se connecter' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Rester connecté' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Retour à TonTuyau' })).toBeInTheDocument()
  })

  it('logs in with what was typed, then goes back to the page that sent here', async () => {
    const requests = backend(loggedIn)
    goTo('/login?next=%2Fwatch%3Fv%3Dabc')
    render(LoginPage)

    await logIn('admin', 'secret', true)

    expect(requests.filter((r) => r.path === '/api/auth/login')).toEqual([
      { path: '/api/auth/login', method: 'POST', body: { username: 'admin', password: 'secret', remember: true } },
    ])
    expect(location()).toBe('/watch?v=abc')
    expect(auth.status).toBe('authenticated')
    expect(auth.username).toBe('admin')
  })

  it.each(['https://evil.example/', '//evil.example/', '/\\evil.example', 'watch?v=abc', '/login?next=/'])(
    'goes home rather than to %s',
    async (next) => {
      backend(loggedIn)
      goTo(`/login?next=${encodeURIComponent(next)}`)
      render(LoginPage)

      await logIn('admin', 'secret')

      expect(location()).toBe('/')
    },
  )

  it('says the same thing for any refusal, and empties the password', async () => {
    backend(() => problemResponse(401, 'Wrong username or password.'))
    render(LoginPage)

    await logIn('admin', 'wrong')

    expect(screen.getByRole('alert')).toHaveTextContent('Wrong username or password.')
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.getByLabelText('Username')).toHaveValue('admin')
    expect(location()).toBe('/')
    expect(auth.status).toBe('anonymous')
  })

  it('says to wait after too many attempts', async () => {
    backend(() => problemResponse(429, 'Too Many Requests'))
    render(LoginPage)

    await logIn('admin', 'again')

    expect(screen.getByRole('alert')).toHaveTextContent('Too many attempts. Try again in a minute.')
  })

  it("says so when the server can't be reached", async () => {
    backend(() => new Response('Bad Gateway', { status: 502 }))
    render(LoginPage)

    await logIn('admin', 'secret')

    expect(screen.getByRole('alert')).toHaveTextContent("Can't reach the TonTuyau server")
  })

  it('goes straight back when already logged in', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(JSON.stringify({ username: 'admin' }), { headers: { 'content-type': 'application/json' } }),
    )
    goTo('/login?next=%2Fchannels')
    render(LoginPage)

    await vi.waitFor(() => expect(location()).toBe('/channels'))
  })
})
