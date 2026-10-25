import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { YouTubeCookie, YouTubeOverview, YouTubeSettings, YtdlpState, YtdlpVersions } from '../lib/api/types'
import { auth } from '../lib/auth.svelte'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import YouTubePage from './YouTubePage.svelte'

const cookie = (overrides: Partial<YouTubeCookie> = {}): YouTubeCookie => ({
  receivedAt: '2026-10-14T08:30:00Z',
  receivedBy: 'Firefox, laptop (API key)',
  state: 'works',
  checkedAt: '2026-10-14T08:30:05Z',
  reason: null,
  ...overrides,
})

const overview = (overrides: Partial<YouTubeOverview> = {}): YouTubeOverview => ({
  cookie: cookie(),
  ytdlp: '2026.09.30',
  deno: '2.9.7',
  ...overrides,
})

const ytdlp = (overrides: Partial<YtdlpState> = {}): YtdlpState => ({
  choice: { kind: 'latest', version: null, chosenBy: null, chosenAt: null },
  running: '2026.09.30',
  bundled: false,
  waiting: null,
  checkedAt: '2026-10-14T09:00:00Z',
  error: null,
  ...overrides,
})

const versions: YtdlpVersions = {
  official: [
    { version: '2026.09.30', publishedAt: '2026-09-30T20:00:00Z' },
    { version: '2026.08.19', publishedAt: '2026-08-19T23:48:43Z' },
  ],
  nightly: [
    { version: '2026.10.10.233141', publishedAt: '2026-10-10T23:35:05Z' },
    { version: '2026.09.27.232945', publishedAt: '2026-09-27T23:33:16Z' },
  ],
}

/** The page's API, every part answered unless given. */
function routes(given: Record<string, unknown> = {}) {
  return fakeApi({
    '/api/admin/youtube': overview(),
    '/api/admin/youtube/ytdlp': ytdlp(),
    '/api/admin/youtube/ytdlp/versions': versions,
    '/api/admin/youtube/settings': {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    },
    ...given,
  })
}

