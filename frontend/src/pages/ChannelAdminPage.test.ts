import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import type { AdminChannel, AdminVideo } from '../lib/api/types'
import { auth } from '../lib/auth.svelte'
import { channelCard, page, run, tasks, videoCard } from '../test/data'
import { fakeApi, goTo, problemResponse } from '../test/fakeApi'
import ChannelAdminPage from './ChannelAdminPage.svelte'

const id = 'UCtest'
const base = `/api/admin/channels/${id}`

const channel = (overrides: Partial<AdminChannel> = {}): AdminChannel => ({
  channel: channelCard(),
  youTube: { state: 'onYouTube', checkedAt: '2026-10-12T08:00:00Z', reason: null, refreshedAt: '2026-10-12T08:00:00Z' },
  videos: { notChecked: 4, onYouTube: 100, unlisted: 6, private: 2, retryLater: 9, gone: 21 },
  subscription: null,
  ...overrides,
})

const row = (videoId: string, title: string, youTube: AdminVideo['youTube']): AdminVideo => ({
  video: videoCard({ id: videoId, title }),
  youTube,
})

const gone = row('gone0000001', 'Le poulet rôti', {
  state: 'gone',
  checkedAt: '2026-10-02T08:00:00Z',
  reason: 'Video unavailable. This video has been removed by the uploader',
  refreshedAt: null,
})
const never = row('never000001', 'Crème brûlée', {
  state: 'notChecked',
  checkedAt: null,
  reason: null,
  refreshedAt: null,
})

/** The page's API, every part answered unless given. */
function routes(given: Record<string, unknown> = {}) {
  return fakeApi({
    [base]: channel(),
    [`${base}/videos`]: page([never, gone], 134),
    '/api/admin/tasks': tasks(),
    ...given,
  })
}

