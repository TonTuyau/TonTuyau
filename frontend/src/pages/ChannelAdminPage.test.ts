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
  videos: { notChecked: 4, onYouTube: 100, retryLater: 9, gone: 21 },
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
    expect(within(chips).getByRole('link', { name: 'All 134' })).toHaveAttribute('href', '/admin/channels/UCtest')
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
        return run({ task: 'refreshVideo', state: 'running', finishedAt: null, target: gone.video.id })
      },
    })
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Refresh Le poulet rôti' }))

    expect(api.requests('/api/admin/tasks/refreshVideo/runs')[0]?.searchParams.get('target')).toBe('gone0000001')
    expect(await screen.findByRole('status')).toHaveTextContent(/Refresh a video is going on/)
    expect(screen.getByRole('link', { name: 'Follow its log' })).toHaveAttribute(
      'href',
      '/admin/runs/run-20261016T090000Z-aaaaaa',
    )
    expect(screen.getByRole('button', { name: 'Refresh the channel' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Refresh Crème brûlée' })).toBeDisabled()
  })

  it('says why a refresh was refused', async () => {
    const user = userEvent.setup()
    routes({
      '/api/admin/tasks/refreshChannelVideos/runs': problemResponse(409, 'A sync is already running.', 'Conflict'),
    })
    render(ChannelAdminPage, { id })

    await user.click(await screen.findByRole('button', { name: 'Refresh the channel and its videos' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Not started: A sync is already running.')
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
