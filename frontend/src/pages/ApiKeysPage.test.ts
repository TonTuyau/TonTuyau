import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { auth } from '../lib/auth.svelte'
import { apiKey } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import ApiKeysPage from './ApiKeysPage.svelte'

const SECRET = 'tt_k7QmN2vXr9TbLp4WcE8hYz1sDf6GjA3uQw5ZxCv0'

/** The table's rows: each cell's text. */
const rows = () =>
  within(screen.getByRole('table', { name: 'Keys' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) =>
      within(row)
        .getAllByRole('cell')
        .map((cell) => cell.textContent?.trim()),
    )

/** A fake /api/admin/api-keys answering GET with `keys` and POST with `onPost`'s answer. */
function keysRoute(keys: () => unknown, onPost?: (body: { name: string; kind: string }) => unknown) {
  return (_url: URL, init?: RequestInit) =>
    init?.method === 'POST' ? onPost?.(JSON.parse(String(init.body)) as { name: string; kind: string }) : keys()
}

describe('ApiKeysPage', () => {
  beforeEach(() => goTo('/admin/api-keys'))

  it('lists the keys: kind, the secret’s first characters, state, who made them, their last use; never a secret', async () => {
    fakeApi({
      '/api/admin/api-keys': [
        apiKey(),
        apiKey({ id: 3, name: 'Old phone', prefix: 'tt_Lr2e', enabled: false, lastUsedAt: '2026-09-30T10:00:00Z' }),
      ],
    })
    render(ApiKeysPage)

    await screen.findByRole('table', { name: 'Keys' })
    const [first, second] = rows()
    expect(first?.slice(0, 4)).toEqual(['Firefox, laptop', 'Browser extension', 'tt_k7Qm…', 'Enabled'])
    expect(first?.[4]).toMatch(/ · claire$/)
    expect(first?.[5]).toBe('Never')
    expect(second?.slice(0, 4)).toEqual(['Old phone', 'Browser extension', 'tt_Lr2e…', 'Disabled'])
    expect(second?.[5]).not.toBe('Never')
    expect(screen.getByRole('button', { name: 'Disable “Firefox, laptop”' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Enable “Old phone”' })).toBeInTheDocument()
  })

  it('says when there is no key yet', async () => {
    fakeApi({ '/api/admin/api-keys': [] })
    render(ApiKeysPage)

    expect(await screen.findByText('No keys yet.')).toBeInTheDocument()
  })

  it('creates a key with its name and kind, shows its secret once, then lists it', async () => {
    const user = userEvent.setup()
    let keys = [apiKey({ id: 3, name: 'Old phone' })]
    const sent: { name: string; kind: string }[] = []
    fakeApi({
      '/api/admin/api-keys': keysRoute(
        () => keys,
        (body) => {
          sent.push(body)
          const key = apiKey({ id: 8, name: body.name, prefix: SECRET.slice(0, 7) })
          keys = [key, ...keys]
          return new Response(JSON.stringify({ key, secret: SECRET }), {
            status: 201,
            headers: { 'content-type': 'application/json' },
          })
        },
      ),
    })
    render(ApiKeysPage)
    await screen.findByRole('table', { name: 'Keys' })

    await user.type(screen.getByLabelText('Name'), '  Chrome, work ')
    expect(screen.getByLabelText('Kind')).toHaveValue('browserExtension')
    await user.click(screen.getByRole('button', { name: 'Create the key' }))

    const ready = await screen.findByRole('status')
    expect(within(ready).getByText(/“Chrome, work” is ready/)).toBeInTheDocument()
    expect(within(ready).getByText(SECRET)).toBeInTheDocument()
    expect(within(ready).getByRole('button', { name: 'Copy the key' })).toBeInTheDocument()
    expect(sent).toEqual([{ name: 'Chrome, work', kind: 'browserExtension' }])
    expect(screen.getByLabelText('Name')).toHaveValue('')
    await vi.waitFor(() => expect(rows().map((row) => row[0])).toEqual(['Chrome, work', 'Old phone']))

    await user.click(within(ready).getByRole('button', { name: 'Done' }))
    expect(screen.queryByText(SECRET)).not.toBeInTheDocument()
  })

  it('asks for a name before sending anything', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    fakeApi({
      '/api/admin/api-keys': keysRoute(
        () => [],
        (body) => sent.push(body),
      ),
    })
    render(ApiKeysPage)
    await screen.findByText('No keys yet.')

    await user.type(screen.getByLabelText('Name'), '   ')
    await user.click(screen.getByRole('button', { name: 'Create the key' }))

    expect(screen.getByRole('alert')).toHaveTextContent('Give it a name first.')
    expect(sent).toEqual([])
  })

  it('says why a key wasn’t created (a name taken)', async () => {
    const user = userEvent.setup()
    fakeApi({
      '/api/admin/api-keys': keysRoute(
        () => [apiKey({ name: 'Firefox' })],
        () => problemResponse(409, 'A key is already called “firefox”.'),
      ),
    })
    render(ApiKeysPage)
    await screen.findByRole('table', { name: 'Keys' })

    await user.type(screen.getByLabelText('Name'), 'firefox')
    await user.click(screen.getByRole('button', { name: 'Create the key' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The key wasn’t created: A key is already called “firefox”.'.replace('’', "'"),
    )
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('disables a key, then shows it disabled', async () => {
    const user = userEvent.setup()
    let enabled = true
    const api = fakeApi({
      '/api/admin/api-keys': () => [apiKey({ enabled })],
      '/api/admin/api-keys/7/disable': (_url: URL, init?: RequestInit) => {
        expect(init?.method).toBe('POST')
        enabled = false
        return apiKey({ enabled })
      },
    })
    render(ApiKeysPage)

    await user.click(await screen.findByRole('button', { name: 'Disable “Firefox, laptop”' }))

    expect(api.requests('/api/admin/api-keys/7/disable')).toHaveLength(1)
    expect(await screen.findByRole('button', { name: 'Enable “Firefox, laptop”' })).toBeInTheDocument()
    expect(rows()[0]?.[3]).toBe('Disabled')
  })

  it('asks before deleting a key; “Keep it” leaves it', async () => {
    const user = userEvent.setup()
    let keys = [apiKey()]
    const api = fakeApi({
      '/api/admin/api-keys': () => keys,
      '/api/admin/api-keys/7': (_url: URL, init?: RequestInit) => {
        expect(init?.method).toBe('DELETE')
        keys = []
        return new Response(null, { status: 204 })
      },
    })
    render(ApiKeysPage)

    await user.click(await screen.findByRole('button', { name: 'Delete “Firefox, laptop”' }))
    const ask = screen.getByRole('group', { name: 'Delete “Firefox, laptop”' })
    expect(ask).toHaveTextContent('A program using it is refused from now on')
    await user.click(within(ask).getByRole('button', { name: 'Keep it' }))
    expect(screen.queryByRole('group')).not.toBeInTheDocument()
    expect(api.requests('/api/admin/api-keys/7')).toHaveLength(0)

    await user.click(screen.getByRole('button', { name: 'Delete “Firefox, laptop”' }))
    await user.click(within(screen.getByRole('group')).getByRole('button', { name: 'Delete' }))

    expect(api.requests('/api/admin/api-keys/7')).toHaveLength(1)
    expect(await screen.findByText('No keys yet.')).toBeInTheDocument()
  })

  it('says why an action was refused', async () => {
    const user = userEvent.setup()
    fakeApi({
      '/api/admin/api-keys': [apiKey()],
      '/api/admin/api-keys/7/disable': problemResponse(404, 'There’s no API key with the id 7.'),
    })
    render(ApiKeysPage)

    await user.click(await screen.findByRole('button', { name: 'Disable “Firefox, laptop”' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('There’s no API key with the id 7.')
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    fakeApi({
      '/api/auth/me': { username: 'admin' },
      '/api/admin/api-keys': problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(ApiKeysPage)

    await vi.waitFor(() => expect(auth.status).toBe('anonymous'))
  })
})