describe('ChannelAdminPage', () => {
  beforeEach(() => goTo(`/admin/channels/${id}`))

  it('shows the channel on YouTube, its videos by state, and each video with its own', async () => {
    routes()
    render(ChannelAdminPage, { id })

    expect(await screen.findByRole('heading', { name: 'Tom Cooks', level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open on YouTube' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/channel/UCtest',
    )
    expect(screen.getByRole('link', { name: 'Open in TonTuyau' })).toHaveAttribute('href', '/channel/UCtest')
    const onYouTube = screen.getByRole('region', { name: 'The channel on YouTube' })
    expect(within(onYouTube).getByText('On YouTube')).toBeInTheDocument()
    const states = screen.getByRole('region', { name: 'Its videos on YouTube' })
    expect(within(states).getByText('Gone').closest('div')).toHaveTextContent('21')

    const table = await screen.findByRole('table')
    const [, first, second] = within(table).getAllByRole('row')
    expect(first).toHaveTextContent('Crème brûlée')
    expect(first).toHaveTextContent('Not checked')
    expect(first).toHaveTextContent('never')
    expect(second).toHaveTextContent('Gone')
    expect(second).toHaveTextContent('removed by the uploader')
    expect(screen.getByText('134 videos')).toBeInTheDocument()
  })

  it('shows one state from the URL, and the chips link to each state', async () => {
    goTo(`/admin/channels/${id}?state=gone`)
    const api = routes()
    render(ChannelAdminPage, { id })

    await screen.findByRole('table')
    expect(api.requests(`${base}/videos`)[0]?.searchParams.get('state')).toBe('gone')
    const chips = screen.getByRole('navigation', { name: 'State on YouTube' })
    expect(within(chips).getByRole('link', { name: 'Retry later 9' })).toHaveAttribute(
      'href',
      '/admin/channels/UCtest?state=retryLater',
    )
    expect(within(chips).getByRole('link', { name: 'Private 2' })).toHaveAttribute(
      'href',
      '/admin/channels/UCtest?state=private',
    )
    expect(within(chips).getByRole('link', { name: 'All 142' })).toHaveAttribute('href', '/admin/channels/UCtest')
  })

  it('searches its videos by words, once typing pauses', async () => {
    const user = userEvent.setup()
    const api = routes()
    render(ChannelAdminPage, { id })

    await user.type(await screen.findByRole('searchbox', { name: "Search this channel's videos" }), 'poulet')

    await vi.waitFor(() =>
      expect(api.requests(`${base}/videos`).some((url) => url.searchParams.get('q') === 'poulet')).toBe(true),
    )
  })

  it('refreshes a video, the channel, or the channel and its videos: one task at a time', async () => {
    const user = userEvent.setup()
    let busy = false
    const api = routes({
      '/api/admin/tasks': () =>
        busy
          ? tasks({
              lock: { task: 'refreshVideo', trigger: 'admin', since: new Date().toISOString() },
              runningHere: 'run-20261016T090000Z-aaaaaa',
            })
          : tasks(),
      '/api/admin/tasks/refreshVideo/runs': () => {
        busy = true
        return run({ task: 'refreshVideo', state: 'running', finishedAt: null, target: never.video.id })
      },
    })
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Refresh Crème brûlée' }))

    expect(api.requests('/api/admin/tasks/refreshVideo/runs')[0]?.searchParams.get('target')).toBe('never000001')
    expect(await screen.findByRole('status')).toHaveTextContent(/Refresh a video is going on/)
    expect(screen.getByRole('link', { name: 'Follow its log' })).toHaveAttribute(
      'href',
      '/admin/runs/run-20261016T090000Z-aaaaaa',
    )
    expect(screen.getByRole('button', { name: 'Refresh the channel' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Refresh Crème brûlée' })).toBeDisabled()
  })

  it("shows each state as its coloured badge; a gone video can't be refreshed", async () => {
    routes()
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const goneRow = within(table).getByText('Le poulet rôti', { selector: 'b' }).closest('tr') as HTMLElement
    expect(within(goneRow).getByText('Gone')).toHaveClass('state', 'gone')
    expect(within(goneRow).getByRole('button', { name: 'Refresh Le poulet rôti' })).toBeDisabled()
    expect(goneRow).toHaveTextContent('gone: not refreshed')
    expect(within(table).getByRole('button', { name: 'Refresh Crème brûlée' })).toBeEnabled()
    const states = screen.getByRole('region', { name: 'Its videos on YouTube' })
    expect(within(states).getByText('Private')).toHaveClass('state', 'private')
    expect(within(states).getByText('Unlisted').closest('div')).toHaveTextContent('6')
  })

  it('subscribes to a channel with the tabs ticked, then shows its subscription', async () => {
    const user = userEvent.setup()
    const subscribed = {
      tabs: { videos: true, shorts: false, streams: true },
      since: '2026-10-20T09:00:00Z',
      by: 'claire',
      origin: 'tonTuyau' as const,
    }
    let sent: unknown = null
    let read = 0
    routes({
      [base]: () => (read++ === 0 ? channel() : channel({ subscription: subscribed })),
      [`${base}/subscription`]: (_url: URL, init?: RequestInit) => {
        sent = { method: init?.method, body: JSON.parse(String(init?.body)) }
        return subscribed
      },
    })
    render(ChannelAdminPage, { id })

    const box = await screen.findByRole('region', { name: 'Subscription' })
    expect(box).toHaveTextContent('Not subscribed')
    expect(within(box).queryByRole('button', { name: 'Unsubscribe' })).not.toBeInTheDocument()
    await user.click(within(box).getByRole('checkbox', { name: 'Shorts' }))
    await user.click(within(box).getByRole('button', { name: 'Subscribe' }))

    await vi.waitFor(() => expect(box).toHaveTextContent('by claire'))
    expect(sent).toEqual({ method: 'PUT', body: { videos: true, shorts: false, streams: true } })
    expect(within(box).getByRole('checkbox', { name: 'Shorts' })).not.toBeChecked()
    expect(within(box).getByRole('button', { name: 'Save the tabs' })).toBeDisabled()
    expect(within(box).getByRole('button', { name: 'Unsubscribe' })).toBeEnabled()
  })

  it("says a subscription taken from TubeArchivist, unsubscribes, and won't save no tab", async () => {
    const user = userEvent.setup()
    let method: string | undefined
    routes({
      [base]: channel({
        subscription: {
          tabs: { videos: true, shorts: true, streams: true },
          since: '2026-10-18T09:00:00Z',
          by: null,
          origin: 'tubeArchivist',
        },
      }),
      [`${base}/subscription`]: (_url: URL, init?: RequestInit) => {
        method = init?.method
        return new Response(null, { status: 204 })
      },
    })
    render(ChannelAdminPage, { id })

    const box = await screen.findByRole('region', { name: 'Subscription' })
    expect(box).toHaveTextContent('taken from TubeArchivist')
    for (const tab of ['Videos', 'Shorts', 'Streams']) {
      await user.click(within(box).getByRole('checkbox', { name: tab }))
    }
    expect(within(box).getByRole('alert')).toHaveTextContent('Tick at least one tab.')
    expect(within(box).getByRole('button', { name: 'Save the tabs' })).toBeDisabled()

    await user.click(within(box).getByRole('button', { name: 'Unsubscribe' }))
    await vi.waitFor(() => expect(method).toBe('DELETE'))
  })

  it('downloads everything of a subscribed channel, asked first, then opens its run', async () => {
    const user = userEvent.setup()
    const started = run({ id: 'run-20261020T190000Z-eeeeee', task: 'youTubeFullSync', state: 'queued' })
    const api = routes({
      [base]: channel({
        subscription: {
          tabs: { videos: true, shorts: false, streams: false },
          since: '2026-10-20T09:00:00Z',
          by: 'claire',
          origin: 'tonTuyau',
        },
      }),
      '/api/admin/tasks/youTubeFullSync/runs': started,
    })
    render(ChannelAdminPage, { id })

    const box = await screen.findByRole('region', { name: 'Subscription' })
    await user.click(within(box).getByRole('button', { name: 'Download everything' }))
    const confirm = within(box).getByRole('group', { name: 'Download everything' })
    expect(confirm).toHaveTextContent(/up to hours for a big channel/)
    expect(api.requests('/api/admin/tasks/youTubeFullSync/runs')).toEqual([])

    await user.click(within(confirm).getByRole('button', { name: 'Download everything' }))
    await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
    expect(api.requests('/api/admin/tasks/youTubeFullSync/runs')[0]?.searchParams.get('target')).toBe(id)
  })

  it('lists the failed downloads with why, and tries one or all of them again', async () => {
    const user = userEvent.setup()
    const asked = run({ id: 'run-20261020T200000Z-ffffff', task: 'downloadVideo', state: 'queued' })
    const api = routes({
      [`${base}/failed-downloads`]: [
        {
          videoId: 'fail0000001',
          title: 'Le soufflé',
          reason: 'Requested format is not available. Use --list-formats for a list of available formats',
          tries: 3,
          skipped: true,
          firstFailedAt: '2026-10-19T08:00:00Z',
          lastFailedAt: '2026-10-20T08:00:00Z',
          youTubeUrl: 'https://www.youtube.com/watch?v=fail0000001',
        },
      ],
      '/api/admin/tasks/downloadVideo/runs': asked,
      '/api/admin/tasks/retryDownloads/runs': { ...asked, task: 'retryDownloads' },
    })
    render(ChannelAdminPage, { id })

    const failed = await screen.findByRole('region', { name: 'Failed downloads · 1' })
    const row = within(failed).getAllByRole('row')[1] as HTMLElement
    expect(row).toHaveTextContent('Le soufflé')
    expect(row).toHaveTextContent('Requested format is not available')
    expect(row).toHaveTextContent('3 failures')
    expect(row).toHaveTextContent('Left by the sync')
    expect(within(row).getByRole('link', { name: 'Le soufflé' })).toHaveAttribute(
      'href',
      'https://www.youtube.com/watch?v=fail0000001',
    )

    await user.click(within(row).getByRole('button', { name: /^Try again/ }))
    expect(await within(failed).findByRole('status')).toHaveTextContent('Asked: it runs when its turn comes.')
    expect(api.requests('/api/admin/tasks/downloadVideo/runs')[0]?.searchParams.get('target')).toBe('fail0000001')

    await user.click(within(failed).getByRole('button', { name: 'Try them all again' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/retryDownloads/runs')).toHaveLength(1))
    expect(api.requests('/api/admin/tasks/retryDownloads/runs')[0]?.searchParams.get('target')).toBe(id)
  })

  it('says why a refresh was refused', async () => {
    const user = userEvent.setup()
    routes({
      '/api/admin/tasks/refreshChannelVideos/runs': problemResponse(409, 'A run is already going on.', 'Conflict'),
    })
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Refresh the channel and its videos' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Not started: A run is already going on.')
  })

  it('says when the channel is not in TonTuyau', async () => {
    routes({ [base]: problemResponse(404, "There's no channel with the id 'UCtest'.", 'Not found') })
    render(ChannelAdminPage, { id })

    expect(await screen.findByText(/no channel with the id/)).toBeInTheDocument()
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    const expired = vi.spyOn(auth, 'expired')
    routes({ [base]: problemResponse(401, 'Nobody is logged in.', 'Unauthorized') })
    render(ChannelAdminPage, { id })

    await vi.waitFor(() => expect(expired).toHaveBeenCalled())
  })
})
