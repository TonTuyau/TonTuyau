import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import App from '../App.svelte'
import { ADMIN_GUIDE_STORAGE_KEY, adminGuide } from '../lib/adminGuide.svelte'
import { apiKey, dashboard, page, run, tasks } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'

const shell = () => document.querySelector('[data-guide]')
const loggedIn = () => ({ username: 'admin' })

/** matchMedia for a viewport `width` px wide, answering max-width queries. */
function viewport(width: number) {
  vi.stubGlobal('matchMedia', (query: string) => {
    const max = /max-width:\s*(\d+)px/.exec(query)
    return {
      matches: !max || width <= Number(max[1]),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }
  })
}

describe('the admin area', () => {
  beforeEach(() => adminGuide.reload())
  afterEach(() => vi.unstubAllGlobals())

  it('sends anyone not logged in to the login page, and back once logged in', async () => {
    let session = false
    const api = fakeApi({
      '/api/auth/me': () => (session ? loggedIn() : problemResponse(401, 'Nobody is logged in.')),
      '/api/auth/login': () => {
        session = true
        return new Response(null, { status: 204 })
      },
      '/api/admin/dashboard': dashboard(),
    })
    goTo('/admin')
    const user = userEvent.setup()
    render(App)

    await screen.findByRole('heading', { name: 'Log in' })
    expect(location.pathname + location.search).toBe('/login?next=%2Fadmin')
    expect(api.requests('/api/admin/dashboard')).toHaveLength(0)

    await user.type(screen.getByLabelText('Username'), 'admin')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    expect(location.pathname).toBe('/admin')
    expect(api.requests('/api/admin/dashboard')).toHaveLength(1)
  })

  it('goes to the login page when the session ended meanwhile (the API answers 401)', async () => {
    fakeApi({
      '/api/auth/me': loggedIn(),
      '/api/admin/dashboard': problemResponse(401, 'Nobody is logged in.'),
    })
    goTo('/admin')
    render(App)

    await screen.findByRole('heading', { name: 'Log in' })
    expect(location.pathname + location.search).toBe('/login?next=%2Fadmin')
  })

  it('has its own menu: the dashboard, and the way back to TonTuyau', async () => {
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/dashboard': dashboard() })
    goTo('/admin')
    render(App)

    const nav = await screen.findByRole('navigation', { name: 'Administration' })
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'Back to TonTuyau' })).toHaveAttribute('href', '/')
    expect(screen.queryByRole('navigation', { name: 'Main navigation' })).toBeNull()
    expect(screen.getByText('Admin')).toBeInTheDocument()
  })

  it('goes to the Tasks page (1.4), which its menu then shows as the current one', async () => {
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
    goTo('/admin/tasks')
    render(App)

    expect(await screen.findByRole('heading', { name: 'Tasks', level: 1 })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administration' })
    expect(within(nav).getByRole('link', { name: 'Tasks' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'Tasks' })).toHaveAttribute('href', '/admin/tasks')
    expect(within(nav).getByRole('link', { name: 'Dashboard' })).not.toHaveAttribute('aria-current')
  })

  it('goes to the API keys (1.5), which its menu then shows as the current one', async () => {
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/api-keys': [apiKey()] })
    goTo('/admin/api-keys')
    render(App)

    expect(await screen.findByRole('heading', { name: 'API keys', level: 1 })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administration' })
    expect(within(nav).getByRole('link', { name: 'API keys' })).toHaveAttribute('aria-current', 'page')
    expect(within(nav).getByRole('link', { name: 'API keys' })).toHaveAttribute('href', '/admin/api-keys')
    expect(within(nav).getByRole('link', { name: 'Tasks' })).not.toHaveAttribute('aria-current')
  })

  it('shows a run’s page as part of the Tasks', async () => {
    const one = run()
    fakeApi({
      '/api/auth/me': loggedIn(),
      [`/api/admin/runs/${one.id}`]: one,
      [`/api/admin/runs/${one.id}/log`]: { lines: [], complete: true },
    })
    goTo(`/admin/runs/${one.id}`)
    render(App)

    expect(await screen.findByRole('heading', { name: 'Incremental sync', level: 1 })).toBeInTheDocument()
    const nav = screen.getByRole('navigation', { name: 'Administration' })
    expect(within(nav).getByRole('link', { name: 'Tasks' })).toHaveAttribute('aria-current', 'page')
  })

  it('collapses its menu to the icons with ☰, and remembers it', async () => {
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/dashboard': dashboard() })
    goTo('/admin')
    const user = userEvent.setup()
    const { unmount } = render(App)

    const collapse = await screen.findByRole('button', { name: 'Collapse the menu' })
    expect(shell()).toHaveAttribute('data-guide', 'full')
    expect(collapse).toHaveAttribute('aria-expanded', 'true')

    await user.click(collapse)
    expect(shell()).toHaveAttribute('data-guide', 'icons')
    expect(screen.getByRole('button', { name: 'Expand the menu' })).toHaveAttribute('aria-expanded', 'false')
    // The links keep their names, only shown on hover.
    expect(screen.getByRole('link', { name: 'Dashboard' })).toHaveAttribute('title', 'Dashboard')
    expect(localStorage.getItem(ADMIN_GUIDE_STORAGE_KEY)).toBe('collapsed')

    unmount()
    adminGuide.reload()
    render(App)
    await screen.findByRole('button', { name: 'Expand the menu' })
    expect(shell()).toHaveAttribute('data-guide', 'icons')
  })

  it('shows the icons only under 900 px; ☰ opens the menu for this visit only', async () => {
    viewport(800)
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/dashboard': dashboard() })
    goTo('/admin')
    const user = userEvent.setup()
    render(App)

    await user.click(await screen.findByRole('button', { name: 'Expand the menu' }))

    expect(shell()).toHaveAttribute('data-guide', 'full')
    expect(localStorage.getItem(ADMIN_GUIDE_STORAGE_KEY)).toBeNull()
  })

  it('is a row of links on phones, without ☰', async () => {
    viewport(375)
    fakeApi({ '/api/auth/me': loggedIn(), '/api/admin/dashboard': dashboard() })
    goTo('/admin')
    render(App)

    await screen.findByRole('heading', { name: 'Dashboard' })
    expect(shell()).toHaveAttribute('data-guide', 'row')
    expect(screen.queryByRole('button', { name: /menu/ })).toBeNull()
    expect(screen.getByText('Back to TonTuyau')).not.toHaveClass('visually-hidden')
  })
})
