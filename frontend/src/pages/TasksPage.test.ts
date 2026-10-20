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
      '/api/admin/tasks': tasks({ tasks: [{ task: 'import', lastRun: null, unavailable: reason }] }),
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

  it('says the queue waits while a run is on hold', async () => {
    const held = run({
      id: 'run-20261016T140200Z-7c31aa',
      task: 'refreshChannelVideos',
      state: 'onHold',
      target: 'UCunderTheCity',
      resumeAt: '2026-10-16T15:20:00Z',
      cancellable: true,
    })
    const waiting = run({ id: 'run-20261016T141000Z-bbbbbb', state: 'queued', position: 1, finishedAt: null })
    fakeApi({ '/api/admin/tasks': tasks({ onHold: [held], queue: [waiting] }), '/api/admin/runs': page([waiting]) })
    render(TasksPage)

    const queue = await screen.findByRole('region', { name: 'Waiting · 1' })
    expect(within(queue).getByRole('status')).toHaveTextContent(
      'The queue is on hold: Refresh a channel and its videos is on hold.',
    )
    expect(queue).toHaveTextContent(/On hold until/)
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
