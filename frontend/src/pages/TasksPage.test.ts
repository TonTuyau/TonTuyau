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
  task: 'full',
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

  it('shows the three tasks: their command, when they run next, how they last ran, the list waiting', async () => {
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
    render(TasksPage)

    const incremental = await screen.findByRole('article', { name: 'Incremental sync' })
    expect(within(incremental).getByText('tontuyau sync incremental')).toBeInTheDocument()
    expect(within(incremental).getByText('0 7-23 * * *')).toBeInTheDocument()
    expect(within(incremental).getByText(/^next /)).toBeInTheDocument()
    expect(within(incremental).getByText('Succeeded')).toBeInTheDocument()
    expect(within(incremental).getByText(/· 7 s$/)).toBeInTheDocument()

    const deletions = screen.getByRole('article', { name: 'Apply deletions' })
    expect(within(deletions).getByText('312 deletions waiting')).toBeInTheDocument()
    expect(within(deletions).getByText(/^from the full sync of /)).toBeInTheDocument()
    expect(within(deletions).getByText('Never')).toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('says what is going on, and the running run’s duration goes up by itself', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true, now: new Date('2026-10-12T14:00:20Z') })
    const running = run({
      id: 'run-20261012T140000Z-25ede6',
      state: 'running',
      startedAt: '2026-10-12T14:00:00Z',
      finishedAt: null,
    })
    const busy = tasks({ lock: { task: 'incremental', trigger: 'scheduler', since: '2026-10-12T14:00:00Z' } })
    busy.tasks = busy.tasks.map((info) => (info.task === 'incremental' ? { ...info, lastRun: running } : info))
    fakeApi({ '/api/admin/tasks': busy, '/api/admin/runs': page([running]) })
    render(TasksPage)

    expect(await screen.findByRole('status')).toHaveTextContent('Incremental sync is going on, started by Schedule')
    const card = screen.getByRole('article', { name: 'Incremental sync' })
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
    expect(first?.[1]).toBe(`Full sync ${canceled.id}`)
    expect(first?.[2]).toBe('claire')
    expect(first?.[4]).toBe('4 min 32 s')
    expect(first?.[5]).toBe('Stopped')
    expect(second?.[2]).toBe('Schedule')
    expect(second?.[5]).toBe('8 new · 3 changed')
    expect(third?.[5]).toBe('TubeArchivist failed (502).')
    expect(screen.getByRole('link', { name: canceled.id })).toHaveAttribute('href', `/admin/runs/${canceled.id}`)
  })

  it('shows one task’s runs from the URL, and the chips link to each task', async () => {
    goTo('/admin/tasks?task=full')
    const api = fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([canceled]) })
    render(TasksPage)

    await screen.findByRole('table', { name: 'Run history' })
    expect(api.requests('/api/admin/runs').map((url) => url.search)).toEqual(['?task=full'])
    const chips = screen.getByRole('navigation', { name: 'Task' })
    expect(within(chips).getByRole('link', { name: 'Full sync' })).toHaveAttribute('aria-current', 'true')
    expect(within(chips).getByRole('link', { name: 'All tasks' })).toHaveAttribute('href', '/admin/tasks')
    expect(within(chips).getByRole('link', { name: 'Apply deletions' })).toHaveAttribute(
      'href',
      '/admin/tasks?task=applyDeletions',
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
    const busy = tasks({ lock: { task: 'incremental', trigger: 'scheduler', since: running.startedAt } })
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

  it('speaks French', async () => {
    i18n.setLang('fr')
    fakeApi({ '/api/admin/tasks': tasks(), '/api/admin/runs': page([run()]) })
    render(TasksPage)

    expect(await screen.findByRole('article', { name: 'Synchronisation incrémentale' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Tâches', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('312 suppressions en attente')).toBeInTheDocument()
    expect(await screen.findByText('8 nouvelles · 3 modifiées')).toBeInTheDocument()
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

    it('starts the incremental sync at once, then shows its run', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/incremental/runs': started,
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Incremental sync' })
      await user.click(within(card).getByRole('button', { name: 'Run now' }))
      await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
      expect(api.requests('/api/admin/tasks/incremental/runs')).toHaveLength(1)
    })

    it('asks before a full sync, and before applying the list that waits', async () => {
      const user = userEvent.setup()
      const api = fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/full/runs': started,
        '/api/admin/tasks/applyDeletions/runs': started,
      })
      render(TasksPage)

      const full = await screen.findByRole('article', { name: 'Full sync' })
      await user.click(within(full).getByRole('button', { name: 'Run now' }))
      expect(within(full).getByText(/about an hour/)).toBeInTheDocument()
      await user.click(within(full).getByRole('button', { name: 'Back' }))
      expect(api.requests('/api/admin/tasks/full/runs')).toHaveLength(0)

      const deletions = screen.getByRole('article', { name: 'Apply deletions' })
      await user.click(within(deletions).getByRole('button', { name: 'Apply 312 deletions' }))
      expect(within(deletions).getByRole('group')).toHaveTextContent('Removes 312 videos from TonTuyau')
      await user.click(within(deletions).getByRole('button', { name: 'Apply 312 deletions' }))
      await vi.waitFor(() => expect(api.requests('/api/admin/tasks/applyDeletions/runs')).toHaveLength(1))
    })

    it('one run at a time: the other buttons wait, this task’s run can be canceled (asked first)', async () => {
      const user = userEvent.setup()
      const running = run({ id: 'run-20261014T090000Z-bbbbbb', state: 'running', finishedAt: null })
      const busy = tasks({
        lock: { task: 'incremental', trigger: 'scheduler', since: running.startedAt },
        runningHere: running.id,
      })
      busy.tasks = busy.tasks.map((info) => (info.task === 'incremental' ? { ...info, lastRun: running } : info))
      const api = fakeApi({
        '/api/admin/tasks': busy,
        '/api/admin/runs': page([running]),
        [`/api/admin/runs/${running.id}/cancel`]: { ...running, cancellable: true },
      })
      render(TasksPage)

      const full = await screen.findByRole('article', { name: 'Full sync' })
      expect(within(full).getByRole('button', { name: 'Run now' })).toBeDisabled()
      expect(within(full).getByText('Incremental sync is going on: one run at a time.')).toBeInTheDocument()

      const incremental = screen.getByRole('article', { name: 'Incremental sync' })
      expect(within(incremental).getByRole('link', { name: 'Follow its log' })).toHaveAttribute(
        'href',
        `/admin/runs/${running.id}`,
      )
      await user.click(within(incremental).getByRole('button', { name: 'Cancel' }))
      await user.click(within(incremental).getByRole('button', { name: 'Cancel the run' }))
      await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${running.id}/cancel`)).toHaveLength(1))
    })

    it('a run going on in another process can only be stopped there', async () => {
      const running = run({ state: 'running', finishedAt: null, trigger: 'cli' })
      const busy = tasks({ lock: { task: 'incremental', trigger: 'cli', since: running.startedAt } })
      busy.tasks = busy.tasks.map((info) => (info.task === 'incremental' ? { ...info, lastRun: running } : info))
      fakeApi({ '/api/admin/tasks': busy, '/api/admin/runs': page([running]) })
      render(TasksPage)

      const incremental = await screen.findByRole('article', { name: 'Incremental sync' })
      expect(within(incremental).queryByRole('button', { name: 'Cancel' })).not.toBeInTheDocument()
      expect(within(incremental).getByText(/stop it there/)).toBeInTheDocument()
    })

    it('says why a start was refused', async () => {
      const user = userEvent.setup()
      fakeApi({
        '/api/admin/tasks': tasks(),
        '/api/admin/runs': page([run()]),
        '/api/admin/tasks/incremental/runs': problemResponse(409, 'A sync is already running.'),
      })
      render(TasksPage)

      const card = await screen.findByRole('article', { name: 'Incremental sync' })
      await user.click(within(card).getByRole('button', { name: 'Run now' }))
      expect(await screen.findByRole('alert')).toHaveTextContent('Not started: A sync is already running.')
      expect(location.pathname).toBe('/admin/tasks')
    })
  })
})
