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
const blocked = run({
  id,
  task: 'full',
  startedAt: '2026-10-09T23:00:00Z',
  finishedAt: '2026-10-10T00:08:12Z',
  deletionsPlanned: 312,
  deletionsBlocked: true,
  notes: ['312 deletions planned > limit 100: nothing deleted.\nCheck the list, then apply it.'],
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
    fakeApi({ [path]: blocked, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    expect(await screen.findByRole('heading', { name: 'Full sync', level: 1 })).toBeInTheDocument()
    expect(screen.getByText('Succeeded')).toBeInTheDocument()
    expect(screen.getByText(id)).toBeInTheDocument()
    expect(screen.getByText('Schedule')).toBeInTheDocument()
    expect(screen.getByText('1 h 08 min')).toBeInTheDocument()
    const result = screen.getByRole('region', { name: 'Result' })
    expect(within(result).getByText('Deletions waiting').nextElementSibling).toHaveTextContent('312')
    expect(within(result).getByText('Videos read').nextElementSibling).toHaveTextContent('61')
    const notes = screen.getByRole('region', { name: 'To know' })
    expect(within(notes).getByText(/312 deletions planned/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tasks' })).toHaveAttribute('href', '/admin/tasks')
  })

  it('links a refresh to what it read on YouTube, and shows what YouTube answered', async () => {
    const channel = 'UCmw5b3sYz6v1sFkwM1HLs1g'
    const refresh = run({ task: 'refreshChannelVideos', target: channel })
    refresh.counts = { ...refresh.counts, channelsRead: 1, foundOnYouTube: 50, goneFromYouTube: 2, checkFailed: 10 }
    fakeApi({ [`/api/admin/runs/${refresh.id}`]: refresh, [`/api/admin/runs/${refresh.id}/log`]: log([]) })
    render(RunPage, { id: refresh.id })

    expect(
      await screen.findByRole('heading', { name: 'Refresh a channel and its videos', level: 1 }),
    ).toBeInTheDocument()
    const result = screen.getByRole('region', { name: 'Result' })
    expect(within(result).getByRole('link', { name: channel })).toHaveAttribute('href', `/channel/${channel}`)
    expect(within(result).getByText('Found on YouTube').nextElementSibling).toHaveTextContent('50')
    expect(within(result).getByText('Gone from YouTube').nextElementSibling).toHaveTextContent('2')
    expect(within(result).getByText('To retry (no answer to trust)').nextElementSibling).toHaveTextContent('10')
    expect(within(result).queryByText('Art copied')).not.toBeInTheDocument()
  })

  it('links an apply run to the full run whose list it applied, and shows its own counts', async () => {
    const apply = run({ task: 'applyDeletions', appliesTo: id, deletionsPlanned: 312 })
    apply.counts = { ...apply.counts, deleted: 303, kept: 9 }
    fakeApi({ [`/api/admin/runs/${apply.id}`]: apply, [`/api/admin/runs/${apply.id}/log`]: log([]) })
    render(RunPage, { id: apply.id })

    const result = await screen.findByRole('region', { name: 'Result' })
    expect(within(result).getByRole('link', { name: id })).toHaveAttribute('href', `/admin/runs/${id}`)
    expect(within(result).getByText('To delete').nextElementSibling).toHaveTextContent('312')
    expect(within(result).getByText('Kept (still in TubeArchivist)').nextElementSibling).toHaveTextContent('9')
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
      [path]: blocked,
      [`${path}/log`]: log([
        line(1, 'Sync run started.'),
        line(2, '[00:02] channels      14 read'),
        line(3, '312 videos are gone, over the limit.', 'warning'),
        line(4, 'Sync run finished: Succeeded.'),
      ]),
    })
    render(RunPage, { id })

    await vi.waitFor(() => expect(shownLines()).toHaveLength(4))
    expect(shownLines()).toEqual([
      'INF Sync run started.',
      'INF [00:02] channels      14 read',
      'WRN 312 videos are gone, over the limit.',
      'INF Sync run finished: Succeeded.',
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
          ? log(over ? [line(2, 'Sync run finished: Succeeded.')] : [], over)
          : log([line(1, 'Sync run started.')], false),
    })
    render(RunPage, { id: running.id })

    await vi.waitFor(() => expect(shownLines()).toEqual(['INF Sync run started.']))
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
    fakeApi({ [path]: blocked, [`${path}/log`]: log([]) })
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
    fakeApi({ [path]: blocked, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    await user.click(await screen.findByRole('button', { name: `Copy · ${id}` }))
    expect(await navigator.clipboard.readText()).toBe(id)
    expect(screen.getByRole('button', { name: `Copy · ${id}` })).toHaveTextContent('Copied')
  })

  it('speaks French', async () => {
    i18n.setLang('fr')
    fakeApi({ [path]: blocked, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    expect(await screen.findByRole('heading', { name: 'Synchronisation complète', level: 1 })).toBeInTheDocument()
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
      [`/api/admin/runs/${running.id}/log`]: log([line(1, 'Sync run started.')], false),
      [`/api/admin/runs/${running.id}/cancel`]: running,
    })
    render(RunPage, { id: running.id })

    await user.click(await screen.findByRole('button', { name: 'Cancel the run' }))
    expect(screen.getByText(/It stops after its current step/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancel the run' }))
    await vi.waitFor(() => expect(api.requests(`/api/admin/runs/${running.id}/cancel`)).toHaveLength(1))
  })

  it('has no Cancel for a run over, or going on elsewhere', async () => {
    fakeApi({ [path]: blocked, [`${path}/log`]: log([]) })
    render(RunPage, { id })

    await screen.findByRole('heading', { name: 'Full sync', level: 1 })
    expect(screen.queryByRole('button', { name: 'Cancel the run' })).not.toBeInTheDocument()
  })
})
