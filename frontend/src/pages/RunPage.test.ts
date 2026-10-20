import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { RunLog, RunLogLine } from '../lib/api/types'
import { auth } from '../lib/auth.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { run } from '../test/data'
import { fakeApi, problemResponse } from '../test/fakeApi'
import RunPage from './RunPage.svelte'

const id = 'run-20261009T230000Z-d3a2c7'
const path = `/api/admin/runs/${id}`
const imported = run({
  id,
  startedAt: '2026-10-09T23:00:00Z',
  finishedAt: '2026-10-10T00:08:12Z',
  indexRebuild: true,
  notes: ['Index rebuild first: the index definitions changed since the indices were made.'],
})

function line(lineId: number, message: string, level: RunLogLine['level'] = 'information'): RunLogLine {
  return { id: lineId, at: '2026-10-09T23:00:00Z', level, message }
}

const log = (lines: RunLogLine[], complete = true): RunLog => ({ lines, complete })

/** The log's lines as shown: level and message. */
const shownLines = () =>
  within(screen.getByRole('log'))
    .queryAllByRole('listitem')
    .map((item) => `${item.querySelector('.level')?.textContent} ${item.querySelector('.message')?.textContent}`)

describe('RunPage', () => {
  it('shows what the run is, who started it, how long it ran, its counts and notes', async () => {
    fakeApi({ [path]: imported, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    expect(await screen.findByRole('heading', { name: 'TubeArchivist import', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('Succeeded')).toBeInTheDocument()
    expect(screen.getByText(id)).toBeInTheDocument()
    expect(screen.getByText('Startup')).toBeInTheDocument()
    expect(screen.getByText('1 h 08 min')).toBeInTheDocument()
    const result = screen.getByRole('region', { name: 'Result' })
    expect(within(result).getByText('Videos read').nextElementSibling).toHaveTextContent('61')
    expect(within(result).getByText('New').nextElementSibling).toHaveTextContent('8')
    expect(within(result).getByText('Already there').nextElementSibling).toHaveTextContent('50')
    const notes = screen.getByRole('region', { name: 'To know' })
    expect(within(notes).getByText(/^Index rebuild first/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tasks' })).toHaveAttribute('href', '/admin/tasks')
  })

  it('links a refresh to what it read on YouTube, and shows what YouTube answered', async () => {
    const channel = 'UCmw5b3sYz6v1sFkwM1HLs1g'
    const refresh = run({ task: 'refreshChannelVideos', target: channel })
    refresh.counts = {
      ...refresh.counts,
      channelsRead: 1,
      foundOnYouTube: 50,
      unlistedOnYouTube: 4,
      goneFromYouTube: 2,
      privateOnYouTube: 3,
      checkFailed: 10,
    }
    fakeApi({ [`/api/admin/runs/${refresh.id}`]: refresh, [`/api/admin/runs/${refresh.id}/log`]: log([]) })
    render(RunPage, { id: refresh.id })

    expect(
      await screen.findByRole('heading', { name: 'Refresh a channel and its videos', level: 1 }),
    ).toBeInTheDocument()
    const result = screen.getByRole('region', { name: 'Result' })
    expect(within(result).getByRole('link', { name: channel })).toHaveAttribute('href', `/channel/${channel}`)
    expect(within(result).getByText('Found on YouTube').nextElementSibling).toHaveTextContent('50')
    expect(within(result).getByText('Of which unlisted').nextElementSibling).toHaveTextContent('4')
    expect(within(result).getByText('Gone from YouTube').nextElementSibling).toHaveTextContent('2')
    expect(within(result).getByText('Private on YouTube').nextElementSibling).toHaveTextContent('3')
    expect(within(result).getByText('To retry (no answer to trust)').nextElementSibling).toHaveTextContent('10')
    expect(within(result).queryByText('Art copied')).not.toBeInTheDocument()
  })

  it('shows no counts for an index rebuild: it writes what PostgreSQL has', async () => {
    const rebuild = run({ task: 'indexRebuild', trigger: 'cli' })
    fakeApi({ [`/api/admin/runs/${rebuild.id}`]: rebuild, [`/api/admin/runs/${rebuild.id}/log`]: log([]) })
    render(RunPage, { id: rebuild.id })

    expect(await screen.findByRole('heading', { name: 'Index rebuild', level: 1 })).toBeInTheDocument()
    expect(screen.queryByText('Videos read')).not.toBeInTheDocument()
  })

  it('says why a run failed', async () => {
    const failed = run({ state: 'failed', error: 'TubeArchivist failed (502).' })
    fakeApi({ [`/api/admin/runs/${failed.id}`]: failed, [`/api/admin/runs/${failed.id}/log`]: log([]) })
    render(RunPage, { id: failed.id })

    const error = await screen.findByRole('region', { name: 'Why it failed' })
    expect(error).toHaveTextContent('TubeArchivist failed (502).')
  })

  it('shows the log oldest first, its warnings and errors alone on demand', async () => {
    const user = userEvent.setup()
    fakeApi({
      [path]: imported,
      [`${path}/log`]: log([
        line(1, 'Run started.'),
        line(2, '[00:02] channels      14 read'),
        line(3, '312 videos are gone, over the limit.', 'warning'),
        line(4, 'Run finished: Succeeded.'),
      ]),
    })
    render(RunPage, { id })

    await vi.waitFor(() => expect(shownLines()).toHaveLength(4))
    expect(shownLines()).toEqual([
      'INF Run started.',
      'INF [00:02] channels      14 read',
      'WRN 312 videos are gone, over the limit.',
      'INF Run finished: Succeeded.',
    ])
    await user.click(screen.getByRole('button', { name: 'Warnings and errors' }))
    expect(shownLines()).toEqual(['WRN 312 videos are gone, over the limit.'])
    expect(screen.queryByText('Running: new lines come in as the run writes them.')).not.toBeInTheDocument()
  })

  it('follows a run going on: its new lines after the last one, then its end', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const running = run({ state: 'running', finishedAt: null })
    let over = false
    const api = fakeApi({
      [`/api/admin/runs/${running.id}`]: () => (over ? run() : running),
      [`/api/admin/runs/${running.id}/log`]: (url: URL) =>
        url.searchParams.get('after') === '1'
          ? log(over ? [line(2, 'Run finished: Succeeded.')] : [], over)
          : log([line(1, 'Run started.')], false),
    })
    render(RunPage, { id: running.id })

    await vi.waitFor(() => expect(shownLines()).toEqual(['INF Run started.']))
    expect(screen.getByText('Running: new lines come in as the run writes them.')).toBeInTheDocument()
    expect(screen.getByText('Running')).toBeInTheDocument()

    over = true
    await vi.advanceTimersByTimeAsync(2_000)
    await vi.waitFor(() => expect(shownLines()).toHaveLength(2))
    await vi.waitFor(() => expect(screen.getByText('Succeeded')).toBeInTheDocument())
    expect(screen.queryByText('Running: new lines come in as the run writes them.')).not.toBeInTheDocument()
    expect(api.requests(`/api/admin/runs/${running.id}/log`).map((url) => url.search)).toEqual(['', '?after=1'])
    vi.useRealTimers()
  })

  it('says a run from before 1.4 has no log', async () => {
    fakeApi({ [path]: imported, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    expect(await screen.findByText('No log: this run is from before TonTuyau 1.4.')).toBeInTheDocument()
  })

  it('says when there is no such run', async () => {
    fakeApi({ [path]: problemResponse(404, `No run ${id}.`), [`${path}/log`]: problemResponse(404, 'No run.') })
    render(RunPage, { id })

    expect(await screen.findByText('No such run')).toBeInTheDocument()
  })

  it('copies the run’s id', async () => {
    // user-event's own clipboard: what the page writes can be read back.
    const user = userEvent.setup()
    fakeApi({ [path]: imported, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    await user.click(await screen.findByRole('button', { name: `Copy · ${id}` }))
    expect(await navigator.clipboard.readText()).toBe(id)
    expect(screen.getByRole('button', { name: `Copy · ${id}` })).toHaveTextContent('Copied')
  })

  it('speaks French', async () => {
    i18n.setLang('fr')
    fakeApi({ [path]: imported, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    expect(await screen.findByRole('heading', { name: 'Import de TubeArchivist', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('Réussie')).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Résultat' })).toBeInTheDocument()
    i18n.setLang('en')
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    fakeApi({
      '/api/auth/me': { username: 'admin' },
      [path]: problemResponse(401, 'Nobody is logged in.'),
      [`${path}/log`]: problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(RunPage, { id })

    await vi.waitFor(() => expect(auth.status).toBe('anonymous'))
  })

  it('cancels the run going on, asked first', async () => {
    const user = userEvent.setup()
    const running = run({ state: 'running', finishedAt: null, cancellable: true })
    const api = fakeApi({
      [`/api/admin/runs/${running.id}`]: running,
      [`/api/admin/runs/${running.id}/log`]: log([line(1, 'Run started.')], false),
      [`/api/admin/runs/${running.id}/cancel`]: running,
    })
    render(RunPage, { id: running.id })

    await user.click(await screen.findByRole('button', { name: 'Cancel the run' }))
    expect(screen.getByText(/It stops after its current step/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancel the run' }))
    await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${running.id}/cancel`)).toHaveLength(1))
  })

  it('a refresh on hold: when it resumes by itself, Resume now, and why a resume was refused', async () => {
    const user = userEvent.setup()
    const held = run({
      task: 'refreshVideo',
      state: 'onHold',
      target: 'alpha-02___',
      resumeAt: '2026-10-12T13:00:07Z',
      left: 1,
      cancellable: true,
      notes: ['On hold at 12:00:07 UTC, 1 left to read: YouTube refused (Sign in to confirm you’re not a bot).'],
    })
    const api = fakeApi({
      [`/api/admin/runs/${held.id}`]: held,
      [`/api/admin/runs/${held.id}/log`]: log([line(1, 'Refresh on hold.')]),
      [`/api/admin/runs/${held.id}/resume`]: problemResponse(409, 'A run is already going on.', 'Conflict'),
    })
    render(RunPage, { id: held.id })

    const bar = await screen.findByRole('region', { name: 'On hold' })
    expect(bar).toHaveTextContent('61 read, 1 left.')
    expect(within(bar).queryByRole('link')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancel the run' })).not.toBeInTheDocument()
    await user.click(within(bar).getByRole('button', { name: 'Resume now' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Not resumed: A run is already going on.')
    expect(api.requests(`/api/admin/runs/${held.id}/resume`)).toHaveLength(1)
  })

  it('puts a refresh going on on hold by hand, asked first; then it waits for Resume now', async () => {
    const user = userEvent.setup()
    const going = run({ task: 'refreshChannelVideos', state: 'running', finishedAt: null, cancellable: true })
    let held = false
    const api = fakeApi({
      [`/api/admin/runs/${going.id}`]: () =>
        held ? { ...going, state: 'onHold', finishedAt: '2026-10-12T12:00:07Z', resumeAt: null, left: 4 } : going,
      [`/api/admin/runs/${going.id}/log`]: log([line(1, 'Refresh started.')], false),
      [`/api/admin/runs/${going.id}/hold`]: () => {
        held = true
        return going
      },
    })
    render(RunPage, { id: going.id })

    await user.click(await screen.findByRole('button', { name: 'Put on hold' }))
    const ask = screen.getByRole('group', { name: 'Put this refresh on hold?' })
    expect(ask).toHaveTextContent('it never resumes by itself')
    await user.click(within(ask).getByRole('button', { name: 'Put on hold' }))

    const bar = await screen.findByRole('region', { name: 'On hold' })
    expect(bar).toHaveTextContent('61 read, 4 left. Put on hold by hand on')
    expect(bar).toHaveTextContent('it waits for you: Resume now, or cancel it.')
    expect(api.requests(`/api/admin/runs/${going.id}/hold`)).toHaveLength(1)
  })

  it('a download going on links to its video, and can be put on hold like a refresh', async () => {
    const going = run({
      task: 'downloadVideo',
      target: 'jNQXAC9IVRw',
      state: 'running',
      finishedAt: null,
      cancellable: true,
    })
    fakeApi({
      [`/api/admin/runs/${going.id}`]: going,
      [`/api/admin/runs/${going.id}/log`]: log([line(1, 'Downloading jNQXAC9IVRw.')], false),
    })
    render(RunPage, { id: going.id })

    expect(await screen.findByRole('heading', { name: 'Download a video', level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'jNQXAC9IVRw' })).toHaveAttribute('href', '/watch?v=jNQXAC9IVRw')
    expect(screen.getByRole('button', { name: 'Put on hold' })).toBeInTheDocument()
  })

  it('offers no hold for an import going on: only Cancel', async () => {
    const going = run({ state: 'running', finishedAt: null, cancellable: true })
    fakeApi({
      [`/api/admin/runs/${going.id}`]: going,
      [`/api/admin/runs/${going.id}/log`]: log([line(1, 'Run started.')], false),
    })
    render(RunPage, { id: going.id })

    expect(await screen.findByRole('button', { name: 'Cancel the run' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Put on hold' })).not.toBeInTheDocument()
  })

  it('has no Cancel for a run over, or going on elsewhere', async () => {
    fakeApi({ [path]: imported, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    await screen.findByRole('heading', { name: 'TubeArchivist import', level: 1 })
    expect(screen.queryByRole('button', { name: 'Cancel the run' })).not.toBeInTheDocument()
  })
})
