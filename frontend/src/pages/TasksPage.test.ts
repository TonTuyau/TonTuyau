import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { auth } from '../lib/auth.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { page, run, tasks } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import TasksPage from './TasksPage.svelte'

const failed = run({
  id: 'run-20261011T110000Z-0e5cdf',
  state: 'failed',
  startedAt: '2026-10-11T11:00:00Z',
  finishedAt: '2026-10-11T11:01:01Z',
  error: 'TubeArchivist failed (502).',
})
const canceled = run({
  id: 'run-20261012T071204Z-5b87a2',
  task: 'indexRebuild',
  trigger: 'admin',
  startedBy: 'claire',
  state: 'canceled',
  startedAt: '2026-10-12T07:12:04Z',
  finishedAt: '2026-10-12T07:16:36Z',
})

/** The history's rows: each cell's text. */
const rows = () =>
  within(screen.getByRole('table', { name: 'Run history' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) =>
      within(row)
        .getAllByRole('cell')
        .map((cell) => cell.textContent?.trim()),
    )

describe('TasksPage', () => {
  beforeEach(() => goTo('/admin/tasks'))

  it('shows the import: its command, when it runs, how it last ran', async () => {
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
    render(TasksPage)

    const card = await screen.findByRole('article', { name: 'TubeArchivist import' })
    expect(within(card).getByText('tontuyau import')).toBeInTheDocument()
    expect(within(card).getByText('When the backend starts, and on demand')).toBeInTheDocument()
    expect(within(card).getByText('Succeeded')).toBeInTheDocument()
    expect(within(card).getByText(/· 7 s$/)).toBeInTheDocument()
    expect(within(card).getByRole('button', { name: 'Run now' })).toBeEnabled()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('says why the import can’t start without TubeArchivist', async () => {
    const reason = "TubeArchivist isn't set up (TA_URL, TA_TOKEN): there is nothing to import."
    fakeApi({
      '/api/admin/tasks': tasks({ tasks: [{ task: 'import', lastRun: null, unavailable: reason, next: null }] }),
      '/api/admin/runs': page([]),
    })
    render(TasksPage)

    const card = await screen.findByRole('article', { name: 'TubeArchivist import' })
    expect(within(card).getByText(reason)).toBeInTheDocument()
    expect(within(card).getByText('Never')).toBeInTheDocument()
    expect(within(card).getByRole('button', { name: 'Run now' })).toBeDisabled()
  })

  it('says what is going on, and the running run’s duration goes up by itself', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date('2026-10-12T14:00:20Z') })
    const running = run({
      id: 'run-20261012T140000Z-25ede6',
      state: 'running',
      startedAt: '2026-10-12T14:00:00Z',
      finishedAt: null,
    })
    const busy = tasks({ lock: { task: 'import', trigger: 'startup', since: '2026-10-12T14:00:00Z' } })
    busy.tasks = busy.tasks.map((info) => (info.task === 'import' ? { ...info, lastRun: running } : info))
    fakeApi({ '/api/admin/tasks': busy, '/api/admin/runs': page([running]) })
    render(TasksPage)

    expect(await screen.findByRole('status')).toHaveTextContent('TubeArchivist import is going on, started by Startup')
    const card = screen.getByRole('article', { name: 'TubeArchivist import' })
    expect(within(card).getByText('for 20 s')).toBeInTheDocument()
    await vi.advanceTimersByTimeAsync(2_000)
    expect(within(card).getByText('for 22 s')).toBeInTheDocument()
    vi.useRealTimers()
  })

  it('lists the runs latest first: state, task and id, who started it, its result', async () => {
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([canceled, run(), failed], 3) })
    render(TasksPage)

    await screen.findByRole('table', { name: 'Run history' })
    expect(screen.getByText('3 runs')).toBeInTheDocument()
    const [first, second, third] = rows()
    expect(first?.[0]).toBe('Canceled')
    expect(first?.[1]).toBe(`Index rebuild ${canceled.id}`)
    expect(first?.[2]).toBe('claire')
    expect(first?.[4]).toBe('4 min 32 s')
    expect(first?.[5]).toBe('Stopped')
    expect(second?.[2]).toBe('Startup')
    expect(second?.[5]).toBe('8 new · 50 already there')
    expect(third?.[5]).toBe('TubeArchivist failed (502).')
    expect(screen.getByRole('link', { name: canceled.id })).toHaveAttribute('href', `/admin/runs/${canceled.id}`)
  })

  it('names the browser extension’s key as who started its run', async () => {
    const fromExtension = run({
      id: 'run-20261021T090000Z-dddddd',
      task: 'downloadVideo',
      trigger: 'extension',
      startedBy: 'Firefox, laptop (API key)',
    })
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([fromExtension]) })
    render(TasksPage)

    await screen.findByRole('table', { name: 'Run history' })
    expect(rows()[0]?.[2]).toBe('Firefox, laptop (API key)')
  })

  it('shows one task’s runs from the URL, and the chips link to each task', async () => {
    goTo('/admin/tasks?task=indexRebuild')
    const api = fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([canceled]) })
    render(TasksPage)

    await screen.findByRole('table', { name: 'Run history' })
    expect(api.requests('/api/admin/runs').map((url) => url.search)).toEqual(['?task=indexRebuild'])
    const chips = screen.getByRole('navigation', { name: 'Task' })
    expect(within(chips).getByRole('link', { name: 'Index rebuild' })).toHaveAttribute('aria-current', 'true')
    expect(within(chips).getByRole('link', { name: 'All tasks' })).toHaveAttribute('href', '/admin/tasks')
    expect(within(chips).getByRole('link', { name: 'TubeArchivist import' })).toHaveAttribute(
      'href',
      '/admin/tasks?task=import',
    )
  })

  it('loads the next page after the last run', async () => {
    const api = fakeApi({
      '/api/admin/tasks': tasks(),
      '/api/admin/runs': (url: URL) =>
        url.searchParams.has('before') ? page([failed], 2) : page([run()], 2, 'run-20261012T120000Z-3f9a1c'),
    })
    render(TasksPage)

    ;(await screen.findByRole('button', { name: 'Show more' })).click()
    await vi.waitFor(() => expect(rows()).toHaveLength(2))
    expect(api.requests('/api/admin/runs').map((url) => url.search)).toEqual([
      '',
      '?before=run-20261012T120000Z-3f9a1c',
    ])
  })

  it('keeps up to date while a run goes on, without redrawing the rows that are there', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const running = run({ id: 'run-20261012T140000Z-25ede6', state: 'running', finishedAt: null })
    const busy = tasks({ lock: { task: 'import', trigger: 'startup', since: running.startedAt } })
    let over = false
    fakeApi({
      '/api/admin/tasks': () => (over ? tasks() : busy),
      '/api/admin/runs': () => page([over ? run({ id: running.id }) : running, failed], 2),
    })
    render(TasksPage)

    await screen.findByRole('table', { name: 'Run history' })
    const row = screen.getByText(running.id).closest('tr')
    const other = screen.getByText(failed.id).closest('tr')
    expect(rows()[0]?.[0]).toBe('Running')

    over = true
    await vi.advanceTimersByTimeAsync(3_000)
    await vi.waitFor(() => expect(rows()[0]?.[0]).toBe('Succeeded'))
    expect(screen.getByText(running.id).closest('tr')).toBe(row)
    expect(screen.getByText(failed.id).closest('tr')).toBe(other)
    vi.useRealTimers()
  })

  it('shows the YouTube syncs: when the incremental one runs next, and the full one asks first', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date('2026-10-12T14:00:00Z') })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const started = run({ id: 'run-20261012T140000Z-cccccc', state: 'running', finishedAt: null })
    const api = fakeApi({
      '/api/admin/tasks': tasks(),
      '/api/admin/runs': page([run()]),
      '/api/admin/tasks/youTubeSync/runs': { ...started, task: 'youTubeSync' },
      '/api/admin/tasks/youTubeFullSync/runs': { ...started, task: 'youTubeFullSync' },
    })
    render(TasksPage)

    const sync = await screen.findByRole('article', { name: 'YouTube sync' })
    expect(within(sync).getByText('tontuyau youtube sync')).toBeInTheDocument()
    expect(sync).toHaveTextContent(/Next by itself at .*, and on demand/)
    const full = screen.getByRole('article', { name: 'Full YouTube sync' })
    expect(full).toHaveTextContent('On demand only')

    await user.click(within(full).getByRole('button', { name: 'Run now' }))
    expect(within(full).getByText(/A full sync lists every subscribed channel whole/)).toBeInTheDocument()
    expect(api.requests('/api/admin/tasks/youTubeFullSync/runs')).toEqual([])
    await user.click(within(full).getByRole('button', { name: 'Back' }))

    await user.click(within(sync).getByRole('button', { name: 'Run now' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/youTubeSync/runs')).toHaveLength(1))
    vi.useRealTimers()
  })

  it('refreshes everything once asked, skipping what was read in the days chosen', async () => {
    const user = userEvent.setup()
    const started = run({ id: 'run-20261021T100000Z-aaaaaa', task: 'refreshAll', state: 'running', finishedAt: null })
    const withRefreshAll = tasks()
    withRefreshAll.tasks = [
      ...withRefreshAll.tasks,
      { task: 'refreshAll', lastRun: null, unavailable: null, next: null },
    ]
    const api = fakeApi({
      '/api/admin/tasks': withRefreshAll,
      '/api/admin/runs': page([run()]),
      '/api/admin/tasks/refreshAll/runs': started,
    })
    render(TasksPage)

    const card = await screen.findByRole('article', { name: 'Refresh everything' })
    expect(within(card).getByText('tontuyau youtube refresh-all [days]')).toBeInTheDocument()
    await user.click(within(card).getByRole('button', { name: 'Run now' }))
    const confirm = within(card).getByRole('group', { name: 'Refresh everything' })
    expect(confirm).toHaveTextContent(/days for a big library/)
    const days = within(confirm).getByRole('spinbutton', { name: /Skip what was read in the last/ })
    expect(days).toHaveValue(7)

    await user.clear(days)
    await user.type(days, '400')
    expect(within(confirm).getByRole('alert')).toHaveTextContent('A whole number of days, from 0')
    expect(within(confirm).getByRole('button', { name: 'Refresh everything' })).toBeDisabled()

    await user.clear(days)
    await user.type(days, '30')
    await user.click(within(confirm).getByRole('button', { name: 'Refresh everything' }))
    await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
    expect(api.requests('/api/admin/tasks/refreshAll/runs')[0]?.searchParams.get('days')).toBe('30')
  })

  it('lists the runs waiting their turn, each with who asked and Cancel; the cards add to the queue', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date('2026-10-12T14:00:20Z') })
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime })
    const running = run({ id: 'run-20261012T140000Z-25ede6', state: 'running', finishedAt: null })
    const waiting = run({
      id: 'run-20261012T140010Z-aaaaaa',
      task: 'refreshChannelVideos',
      target: 'UCmw5b3sYz6v1sFkwM1HLs1g',
      trigger: 'admin',
      startedBy: 'claire',
      state: 'queued',
      startedAt: '2026-10-12T14:00:10Z',
      queuedAt: '2026-10-12T14:00:10Z',
      finishedAt: null,
      position: 1,
      cancellable: true,
    })
    const busy = tasks({
      lock: { task: 'import', trigger: 'startup', since: running.startedAt },
      queue: [waiting],
    })
    const api = fakeApi({
      '/api/admin/tasks': busy,
      '/api/admin/runs': page([waiting, running], 2),
      [`/api/admin/runs/${waiting.id}/cancel`]: { ...waiting, state: 'canceled', cancellable: false },
    })
    render(TasksPage)

    const queue = await screen.findByRole('region', { name: 'Waiting · 1' })
    expect(queue).toHaveTextContent('One at a time, in this order')
    expect(queue).toHaveTextContent('Refresh a channel and its videos UCmw5b3sYz6v1sFkwM1HLs1g')
    expect(queue).toHaveTextContent('claire · 10 seconds ago')
    const video = screen.getByRole('article', { name: 'Refresh a video' })
    expect(within(video).getByRole('button', { name: 'Add to the queue' })).toBeEnabled()
    expect(within(video).getByText('A run is going on: this one waits its turn.')).toBeInTheDocument()

    await user.click(within(queue).getByRole('button', { name: 'Cancel' }))
    await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${waiting.id}/cancel`)).toHaveLength(1))
    vi.useRealTimers()
  })

  it('says the queue waits for a run YouTube refused, not for one held by hand or for disk space (2.3.0)', async () => {
    const held = run({
      id: 'run-20261016T140200Z-7c31aa',
      task: 'refreshChannelVideos',
      state: 'onHold',
      target: 'UCunderTheCity',
      resumeAt: '2026-10-16T15:20:00Z',
      heldFor: 'youTube',
      cancellable: true,
    })
    const waiting = run({ id: 'run-20261016T141000Z-bbbbbb', state: 'queued', position: 1, finishedAt: null })
    fakeApi({
      '/api/admin/tasks': tasks({ onHold: [held], queue: [waiting], queueWaitsFor: held.id }),
      '/api/admin/runs': page([waiting]),
    })
    const { unmount } = render(TasksPage)

    const queue = await screen.findByRole('region', { name: 'Waiting · 1' })
    expect(within(queue).getByRole('status')).toHaveTextContent(
      'The queue waits: YouTube refused Refresh a channel and its videos, and would refuse the next runs too.',
    )
    expect(queue).toHaveTextContent(/On hold until/)
    expect(screen.getByRole('region', { name: 'On hold' })).toHaveTextContent('The queue waits for it.')
    unmount()

    for (const [heldFor, text] of [
      ['hand', 'Put on hold by hand on'],
      ['diskSpace', 'Too little disk space on'],
    ] as const) {
      fakeApi({
        '/api/admin/tasks': tasks({ onHold: [{ ...held, heldFor }], queue: [waiting], queueWaitsFor: null }),
        '/api/admin/runs': page([waiting]),
      })
      const rendered = render(TasksPage)
      const goesOn = await screen.findByRole('region', { name: 'Waiting · 1' })
      expect(within(goesOn).queryByRole('status')).toBeNull()
      expect(goesOn).toHaveTextContent('One at a time, in this order')
      const bar = screen.getByRole('region', { name: 'On hold' })
      expect(bar).toHaveTextContent(text)
      expect(bar).toHaveTextContent('The queue goes on meanwhile.')
      rendered.unmount()
    }
  })

  it('runs a queued run now; says why when it’s refused; a run held back in the queue says what it has left', async () => {
    const user = userEvent.setup()
    const back = run({
      id: 'run-20261016T140200Z-7c31aa',
      task: 'youTubeFullSync',
      state: 'queued',
      position: 1,
      left: 1874,
      firstAt: '2026-10-16T15:00:00Z',
      finishedAt: null,
      cancellable: true,
    })
    const waiting = run({ id: 'run-20261016T141000Z-bbbbbb', state: 'queued', position: 2, finishedAt: null })
    const api = fakeApi({
      '/api/admin/tasks': tasks({ queue: [back, waiting] }),
      '/api/admin/runs': page([waiting, back]),
      [`/api/admin/runs/${waiting.id}/run-now`]: { ...waiting, position: 1 },
      [`/api/admin/runs/${back.id}/run-now`]: problemResponse(409, `The run ${back.id} doesn't wait in the queue.`),
    })
    render(TasksPage)

    const queue = await screen.findByRole('region', { name: 'Waiting · 2' })
    const [first, second] = within(queue).getAllByRole('listitem')
    if (!first || !second) throw new Error('Two runs wait.')
    expect(first).toHaveTextContent('Full YouTube sync')
    expect(first).toHaveTextContent('Held, resumes here: 1,874 left')
    expect(second).not.toHaveTextContent('Held')

    await user.click(within(second).getByRole('button', { name: 'Run now' }))
    await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${waiting.id}/run-now`)).toHaveLength(1))
    expect(location.pathname).toBe('/admin/tasks')

    await user.click(within(first).getByRole('button', { name: 'Run now' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(`Not run now: The run ${back.id} doesn't wait`)
  })

  it('speaks French', async () => {
    i18n.setLang('fr')
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
    render(TasksPage)

    expect(await screen.findByRole('article', { name: 'Import de TubeArchivist' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Tâches', level: 1 })).toBeInTheDocument()
    expect(await screen.findByText('8 nouvelles · 50 déjà là')).toBeInTheDocument()
    i18n.setLang('en')
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    fakeApi({
      '/api/auth/me': { username: 'admin' },
      '/api/admin/tasks': problemResponse(401, 'Nobody is logged in.'),
      '/api/admin/runs': problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(TasksPage)

    await vi.waitFor(() => expect(auth.status).toBe('anonymous'))
  })

  describe('starting and canceling', () => {
    const started = run({ id: 'run-20261014T090000Z-aaaaaa', state: 'running', finishedAt: null, trigger: 'admin' })

    it('refreshes a video pasted as a link, with its id, then shows its run', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/refreshVideo/runs': started,
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Refresh a video' })
      expect(within(card).getByText('tontuyau youtube video <id>')).toBeInTheDocument()
      await user.type(within(card).getByLabelText('Video link or id'), 'https://youtu.be/k3Fq8xZ0aQw?si=x')
      await user.click(within(card).getByRole('button', { name: 'Refresh' }))

      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
      const [request] = api.requests('/api/admin/tasks/refreshVideo/runs')
      expect(request?.searchParams.get('target')).toBe('k3Fq8xZ0aQw')
    })

    it('downloads a video pasted as a link, with its id, then shows its run', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/downloadVideo/runs': { ...started, task: 'downloadVideo' },
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Download a video' })
      expect(within(card).getByText('tontuyau youtube download <id|link>')).toBeInTheDocument()
      expect(card).toHaveTextContent('A video of the library is downloaded only when its file is missing.')
      await user.type(within(card).getByLabelText('Video link or id'), 'https://www.youtube.com/shorts/k3Fq8xZ0aQw')
      await user.click(within(card).getByRole('button', { name: 'Download' }))

      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
      const [request] = api.requests('/api/admin/tasks/downloadVideo/runs')
      expect(request?.searchParams.get('target')).toBe('k3Fq8xZ0aQw')
    })

    it('adds a channel pasted as its @handle link, with the tabs ticked, then shows its run', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/addChannel/runs': { ...started, task: 'addChannel' },
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Add a channel' })
      expect(within(card).getByText(/tontuyau youtube add-channel/)).toBeInTheDocument()
      for (const tab of ['Videos', 'Shorts', 'Streams']) {
        expect(within(card).getByRole('checkbox', { name: tab })).toBeChecked()
      }
      await user.type(
        within(card).getByLabelText('Channel link, @handle or id'),
        'https://www.youtube.com/@underthecity/videos',
      )
      await user.click(within(card).getByRole('checkbox', { name: 'Shorts' }))
      await user.click(within(card).getByRole('button', { name: 'Add' }))

      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
      const [request] = api.requests('/api/admin/tasks/addChannel/runs')
      expect(request?.searchParams.get('target')).toBe('@underthecity')
      expect(request?.searchParams.get('tabs')).toBe('videos,streams')
    })

    it('ticks the YouTube page’s default tabs for a channel to add', async () => {
      fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/youtube/settings': {
          comments: 'off',
          commentsMax: 500,
          pauseSeconds: 10,
          holdMinutes: 60,
          syncEveryMinutes: 360,
          downloadFormat: 'bestvideo+bestaudio',
          defaultTabs: { videos: true, shorts: false, streams: false },
          changedBy: null,
          changedAt: null,
        },
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Add a channel' })
      await vi.waitFor(() => expect(within(card).getByRole('checkbox', { name: 'Shorts' })).not.toBeChecked())
      expect(within(card).getByRole('checkbox', { name: 'Videos' })).toBeChecked()
      expect(within(card).getByRole('checkbox', { name: 'Streams' })).not.toBeChecked()
    })

    it('refuses a channel to add without a tab, or that isn’t one, without asking', async () => {
      const user = userEvent.setup()
      const api = fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Add a channel' })
      const field = within(card).getByLabelText('Channel link, @handle or id')
      await user.type(field, 'https://vimeo.com/@underthecity')
      await user.click(within(card).getByRole('button', { name: 'Add' }))
      expect(within(card).getByRole('alert')).toHaveTextContent("That isn't a channel id (UC…), an @handle")

      await user.clear(field)
      await user.type(field, '@underthecity')
      for (const tab of ['Videos', 'Shorts', 'Streams']) {
        await user.click(within(card).getByRole('checkbox', { name: tab }))
      }
      await user.click(within(card).getByRole('button', { name: 'Add' }))
      expect(within(card).getByRole('alert')).toHaveTextContent('Tick at least one tab.')
      expect(api.requests('/api/admin/tasks/addChannel/runs')).toEqual([])
    })

    it('refuses what isn’t a video or a channel, without asking', async () => {
      const user = userEvent.setup()
      const api = fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
      render(TasksPage)

      const video = await screen.findByRole('article', { name: 'Refresh a video' })
      await user.click(within(video).getByRole('button', { name: 'Refresh' }))
      expect(within(video).getByRole('alert')).toHaveTextContent('Paste a link or an id first.')

      const channel = screen.getByRole('article', { name: 'Refresh a channel and its videos' })
      await user.type(within(channel).getByLabelText('Channel link or id'), '@underthecity')
      await user.click(within(channel).getByRole('button', { name: 'Refresh them' }))
      expect(within(channel).getByRole('alert')).toHaveTextContent(/An @handle can't be read yet/)
      expect(api.calls.filter((url) => url.pathname.endsWith('/runs') && url.pathname.includes('refresh'))).toEqual([])
    })

    it('warns when there is no YouTube cookie', async () => {
      fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/youtube': { cookie: null, ytdlp: '2026.09.30', deno: '2.9.7' },
      })
      render(TasksPage)

      expect(await screen.findByText(/^No YouTube cookie: YouTube may refuse/)).toBeInTheDocument()
      expect(screen.getByRole('link', { name: 'YouTube' })).toHaveAttribute('href', '/admin/youtube')
    })

    it('asks before an import, then shows its run', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/import/runs': started,
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'TubeArchivist import' })
      await user.click(within(card).getByRole('button', { name: 'Run now' }))
      expect(within(card).getByRole('group')).toHaveTextContent(/up to an hour/)
      await user.click(within(card).getByRole('button', { name: 'Back' }))
      expect(api.requests('/api/admin/tasks/import/runs')).toHaveLength(0)

      await user.click(within(card).getByRole('button', { name: 'Run now' }))
      await user.click(within(card).getByRole('button', { name: 'Start the import' }))
      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
      expect(api.requests('/api/admin/tasks/import/runs')).toHaveLength(1)
    })

    it('one run at a time: the other buttons add to the queue, this task’s run can be canceled (asked first)', async () => {
      const user = userEvent.setup()
      const running = run({ id: 'run-20261014T090000Z-bbbbbb', state: 'running', finishedAt: null })
      const busy = tasks({
        lock: { task: 'import', trigger: 'startup', since: running.startedAt },
        runningHere: running.id,
      })
      busy.tasks = busy.tasks.map((info) => (info.task === 'import' ? { ...info, lastRun: running } : info))
      const api = fakeApi({
        '/api/admin/tasks': busy,
        '/api/admin/runs': page([running]),
        [`/api/admin/runs/${running.id}/cancel`]: { ...running, cancellable: true },
      })
      render(TasksPage)

      const video = await screen.findByRole('article', { name: 'Refresh a video' })
      expect(within(video).getByRole('button', { name: 'Add to the queue' })).toBeEnabled()
      expect(within(video).getByText('A run is going on: this one waits its turn.')).toBeInTheDocument()

      const card = screen.getByRole('article', { name: 'TubeArchivist import' })
      expect(within(card).getByRole('link', { name: 'Follow its log' })).toHaveAttribute(
        'href',
        `/admin/runs/${running.id}`,
      )
      await user.click(within(card).getByRole('button', { name: 'Cancel' }))
      await user.click(within(card).getByRole('button', { name: 'Cancel the run' }))
      await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${running.id}/cancel`)).toHaveLength(1))
    })

    it('a run going on in another process can only be stopped there', async () => {
      const running = run({ state: 'running', finishedAt: null, trigger: 'cli' })
      const busy = tasks({ lock: { task: 'import', trigger: 'cli', since: running.startedAt } })
      busy.tasks = busy.tasks.map((info) => (info.task === 'import' ? { ...info, lastRun: running } : info))
      fakeApi({ '/api/admin/tasks': busy, '/api/admin/runs': page([running]) })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'TubeArchivist import' })
      expect(within(card).queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument()
      expect(within(card).getByText(/stop it there/)).toBeInTheDocument()
    })

    it('shows a refresh on hold: what it read and has left, Resume now (then its run), Cancel it', async () => {
      const user = userEvent.setup()
      const held = run({
        id: 'run-20261016T140200Z-7c31aa',
        task: 'refreshChannelVideos',
        trigger: 'admin',
        startedBy: 'admin',
        state: 'onHold',
        target: 'UCunderTheCity',
        startedAt: '2026-10-16T14:02:00Z',
        finishedAt: '2026-10-16T14:20:00Z',
        resumeAt: '2026-10-16T15:20:00Z',
        left: 59,
        cancellable: true,
        counts: { ...run().counts, videosRead: 58, channelsRead: 0 },
      })
      const api = fakeApi({
        '/api/admin/tasks': tasks({ onHold: [held] }),
        '/api/admin/runs': page([held]),
        [`/api/admin/runs/${held.id}/cancel`]: { ...held, state: 'canceled' },
        [`/api/admin/runs/${held.id}/resume`]: { ...held, state: 'running' },
      })
      render(TasksPage)

      const bar = await screen.findByRole('region', { name: 'On hold' })
      expect(bar).toHaveTextContent('On hold')
      expect(bar).toHaveTextContent('Refresh a channel and its videos UCunderTheCity')
      expect(bar).toHaveTextContent('58 read, 59 left.')
      expect(within(bar).getByRole('link', { name: /Refresh a channel and its videos/ })).toHaveAttribute(
        'href',
        `/admin/runs/${held.id}`,
      )
      expect(rows()[0]?.[0]).toBe('On hold')
      expect(rows()[0]?.[5]).toMatch(/^On hold until .*: 58 read, 59 left$/)

      await user.click(within(bar).getByRole('button', { name: 'Cancel it' }))
      await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${held.id}/cancel`)).toHaveLength(1))
      await user.click(within(bar).getByRole('button', { name: 'Resume now' }))
      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${held.id}`))
      expect(api.requests(`/api/admin/runs/${held.id}/resume`)).toHaveLength(1)
    })

    it('lets a run on hold be resumed or canceled, while others wait behind it or another goes on (2.3.0)', async () => {
      const held = run({ id: 'run-20261016T140200Z-7c31aa', task: 'youTubeSync', state: 'onHold', cancellable: true })
      const waiting = run({ id: 'run-20261016T150000Z-1a2b3c', task: 'refreshVideo', state: 'queued', position: 1 })
      fakeApi({
        '/api/admin/tasks': tasks({ onHold: [held], queue: [waiting] }),
        '/api/admin/runs': page([waiting, held]),
      })
      const { unmount } = render(TasksPage)

      const bar = await screen.findByRole('region', { name: 'On hold' })
      expect(within(bar).getByRole('button', { name: 'Resume now' })).toBeEnabled()
      expect(within(bar).getByRole('button', { name: 'Cancel it' })).toBeEnabled()
      unmount()

      // Another run going on: Resume now puts it back in the queue, first.
      fakeApi({
        '/api/admin/tasks': tasks({
          onHold: [held],
          lock: { task: 'import', trigger: 'cli', since: new Date().toISOString() },
        }),
        '/api/admin/runs': page([held]),
      })
      render(TasksPage)
      const busyBar = await screen.findByRole('region', { name: 'On hold' })
      expect(within(busyBar).getByRole('button', { name: 'Resume now' })).toBeEnabled()
    })

    it('says why a start was refused', async () => {
      const user = userEvent.setup()
      fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/import/runs': problemResponse(409, 'A run is already going on.'),
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'TubeArchivist import' })
      await user.click(within(card).getByRole('button', { name: 'Run now' }))
      await user.click(within(card).getByRole('button', { name: 'Start the import' }))
      expect(await screen.findByRole('alert')).toHaveTextContent('Not started: A run is already going on.')
      expect(location.pathname).toBe('/admin/tasks')
    })
  })
})