describe('YouTubePage', () => {
  beforeEach(() => goTo('/admin/youtube'))

  it('reads no comments by default; the top N or all once saved, with who changed it', async () => {
    const user = userEvent.setup()
    let saved: YouTubeSettings = {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    }
    const sent: unknown[] = []
    routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          const body = JSON.parse(String(init.body)) as Pick<YouTubeSettings, 'comments' | 'commentsMax'>
          sent.push(body)
          saved = { ...saved, ...body, changedBy: 'admin', changedAt: new Date().toISOString() }
        }
        return saved
      },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Reading YouTube' })
    expect(await within(card).findByRole('radio', { name: /^Off/ })).toBeChecked()
    expect(within(card).getByText(/^Off: no comments are read\./)).toBeInTheDocument()
    const max = within(card).getByRole('spinbutton', { name: 'How many comments, at most' })
    expect(max).toBeDisabled()

    await user.click(within(card).getByRole('radio', { name: /^The top ones/ }))
    await user.clear(max)
    await user.type(max, '2000')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await within(card).findByText(/^The top 2,000 are read\. .* · changed by admin/)).toBeInTheDocument()

    await user.click(within(card).getByRole('radio', { name: /^All/ }))
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await within(card).findByText(/^All the comments are read\./)).toBeInTheDocument()
    expect(sent).toEqual([
      { comments: 'top', commentsMax: 2000, pauseSeconds: 10, holdMinutes: 60, syncEveryMinutes: 360 },
      { comments: 'all', commentsMax: 2000, pauseSeconds: 10, holdMinutes: 60, syncEveryMinutes: 360 },
    ])
  })

  it('goes easy on YouTube: the pause between videos and how long a refused refresh waits, checked first', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    let saved: YouTubeSettings = {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    }
    routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          const body = JSON.parse(String(init.body)) as YouTubeSettings
          sent.push(body)
          saved = { ...saved, ...body, changedBy: 'admin', changedAt: new Date().toISOString() }
        }
        return saved
      },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Reading YouTube' })
    const easy = await within(card).findByRole('group', { name: 'Going easy on YouTube' })
    expect(
      within(card).getByText(/A pause of about 10 s between videos; on hold 60 min after a refusal\./),
    ).toBeInTheDocument()
    const pause = within(easy).getByRole('spinbutton', { name: /^Pause between videos/ })
    const hold = within(easy).getByRole('spinbutton', { name: /^After a bot check, hold the refresh for/ })

    await user.clear(hold)
    await user.type(hold, '0')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(within(easy).getByRole('alert')).toHaveTextContent('A whole number of minutes, 1 or more.')
    expect(sent).toEqual([])

    await user.clear(hold)
    await user.type(hold, '90')
    await user.clear(pause)
    await user.type(pause, '0')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await within(card).findByText(/A pause of about 0 s between videos; on hold 90 min/)).toBeInTheDocument()
    expect(sent).toEqual([
      { comments: 'off', commentsMax: 500, pauseSeconds: 0, holdMinutes: 90, syncEveryMinutes: 360 },
    ])
  })

  it('sets the download format and the tabs a channel added gets, checked first', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    let saved: YouTubeSettings = {
      comments: 'top',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    }
    routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          const body = JSON.parse(String(init.body)) as Partial<YouTubeSettings>
          sent.push(body)
          saved = { ...saved, ...body }
        }
        return saved
      },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Downloading' })
    expect(card).toHaveTextContent(
      'Downloads ask for bestvideo+bestaudio; a channel added gets: Videos · Shorts · Streams.',
    )
    const format = within(card).getByRole('textbox', { name: /^Format/ })
    await user.clear(format)
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(within(card).getByRole('alert')).toHaveTextContent("yt-dlp's format, on one line")
    expect(sent).toEqual([])

    await user.type(format, 'bestvideo[[height<=1080]+bestaudio/best')
    await user.click(within(card).getByRole('checkbox', { name: 'Shorts' }))
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(
      await within(card).findByText(
        /Downloads ask for bestvideo\[height<=1080\]\+bestaudio\/best; a channel added gets: Videos · Streams\./,
      ),
    ).toBeInTheDocument()
    expect(sent).toEqual([
      {
        comments: 'top',
        downloadFormat: 'bestvideo[height<=1080]+bestaudio/best',
        defaultTabs: { videos: true, shorts: false, streams: true },
        // 2.2.0: the disk space settings, as they were; 2.2.1: the retry without the cookie, as it was.
        minFreeGb: 50,
        holdBelowMinFree: true,
        retryWithoutCookie: true,
      },
    ])
  })

  it('turns off trying a download again without the YouTube cookie', async () => {
    const user = userEvent.setup()
    const sent: Partial<YouTubeSettings>[] = []
    let saved: YouTubeSettings = {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    }
    routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          const body = JSON.parse(String(init.body)) as Partial<YouTubeSettings>
          sent.push(body)
          saved = { ...saved, ...body }
        }
        return saved
      },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Downloading' })
    const retry = within(card).getByRole('checkbox', {
      name: "If the format isn't available with the YouTube cookie, try again without it",
    })
    expect(retry).toBeChecked()
    await user.click(retry)
    await user.click(within(card).getByRole('button', { name: 'Save' }))

    await vi.waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0]?.retryWithoutCookie).toBe(false)
    await vi.waitFor(() => expect(retry).not.toBeChecked())
  })

  it('runs the YouTube sync every few minutes or hours, or on demand only at 0, checked first', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    let saved: YouTubeSettings = {
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 360,
      downloadFormat: 'bestvideo+bestaudio',
      defaultTabs: { videos: true, shorts: true, streams: true },
      minFreeGb: 50,
      holdBelowMinFree: true,
      retryWithoutCookie: true,
      changedBy: null,
      changedAt: null,
    }
    routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          const body = JSON.parse(String(init.body)) as YouTubeSettings
          sent.push(body)
          saved = { ...saved, ...body, changedBy: 'admin', changedAt: new Date().toISOString() }
        }
        return saved
      },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Reading YouTube' })
    const sync = await within(card).findByRole('group', { name: 'The YouTube sync' })
    expect(within(card).getByText(/The YouTube sync every 6 h\./)).toBeInTheDocument()
    const every = within(sync).getByRole('spinbutton', { name: /^Run it by itself every/ })

    const unit = within(sync).getByRole('combobox', { name: 'Unit' })
    expect(every).toHaveValue(6)
    expect(unit).toHaveValue('hours')

    await user.clear(every)
    await user.type(every, '169')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(within(sync).getByRole('alert')).toHaveTextContent('A whole number, up to a week')
    expect(sent).toEqual([])

    // In minutes (2.1.0), for whoever wants it eager.
    await user.clear(every)
    await user.type(every, '15')
    await user.selectOptions(unit, 'minutes')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await within(card).findByText(/The YouTube sync every 15 min\./)).toBeInTheDocument()
    expect(sent.at(-1)).toMatchObject({ syncEveryMinutes: 15 })
    expect(unit).toHaveValue('minutes')

    await user.clear(every)
    await user.type(every, '0')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await within(card).findByText(/The YouTube sync on demand only\./)).toBeInTheDocument()
    expect(sent.at(-1)).toEqual({
      comments: 'off',
      commentsMax: 500,
      pauseSeconds: 10,
      holdMinutes: 60,
      syncEveryMinutes: 0,
    })
  })

  it('refuses a number below 1 before asking, and shows what is saved after a refusal', async () => {
    const user = userEvent.setup()
    const api = routes({
      '/api/admin/youtube/settings': (_url: URL, init?: RequestInit) =>
        init?.method === 'PUT'
          ? problemResponse(400, 'comments is required.', 'Invalid request')
          : {
              comments: 'off',
              commentsMax: 500,
              pauseSeconds: 10,
              holdMinutes: 60,
              syncEveryMinutes: 360,
              downloadFormat: 'bestvideo+bestaudio',
              defaultTabs: { videos: true, shorts: true, streams: true },
              minFreeGb: 50,
              holdBelowMinFree: true,
              retryWithoutCookie: true,
              changedBy: null,
              changedAt: null,
            },
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'Reading YouTube' })
    await user.click(await within(card).findByRole('radio', { name: /^The top ones/ }))
    const max = within(card).getByRole('spinbutton', { name: 'How many comments, at most' })
    await user.clear(max)
    await user.type(max, '0')
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(within(card).getByRole('alert')).toHaveTextContent('A whole number, 1 or more.')
    const puts = () => api.calls.filter((url) => url.pathname === '/api/admin/youtube/settings').length
    const before = puts()

    await user.click(within(card).getByRole('radio', { name: /^All/ }))
    await user.click(within(card).getByRole('button', { name: 'Save' }))
    expect(await screen.findByText(/comments is required/)).toBeInTheDocument()
    expect(puts()).toBeGreaterThan(before)
    expect(within(card).getByRole('radio', { name: /^Off/ })).toBeChecked()
  })

  it('shows the cookie, who sent it, the tools, and which yt-dlp runs and was chosen', async () => {
    routes()
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'YouTube cookie' })
    expect(within(card).getByText('Works')).toBeInTheDocument()
    expect(within(card).getByText(/from Firefox, laptop \(API key\)$/)).toBeInTheDocument()
    const tools = screen.getByRole('region', { name: 'Tools' })
    expect(within(tools).getByText('2026.09.30')).toBeInTheDocument()
    expect(within(tools).getByText('2.9.7')).toBeInTheDocument()
    const box = screen.getByRole('region', { name: 'yt-dlp' })
    expect(within(box).getByText('Latest official release, kept up to date', { selector: 'dd' })).toBeInTheDocument()
    expect(within(box).getByText('the default')).toBeInTheDocument()
    expect(await within(box).findByText(/Today: 2026\.09\.30\./)).toBeInTheDocument()
    expect(within(box).getByRole('radio', { name: /Latest official release/ })).toBeChecked()
  })

  it('says there is no cookie, and how to send one', async () => {
    routes({ '/api/admin/youtube': overview({ cookie: null }) })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'YouTube cookie' })
    expect(within(card).getByText('None')).toBeInTheDocument()
    expect(within(card).getByText(/Send one from TonTuyau's browser extension/)).toBeInTheDocument()
    // Nothing to check nor remove: its help only.
    expect(
      within(card)
        .getAllByRole('button')
        .map((button) => button.getAttribute('aria-label')),
    ).toEqual(['Help: YouTube cookie'])
  })

  it('says why YouTube refused the cookie', async () => {
    routes({
      '/api/admin/youtube': overview({ cookie: cookie({ state: 'refused', reason: 'The playlist does not exist.' }) }),
    })
    render(YouTubePage)

    const card = await screen.findByRole('region', { name: 'YouTube cookie' })
    expect(within(card).getByText('Refused')).toBeInTheDocument()
    expect(within(card).getByText('The playlist does not exist.')).toBeInTheDocument()
  })

  it('checks the cookie again, then shows what YouTube said', async () => {
    const user = userEvent.setup()
    let state: YouTubeCookie['state'] = 'unchecked'
    const api = routes({
      '/api/admin/youtube': () => overview({ cookie: cookie({ state }) }),
      '/api/admin/youtube/cookie/check': (_url: URL, init?: RequestInit) => {
        expect(init?.method).toBe('POST')
        state = 'works'
        return cookie({ state })
      },
    })
    render(YouTubePage)
    const card = await screen.findByRole('region', { name: 'YouTube cookie' })
    expect(within(card).getByText('Not checked')).toBeInTheDocument()

    await user.click(within(card).getByRole('button', { name: 'Check it now' }))

    expect(api.requests('/api/admin/youtube/cookie/check')).toHaveLength(1)
    expect(await within(card).findByText('Works')).toBeInTheDocument()
  })

  it('asks before removing the cookie', async () => {
    const user = userEvent.setup()
    let present = true
    const api = routes({
      '/api/admin/youtube': () => overview({ cookie: present ? cookie() : null }),
      '/api/admin/youtube/cookie': (_url: URL, init?: RequestInit) => {
        expect(init?.method).toBe('DELETE')
        present = false
        return new Response(null, { status: 204 })
      },
    })
    render(YouTubePage)
    const card = await screen.findByRole('region', { name: 'YouTube cookie' })

    await user.click(within(card).getByRole('button', { name: 'Remove it' }))
    const ask = within(card).getByRole('group', { name: 'Remove it' })
    expect(ask).toHaveTextContent('reads YouTube signed out')
    await user.click(within(ask).getByRole('button', { name: 'Keep it' }))
    expect(api.requests('/api/admin/youtube/cookie')).toHaveLength(0)

    await user.click(within(card).getByRole('button', { name: 'Remove it' }))
    await user.click(within(within(card).getByRole('group')).getByRole('button', { name: 'Remove it' }))

    expect(api.requests('/api/admin/youtube/cookie')).toHaveLength(1)
    expect(await within(card).findByText('None')).toBeInTheDocument()
  })

  it('chooses one official release: sent, then on its way', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    routes({
      '/api/admin/youtube/ytdlp': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          sent.push(JSON.parse(String(init.body)))
          return new Response(
            JSON.stringify(
              ytdlp({
                choice: {
                  kind: 'official',
                  version: '2026.08.19',
                  chosenBy: 'admin',
                  chosenAt: '2026-10-15T10:00:00Z',
                },
              }),
            ),
            { status: 202, headers: { 'content-type': 'application/json' } },
          )
        }
        return ytdlp()
      },
    })
    render(YouTubePage)
    const box = await screen.findByRole('region', { name: 'yt-dlp' })
    const select = await within(box).findByRole('combobox', { name: 'One official release' })
    expect(select).toBeDisabled()

    await user.click(within(box).getByRole('radio', { name: /One official release/ }))
    await user.selectOptions(select, '2026.08.19')
    await user.click(within(box).getByRole('button', { name: 'Use this version' }))

    expect(sent).toEqual([{ kind: 'official', version: '2026.08.19' }])
    expect(await within(box).findByRole('status')).toHaveTextContent('2026.08.19: downloading, checking, trying…')
    expect(within(box).getByText('Official release 2026.08.19')).toBeInTheDocument()
    expect(within(box).getByText('by admin')).toBeInTheDocument()
  })

  it('offers the nightly builds, and the latest release needs no version', async () => {
    const user = userEvent.setup()
    const sent: unknown[] = []
    routes({
      '/api/admin/youtube/ytdlp': (_url: URL, init?: RequestInit) => {
        if (init?.method === 'PUT') {
          sent.push(JSON.parse(String(init.body)))
        }
        return ytdlp({ choice: { kind: 'nightly', version: '2026.09.27.232945', chosenBy: 'admin', chosenAt: null } })
      },
    })
    render(YouTubePage)
    const box = await screen.findByRole('region', { name: 'yt-dlp' })
    const nightly = await within(box).findByRole('combobox', { name: 'One nightly build' })

    expect(within(box).getByRole('radio', { name: /One nightly build/ })).toBeChecked()
    expect(nightly).toHaveValue('2026.09.27.232945')
    expect(
      within(nightly)
        .getAllByRole('option')
        .map((o) => o.getAttribute('value')),
    ).toEqual(['2026.10.10.233141', '2026.09.27.232945'])

    await user.click(within(box).getByRole('radio', { name: /Latest official release/ }))
    await user.click(within(box).getByRole('button', { name: 'Use this version' }))

    expect(sent).toEqual([{ kind: 'latest', version: null }])
  })

  it('says when a version waits for the task going on, and why the last check failed', async () => {
    routes({
      '/api/admin/youtube/ytdlp': ytdlp({
        waiting: '2026.10.10.233141',
        error: "GitHub can't be reached to list yt-dlp's releases.",
      }),
    })
    render(YouTubePage)
    const box = await screen.findByRole('region', { name: 'yt-dlp' })

    expect(
      await within(box).findByText('2026.10.10.233141 is ready: it takes over when the task going on ends.'),
    ).toBeInTheDocument()
    expect(within(box).getByText("GitHub can't be reached to list yt-dlp's releases.")).toBeInTheDocument()
  })

  it('without GitHub, says the versions can’t be listed: only the latest stays to choose', async () => {
    routes({
      '/api/admin/youtube/ytdlp/versions': problemResponse(502, "GitHub can't be reached to list yt-dlp's releases."),
    })
    render(YouTubePage)
    const box = await screen.findByRole('region', { name: 'yt-dlp' })

    expect(
      await within(box).findByText(/The versions can't be listed now: GitHub can't be reached/),
    ).toBeInTheDocument()
    expect(within(box).queryByRole('combobox')).not.toBeInTheDocument()
    expect(within(box).getByRole('radio', { name: /Latest official release/ })).toBeInTheDocument()
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    fakeApi({
      '/api/auth/me': { username: 'admin' },
      '/api/admin/youtube': problemResponse(401, 'Nobody is logged in.'),
      '/api/admin/youtube/ytdlp': problemResponse(401, 'Nobody is logged in.'),
      '/api/admin/youtube/ytdlp/versions': problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(YouTubePage)

    await vi.waitFor(() => expect(auth.status).toBe('anonymous'))
  })
})
