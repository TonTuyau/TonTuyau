import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { goTo, problemResponse } from '../test/fakeApi'
import { auth } from '../lib/auth.svelte'
import AccountMenu from './AccountMenu.svelte'

function backend(me: Response) {
  const posts: string[] = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const path = new URL(String(input), 'http://localhost').pathname
    if (init?.method === 'POST') {
      posts.push(path)
      return new Response(null, { status: 204 })
    }
    return me.clone()
  })
  return posts
}

const asAdmin = () =>
  new Response(JSON.stringify({ username: 'admin' }), { headers: { 'content-type': 'application/json' } })

describe('AccountMenu (the top bar)', () => {
  it('shows nothing until the backend said who is logged in', () => {
    render(AccountMenu)

    expect(screen.queryByRole('link')).toBeNull()
    expect(screen.queryByRole('button')).toBeNull()
  })

  it('offers "Log in" when nobody is, back to the current page afterwards', async () => {
    backend(problemResponse(401, 'Nobody is logged in.'))
    goTo('/watch?v=abc')
    await auth.load()
    render(AccountMenu)

    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute(
      'href',
      `/login?next=${encodeURIComponent('/watch?v=abc')}`,
    )
  })

  it('shows the initial of whoever is logged in, and a menu with the name and Log out', async () => {
    const posts = backend(asAdmin())
    await auth.load()
    const user = userEvent.setup()
    render(AccountMenu)

    const trigger = screen.getByRole('button', { name: 'Account: admin' })
    expect(trigger).toHaveTextContent('A')
    expect(screen.queryByRole('link', { name: 'Log in' })).toBeNull()

    await user.click(trigger)
    expect(screen.getByRole('menu', { name: 'admin' })).toHaveTextContent('admin')
    expect(screen.getByRole('menuitem', { name: 'Administration' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    const logout = screen.getByRole('menuitem', { name: 'Log out' })
    expect(logout).toHaveFocus()

    await user.click(logout)
    expect(posts).toEqual(['/api/auth/logout'])
    expect(screen.getByRole('link', { name: 'Log in' })).toBeInTheDocument()
    expect(auth.status).toBe('anonymous')
  })

  it('leads to the admin area', async () => {
    backend(asAdmin())
    await auth.load()
    const user = userEvent.setup()
    render(AccountMenu)

    await user.click(screen.getByRole('button', { name: 'Account: admin' }))

    expect(screen.getByRole('menuitem', { name: 'Administration' })).toHaveAttribute('href', '/admin')
  })

  it('closes its menu with Escape, focus back on the button', async () => {
    backend(asAdmin())
    await auth.load()
    const user = userEvent.setup()
    render(AccountMenu)

    await user.click(screen.getByRole('button', { name: 'Account: admin' }))
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('menu')).toBeNull()
    expect(screen.getByRole('button', { name: 'Account: admin' })).toHaveFocus()
  })
})
