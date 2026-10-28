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
  syncBlocked: 0,
  subscription: null,
  size: { videos: 125_500_000_000, shorts: 0, streams: 2_300_000_000 },
  ...overrides,
})

const row = (videoId: string, title: string, youTube: AdminVideo['youTube'], syncBlocked = false): AdminVideo => ({
  video: videoCard({ id: videoId, title }),
  youTube,
  syncBlocked,
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

  it('says the size of its files, in all and by the types it has files of (2.2.0)', async () => {
    routes()
    render(ChannelAdminPage, { id })

    const size = (await screen.findByText('Size', { selector: 'dt' })).nextElementSibling
    expect(size).toHaveTextContent('127.8 GB')
    expect(size).toHaveTextContent('Videos 125.5 GB · Streams 2.3 GB')
    expect(size).not.toHaveTextContent('Shorts')
  })

  it('keeps the explanations behind each zone\'s "?"', async () => {
    const user = userEvent.setup()
    routes()
    render(ChannelAdminPage, { id })

    const help = await screen.findByRole('button', { name: 'Help: Its videos on YouTube' })
    const text = screen.getByText(/"Retry later": the last check had no answer to trust/)
    expect(text).not.toBeVisible()
    await user.click(help)
    expect(text).toBeVisible()
    expect(screen.getByRole('button', { name: 'Help: The channel on YouTube' })).toHaveAttribute(
      'aria-expanded',
      'false',
    )
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

  it('refreshes a video, the channel, or the channel and its videos: while one goes on, the next waits its turn', async () => {
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
      '/api/admin/tasks/refreshChannel/runs': () =>
        run({ task: 'refreshChannel', state: 'queued', finishedAt: null, target: id }),
    })
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Refresh the metadata of “Crème brûlée”' }))

    expect(api.requests('/api/admin/tasks/refreshVideo/runs')[0]?.searchParams.get('target')).toBe('never000001')
    expect(await screen.findByRole('status')).toHaveTextContent(/Refresh a video is going on/)
    expect(screen.getByRole('link', { name: 'Follow its log' })).toHaveAttribute(
      'href',
      '/admin/runs/run-20261016T090000Z-aaaaaa',
    )
    expect(screen.queryByText(/Added to the queue/)).not.toBeInTheDocument()
    expect(screen.getByText('A run is going on: this one waits its turn.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Refresh the metadata of “Crème brûlée”' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'Refresh the channel' }))

    expect(api.requests('/api/admin/tasks/refreshChannel/runs')[0]?.searchParams.get('target')).toBe(id)
    expect(await screen.findByText('Added to the queue: "Refresh a channel" waits its turn.')).toBeInTheDocument()
  })

  it("shows each state as its coloured badge; a gone video can't be refreshed", async () => {
    routes()
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const goneRow = within(table).getByText('Le poulet rôti', { selector: 'b' }).closest('tr') as HTMLElement
    expect(within(goneRow).getByText('Gone')).toHaveClass('state', 'gone')
    expect(within(goneRow).getByRole('button', { name: 'Refresh the metadata of “Le poulet rôti”' })).toBeDisabled()
    expect(goneRow).toHaveTextContent('gone: not refreshed')
    expect(within(table).getByRole('button', { name: 'Refresh the metadata of “Crème brûlée”' })).toBeEnabled()
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

  const subscribed = () =>
    channel({
      subscription: {
        tabs: { videos: true, shorts: false, streams: false },
        since: '2026-10-20T09:00:00Z',
        by: 'claire',
        origin: 'tonTuyau',
      },
    })

  it('downloads everything of a subscribed channel, asked first in a dialog, then opens its run', async () => {
    const user = userEvent.setup()
    const started = run({ id: 'run-20261020T190000Z-eeeeee', task: 'youTubeFullSync', state: 'queued' })
    const api = routes({
      [base]: subscribed(),
      '/api/admin/youtube/settings': { downloadFormat: 'bestvideo+bestaudio', retryWithoutCookie: true },
      '/api/admin/tasks/youTubeFullSync/runs': started,
    })
    render(ChannelAdminPage, { id })

    const box = await screen.findByRole('region', { name: 'Subscription' })
    await user.click(within(box).getByRole('button', { name: 'Download everything' }))
    const dialog = await screen.findByRole('dialog', { name: 'Download everything' })
    expect(dialog).toHaveTextContent(/up to hours for a big channel/)
    // Filled in with the YouTube page's settings.
    const field = within(dialog).getByRole('textbox', { name: 'yt-dlp format for this download' })
    await vi.waitFor(() => expect(field).toHaveValue('bestvideo+bestaudio'))
    expect(within(dialog).getByRole('checkbox', { name: /try again without it/ })).toBeChecked()
    expect(api.requests('/api/admin/tasks/youTubeFullSync/runs')).toEqual([])

    // Left as they are: nothing more is asked than the channel.
    await user.click(within(dialog).getByRole('button', { name: 'Download everything' }))
    await vi.waitFor(() => expect(location.pathname).toBe(`/admin/runs/${started.id}`))
    const sent = api.requests('/api/admin/tasks/youTubeFullSync/runs')[0]?.searchParams
    expect(sent?.get('target')).toBe(id)
    expect(sent?.has('format')).toBe(false)
    expect(sent?.has('retryWithoutCookie')).toBe(false)
  })

  it('downloads everything with another format and without the retry without the cookie, this once (2.3.0)', async () => {
    const user = userEvent.setup()
    const started = run({ id: 'run-20261020T190000Z-dddddd', task: 'youTubeFullSync', state: 'queued' })
    const api = routes({
      [base]: subscribed(),
      '/api/admin/youtube/settings': { downloadFormat: 'bestvideo+bestaudio', retryWithoutCookie: true },
      '/api/admin/tasks/youTubeFullSync/runs': started,
    })
    render(ChannelAdminPage, { id })

    const box = await screen.findByRole('region', { name: 'Subscription' })
    await user.click(within(box).getByRole('button', { name: 'Download everything' }))
    const dialog = await screen.findByRole('dialog', { name: 'Download everything' })
    const field = within(dialog).getByRole('textbox', { name: 'yt-dlp format for this download' })
    await vi.waitFor(() => expect(field).toHaveValue('bestvideo+bestaudio'))
    await user.clear(field)
    await user.type(field, ' 398+251 ')
    await user.click(within(dialog).getByRole('checkbox', { name: /try again without it/ }))

    await user.click(within(dialog).getByRole('button', { name: 'Download everything' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/youTubeFullSync/runs')).toHaveLength(1))
    const sent = api.requests('/api/admin/tasks/youTubeFullSync/runs')[0]?.searchParams
    expect(sent?.get('format')).toBe('398+251')
    expect(sent?.get('retryWithoutCookie')).toBe('false')
  })

  it('purges one video from its row, asked first, then reads the list again (2.3.0)', async () => {
    const user = userEvent.setup()
    const purged = run({
      id: 'run-20261020T210000Z-pppppp',
      task: 'purgeVideos',
      state: 'succeeded',
      counts: { ...run().counts, purged: 1 },
    })
    const api = routes({ '/api/admin/tasks/purgeVideos/runs': purged })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const first = within(table).getAllByRole('row')[1] as HTMLElement
    await user.click(within(first).getByRole('button', { name: 'Purge “Crème brûlée”' }))
    const dialog = await screen.findByRole('dialog', { name: 'Purge “Crème brûlée”?' })
    expect(dialog).toHaveTextContent("The channel's next sync downloads them again.")
    expect(within(dialog).getByRole('radio', { name: /^Everything/ })).toBeChecked()
    expect(api.requests('/api/admin/tasks/purgeVideos/runs')).toEqual([])

    await user.click(within(dialog).getByRole('button', { name: 'Purge' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/purgeVideos/runs')).toHaveLength(1))
    const sent = api.requests('/api/admin/tasks/purgeVideos/runs')[0]?.searchParams
    expect(sent?.get('target')).toBe(id)
    expect(sent?.get('videos')).toBe('never000001')
    expect(sent?.has('filesOnly')).toBe(false)
    expect(await screen.findByText('1 video purged')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Follow its log' })).toHaveAttribute('href', `/admin/runs/${purged.id}`)
    await vi.waitFor(() => expect(api.requests(`${base}/videos`).length).toBeGreaterThan(1))
  })

  it('purges the files only when asked: the videos kept, their syncs blocked (2.3.0)', async () => {
    const user = userEvent.setup()
    const purged = run({ id: 'run-20261020T210000Z-pppppp', task: 'purgeVideos', state: 'succeeded' })
    const api = routes({ '/api/admin/tasks/purgeVideos/runs': purged })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const first = within(table).getAllByRole('row')[1] as HTMLElement
    await user.click(within(first).getByRole('button', { name: 'Purge “Crème brûlée”' }))
    const dialog = await screen.findByRole('dialog', { name: 'Purge “Crème brûlée”?' })
    await user.click(within(dialog).getByRole('radio', { name: /^The files only/ }))
    await user.click(within(dialog).getByRole('button', { name: 'Purge' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/purgeVideos/runs')).toHaveLength(1))
    expect(api.requests('/api/admin/tasks/purgeVideos/runs')[0]?.searchParams.get('filesOnly')).toBe('true')
  })

  it('greys the videos whose syncs are blocked, each to unblock, or the ones ticked (2.3.0)', async () => {
    const user = userEvent.setup()
    const blocked = row('block000001', 'Le flan', never.youTube, true)
    const api = routes({
      [base]: { ...channel(), syncBlocked: 1 },
      [`${base}/videos`]: page([blocked, never, gone], 3),
      [`${base}/blocked-videos`]: () => new Response(null, { status: 204 }),
    })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const [, first, second] = within(table).getAllByRole('row') as HTMLElement[]
    expect(first).toHaveClass('blocked')
    expect(first).toHaveTextContent('Sync blocked')
    expect(within(first as HTMLElement).queryByRole('link', { name: /Le flan/ })).toBeNull()
    expect(second).not.toHaveClass('blocked')
    expect(within(second as HTMLElement).queryByRole('button', { name: /^Unblock/ })).toBeNull()

    await user.click(within(first as HTMLElement).getByRole('button', { name: 'Unblock “Le flan”' }))
    await vi.waitFor(() => expect(api.requests(`${base}/blocked-videos`)).toHaveLength(1))
    expect(api.requests(`${base}/blocked-videos`)[0]?.searchParams.get('videos')).toBe('block000001')
    await vi.waitFor(() => expect(api.requests(`${base}/videos`).length).toBeGreaterThan(1))

    const chip = screen.getByRole('link', { name: 'Blocked 1' })
    expect(chip).toHaveAttribute('href', `/admin/channels/${id}?state=blocked`)

    await user.click(screen.getByRole('button', { name: 'Select' }))
    expect(screen.queryByRole('button', { name: 'Unblock' })).toBeNull()
    const again = within(await screen.findByRole('table')).getAllByRole('row')[1] as HTMLElement
    await user.click(within(again).getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Unblock' }))
    await vi.waitFor(() => expect(api.requests(`${base}/blocked-videos`)).toHaveLength(2))
  })

  it('unticks the videos when another list is shown: none acted on unseen (2.3.0)', async () => {
    const user = userEvent.setup()
    routes()
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Select' }))
    const first = within(await screen.findByRole('table')).getAllByRole('row')[1] as HTMLElement
    await user.click(within(first).getByRole('checkbox'))
    expect(screen.getByText('1 selected')).toBeInTheDocument()

    await user.type(screen.getByRole('searchbox', { name: "Search this channel's videos" }), 'poulet')
    await vi.waitFor(() => expect(screen.getByText('0 selected')).toBeInTheDocument())
  })

  it('selects videos to purge them together, or purges the whole channel (2.3.0)', async () => {
    const user = userEvent.setup()
    const purged = run({ id: 'run-20261020T210000Z-qqqqqq', task: 'purgeVideos', state: 'succeeded' })
    const api = routes({ '/api/admin/tasks/purgeVideos/runs': purged })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    expect(within(table).queryByRole('checkbox')).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Select' }))
    const bar = screen.getByRole('group', { name: 'Select' })
    expect(bar).toHaveTextContent('0 selected')
    expect(within(bar).getByRole('button', { name: 'Purge' })).toBeDisabled()

    await user.click(within(table).getByRole('checkbox', { name: 'Select “Le poulet rôti”' }))
    expect(bar).toHaveTextContent('1 selected')
    await user.click(within(table).getByRole('checkbox', { name: 'Select all (shown)' }))
    expect(bar).toHaveTextContent('2 selected')
    await user.click(within(table).getByRole('checkbox', { name: 'Select “Crème brûlée”' }))
    await user.click(within(bar).getByRole('button', { name: 'Purge' }))
    const dialog = await screen.findByRole('dialog', { name: 'Purge 1 video?' })
    await user.click(within(dialog).getByRole('button', { name: 'Purge' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/purgeVideos/runs')).toHaveLength(1))
    expect(api.requests('/api/admin/tasks/purgeVideos/runs')[0]?.searchParams.get('videos')).toBe('gone0000001')
    // Asked: the boxes go.
    await vi.waitFor(() => expect(screen.queryByRole('group', { name: 'Select' })).toBeNull())

    await user.click(screen.getByRole('button', { name: 'Select' }))
    await user.click(
      within(screen.getByRole('group', { name: 'Select' })).getByRole('button', { name: 'Purge the whole channel' }),
    )
    const whole = await screen.findByRole('dialog', { name: 'Purge the whole channel?' })
    expect(whole).toHaveTextContent('142 videos, 127.8 GB on disk.')
    await user.click(within(whole).getByRole('button', { name: 'Purge' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/purgeVideos/runs')).toHaveLength(2))
    const sent = api.requests('/api/admin/tasks/purgeVideos/runs')[1]?.searchParams
    expect(sent?.get('target')).toBe(id)
    expect(sent?.has('videos')).toBe(false)
  })

  it('refreshes the videos ticked, and downloads them again with a format asked in a dialog (2.3.0)', async () => {
    const user = userEvent.setup()
    const refreshed = run({ id: 'run-20261020T220000Z-rrrrrr', task: 'refreshVideos', state: 'succeeded' })
    const again = run({
      id: 'run-20261020T220000Z-ssssss',
      task: 'redownloadVideos',
      state: 'succeeded',
      counts: { ...run().counts, updated: 2 },
    })
    const api = routes({
      '/api/admin/youtube/settings': { downloadFormat: 'bestvideo+bestaudio', retryWithoutCookie: true },
      '/api/admin/tasks/refreshVideos/runs': refreshed,
      '/api/admin/tasks/redownloadVideos/runs': again,
    })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    await user.click(screen.getByRole('button', { name: 'Select' }))
    const bar = screen.getByRole('group', { name: 'Select' })
    expect(within(bar).getByRole('button', { name: 'Refresh metadata' })).toBeDisabled()
    await user.click(within(table).getByRole('checkbox', { name: 'Select all (shown)' }))
    await user.click(within(bar).getByRole('button', { name: 'Refresh metadata' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/refreshVideos/runs')).toHaveLength(1))
    const asked = api.requests('/api/admin/tasks/refreshVideos/runs')[0]?.searchParams
    expect(asked?.get('target')).toBe(id)
    expect(asked?.get('videos')?.split(',').sort()).toEqual(['gone0000001', 'never000001'])

    // The list read again once it's over.
    await vi.waitFor(() => expect(api.requests(`${base}/videos`).length).toBeGreaterThan(1))
    await user.click(await screen.findByRole('button', { name: 'Select' }))
    await user.click(within(await screen.findByRole('table')).getByRole('checkbox', { name: 'Select all (shown)' }))
    await user.click(
      within(screen.getByRole('group', { name: 'Select' })).getByRole('button', { name: 'Download again' }),
    )
    const dialog = await screen.findByRole('dialog', { name: 'Download 2 videos again?' })
    const field = within(dialog).getByRole('textbox', { name: 'yt-dlp format for this download' })
    await vi.waitFor(() => expect(field).toHaveValue('bestvideo+bestaudio'))
    await user.clear(field)
    await user.type(field, '398+251')
    await user.click(within(dialog).getByRole('button', { name: 'Download again' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/redownloadVideos/runs')).toHaveLength(1))
    const sent = api.requests('/api/admin/tasks/redownloadVideos/runs')[0]?.searchParams
    expect(sent?.get('format')).toBe('398+251')
    expect(sent?.has('retryWithoutCookie')).toBe(false)
    expect(await screen.findByText('2 downloaded again, 0 failed')).toBeInTheDocument()
  })

  it('downloads one video again from its row, the settings as they are (2.3.0)', async () => {
    const user = userEvent.setup()
    const again = run({ id: 'run-20261020T230000Z-tttttt', task: 'redownloadVideos', state: 'queued' })
    const api = routes({
      '/api/admin/youtube/settings': { downloadFormat: 'bestvideo+bestaudio', retryWithoutCookie: true },
      '/api/admin/tasks/redownloadVideos/runs': again,
    })
    render(ChannelAdminPage, { id })

    const table = await screen.findByRole('table')
    const first = within(table).getAllByRole('row')[1] as HTMLElement
    await user.click(within(first).getByRole('button', { name: 'Download “Crème brûlée” again' }))
    const dialog = await screen.findByRole('dialog', { name: 'Download “Crème brûlée” again?' })
    await vi.waitFor(() =>
      expect(within(dialog).getByRole('textbox', { name: 'yt-dlp format for this download' })).toHaveValue(
        'bestvideo+bestaudio',
      ),
    )
    await user.click(within(dialog).getByRole('button', { name: 'Download again' }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/redownloadVideos/runs')).toHaveLength(1))
    const sent = api.requests('/api/admin/tasks/redownloadVideos/runs')[0]?.searchParams
    expect(sent?.get('videos')).toBe('never000001')
    expect(sent?.has('format')).toBe(false)
    expect(await screen.findByText('Asked for: “Download videos again” runs in its turn.')).toBeInTheDocument()
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

  it('tries a video whose format matched none again with the format typed next to Try again', async () => {
    const user = userEvent.setup()
    const asked = run({ id: 'run-20261020T200000Z-eeeeee', task: 'downloadVideo', state: 'queued' })
    const failure = {
      title: null,
      tries: 1,
      skipped: false,
      firstFailedAt: '2026-10-20T08:00:00Z',
      lastFailedAt: '2026-10-20T08:00:00Z',
    }
    const api = routes({
      [`${base}/failed-downloads`]: [
        {
          ...failure,
          videoId: 'fmt00000001',
          reason: 'Requested format is not available. Use --list-formats for a list of available formats',
          youTubeUrl: 'https://www.youtube.com/watch?v=fmt00000001',
        },
        {
          ...failure,
          videoId: 'net00000001',
          reason: 'Unable to download webpage: timed out',
          youTubeUrl: 'https://www.youtube.com/watch?v=net00000001',
        },
      ],
      '/api/admin/youtube/settings': { downloadFormat: 'bestvideo+bestaudio' },
      '/api/admin/tasks/downloadVideo/runs': asked,
    })
    render(ChannelAdminPage, { id })

    const failed = await screen.findByRole('region', { name: 'Failed downloads · 2' })
    const rows = within(failed).getAllByRole('row')
    const formatRow = rows[1] as HTMLElement
    const otherRow = rows[2] as HTMLElement
    expect(within(otherRow).queryByRole('textbox')).toBeNull()
    const field = within(formatRow).getByRole('textbox', { name: "yt-dlp's format for this try" })
    await vi.waitFor(() => expect(field).toHaveValue('bestvideo+bestaudio'))
    expect(formatRow).toHaveTextContent("The video's formats are listed in the failed run's log.")

    // The YouTube page's format: nothing more is asked than without the field.
    await user.click(within(formatRow).getByRole('button', { name: /^Try again/ }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/downloadVideo/runs')).toHaveLength(1))
    expect(api.requests('/api/admin/tasks/downloadVideo/runs')[0]?.searchParams.has('format')).toBe(false)

    await user.clear(field)
    await user.type(field, ' 398+251 ')
    await user.click(within(formatRow).getByRole('button', { name: /^Try again/ }))
    await vi.waitFor(() => expect(api.requests('/api/admin/tasks/downloadVideo/runs')).toHaveLength(2))
    const sent = api.requests('/api/admin/tasks/downloadVideo/runs')[1]?.searchParams
    expect(sent?.get('target')).toBe('fmt00000001')
    expect(sent?.get('format')).toBe('398+251')
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
