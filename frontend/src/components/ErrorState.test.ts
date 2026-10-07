import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { ApiError } from '../lib/api/client'
import { i18n } from '../lib/i18n/i18n.svelte'
import ErrorState from './ErrorState.svelte'

const problem = (status: number, detail: string) => new ApiError({ title: 'Problem', status, detail }, true)
const network = () => new ApiError({ title: 'Network error', status: 0 })

describe('ErrorState', () => {
  afterEach(() => {
    i18n.setLang('en')
    vi.unstubAllGlobals()
  })

  it('explains a server error in words, keeps the ProblemDetails as a technical line, and retries', async () => {
    const onretry = vi.fn()
    render(ErrorState, { error: problem(500, 'Elasticsearch is not reachable.'), onretry })

    const alert = screen.getByRole('alert')
    expect(alert).toHaveTextContent('The server ran into a problem')
    expect(alert).toHaveTextContent("If it keeps happening, the server's log has the details.")
    expect(alert).toHaveTextContent('Error 500 · Elasticsearch is not reachable.')
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(onretry).toHaveBeenCalledOnce()
  })

  it('has no Retry for a 400: the same request gets the same answer', () => {
    render(ErrorState, { error: problem(400, 'q: must be at most 200 characters.'), onretry: vi.fn() })

    expect(screen.getByRole('alert')).toHaveTextContent("This request isn't valid")
    expect(screen.getByRole('alert')).toHaveTextContent('Error 400 · q: must be at most 200 characters.')
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument()
  })

  it('says the server is unreachable for a bare 502 from the proxy, without a made-up detail', () => {
    render(ErrorState, { error: new ApiError({ title: 'Bad Gateway', status: 502 }), onretry: vi.fn() })

    expect(screen.getByRole('alert')).toHaveTextContent("Can't reach the TonTuyau server")
    expect(screen.getByRole('alert')).toHaveTextContent('Error 502')
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('says the browser is offline, and retries by itself when it is back online', () => {
    vi.stubGlobal('navigator', { ...navigator, onLine: false })
    const onretry = vi.fn()
    render(ErrorState, { error: network(), onretry })

    expect(screen.getByRole('alert')).toHaveTextContent("You're offline")
    window.dispatchEvent(new Event('online'))
    expect(onretry).toHaveBeenCalledOnce()
  })

  it('does not retry by itself for a server error', () => {
    const onretry = vi.fn()
    render(ErrorState, { error: problem(500, 'Boom'), onretry })

    window.dispatchEvent(new Event('online'))
    expect(onretry).not.toHaveBeenCalled()
  })

  it('speaks French', () => {
    i18n.setLang('fr')
    render(ErrorState, { error: network() })

    expect(screen.getByRole('alert')).toHaveTextContent('Impossible de joindre le serveur TonTuyau')
  })
})
