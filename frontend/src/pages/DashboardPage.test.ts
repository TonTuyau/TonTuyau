import { fireEvent, render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { auth } from '../lib/auth.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import type { ImportBar, ImportHistory } from '../lib/api/types'
import { dashboard, importHistory } from '../test/data'
import { fakeApi, problemResponse } from '../test/fakeApi'
import DashboardPage from './DashboardPage.svelte'

/** The channel names in the table, top to bottom. */
const names = () =>
  within(screen.getByRole('table', { name: 'Channels' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).queryAllByRole('link')[0]?.textContent?.trim() ?? row.textContent?.trim())

/** Moves the pointer over the bar at this index (its whole column is the hover zone). */
async function hover(index: number) {
  const zone = document.querySelectorAll('rect.hit')[index]
  if (!zone) throw new Error(`no hover zone for bar ${index}`)
  await fireEvent.pointerMove(zone)
}

/** The vertical axis' labels, bottom to top. */
const axis = () => [...document.querySelectorAll('text.axis[text-anchor="end"]')].map((text) => text.textContent)

/** The heights of the bar's pieces, bottom to top. */
const pieces = (index: number) =>
  [...(document.querySelectorAll('[data-testid="import-bar"]')[index]?.querySelectorAll('rect') ?? [])].map((rect) =>
    Number(rect.getAttribute('height')),
  )

const sum = (values: number[]) => values.reduce((a, b) => a + b, 0)

const total = (label: string) => screen.getAllByText(label, { selector: 'dt' })[0]?.nextElementSibling?.textContent

describe('DashboardPage', () => {
  it('shows the totals as the API counted them', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard() })
    render(DashboardPage)

    await screen.findByText('Channels', { selector: 'dt' })
    expect(total('Channels')).toBe('3')
    expect(total('Videos')).toBe('5,583')
    expect(total('Shorts')).toBe('1,231')
    expect(total('Streams')).toBe('574')
    expect(total('Gone from YouTube')).toBe('85')
    expect(screen.getByText(/^Library synced/)).toBeInTheDocument()
  })

  it('shows the play time with each type’s share and average length, as given', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard() })
    render(DashboardPage)

    const card = within(await screen.findByRole('region', { name: 'Play time' }))
    expect(card.getByText('1,000 h')).toBeInTheDocument()
    expect(card.getByText('≈ 42 days non-stop')).toBeInTheDocument()
    expect(card.getByText('70%, 8 min on average')).toBeInTheDocument()
    expect(card.getByText('700 h')).toBeInTheDocument()
    expect(card.getByText('1%, 29 s on average')).toBeInTheDocument()
    expect(card.getByText('29%, 2 h 00 min on average')).toBeInTheDocument()
  })

  it('asks for the last month by default, a bar per day, empty ones too, the step and counts on hover', async () => {
    const api = fakeApi({ '/api/admin/dashboard': dashboard(), '/api/admin/dashboard/imports': importHistory() })
    render(DashboardPage)

    const card = within(await screen.findByRole('region', { name: 'Import history' }))
    expect(await card.findByText('40,012 new videos', { selector: 'p' })).toBeInTheDocument()
    expect(api.requests('/api/admin/dashboard/imports').map((url) => url.search)).toEqual(['?period=month'])
    expect(card.getByRole('button', { name: 'Last month' })).toHaveAttribute('aria-pressed', 'true')
    expect(card.getByRole('img', { name: 'New videos per day' })).toBeInTheDocument()

    const bars = card.getAllByTestId('import-bar')
    expect(bars).toHaveLength(3)
    // A run of 1.1: one grey piece, and the legend says why. An empty day keeps its slot.
    expect(bars[0]?.querySelectorAll('rect.unknown')).toHaveLength(1)
    expect(bars[1]?.querySelectorAll('rect')).toHaveLength(0)
    expect(bars[2]?.querySelectorAll('rect')).toHaveLength(3)
    expect(card.getAllByText('Types not recorded (before 1.2)').length).toBeGreaterThan(0)

    // 22:00 UTC on the 11th is the 12th in Paris, the sync's time zone.
    await hover(2)
    const tip = card.getByRole('status')
    expect(tip).toHaveTextContent('Oct 12')
    expect(tip).toHaveTextContent('12 new videos')
    expect(tip).toHaveTextContent('8 videos · 3 shorts · 1 stream')
    await hover(1)
    expect(card.getByRole('status')).toHaveTextContent('Oct 11 0 new videos')
  })

  it.each<[ImportHistory['step'], string, string]>([
    ['run', '2026-10-12T07:30:00Z', 'Oct 12, 09:30 AM'],
    ['day', '2026-10-11T22:00:00Z', 'Oct 12'],
    ['week', '2026-10-04T22:00:00Z', 'Week of Oct 5'],
    ['month', '2026-09-30T22:00:00Z', 'October 2026'],
  ])('names a bar by its step: %s', async (step, start, name) => {
    const bar: ImportBar = { start, total: 2, byType: { videos: 2, shorts: 0, streams: 0 }, notRecorded: 0 }
    fakeApi({
      '/api/admin/dashboard': dashboard(),
      '/api/admin/dashboard/imports': importHistory({ step, total: 2, bars: [bar] }),
    })
    render(DashboardPage)

    await screen.findByText('2 new videos', { selector: 'p' })
    await hover(0)
    expect(within(screen.getByRole('region', { name: 'Import history' })).getByRole('status')).toHaveTextContent(name)
  })

  it('switches the period: asks again, draws the new steps, remembers the choice', async () => {
    const year = importHistory({
      step: 'week',
      total: 5,
      bars: [{ start: '2026-10-04T22:00:00Z', total: 5, byType: { videos: 3, shorts: 2, streams: 0 }, notRecorded: 0 }],
    })
    const api = fakeApi({
      '/api/admin/dashboard': dashboard(),
      '/api/admin/dashboard/imports': (url: URL) =>
        url.searchParams.get('period') === 'year' ? year : importHistory(),
    })
    const user = userEvent.setup()
    const { unmount } = render(DashboardPage)

    const card = within(await screen.findByRole('region', { name: 'Import history' }))
    await card.findByText('40,012 new videos', { selector: 'p' })
    await user.click(card.getByRole('button', { name: 'Last year' }))

    expect(await card.findByText('5 new videos', { selector: 'p' })).toBeInTheDocument()
    expect(card.getByRole('button', { name: 'Last year' })).toHaveAttribute('aria-pressed', 'true')
    expect(card.getByRole('button', { name: 'Last month' })).toHaveAttribute('aria-pressed', 'false')
    expect(card.getAllByTestId('import-bar')).toHaveLength(1)
    expect(card.getByRole('img', { name: 'New videos per week' })).toBeInTheDocument()
    // The rest of the dashboard isn't asked again.
    expect(api.requests('/api/admin/dashboard')).toHaveLength(1)
    expect(localStorage.getItem('tontuyau.importPeriod')).toBe('year')

    unmount()
    render(DashboardPage)
    await screen.findByText('5 new videos', { selector: 'p' })
    expect(api.requests('/api/admin/dashboard/imports').map((url) => url.search)).toEqual([
      '?period=month',
      '?period=year',
      '?period=year',
    ])
  })

  it('switches to a log scale: powers of ten, a small import shows, its types share its height, remembered', async () => {
    const api = fakeApi({ '/api/admin/dashboard': dashboard(), '/api/admin/dashboard/imports': importHistory() })
    const user = userEvent.setup()
    const { unmount } = render(DashboardPage)

    const card = within(await screen.findByRole('region', { name: 'Import history' }))
    await card.findByText('40,012 new videos', { selector: 'p' })
    expect(card.getByRole('button', { name: 'Linear' })).toHaveAttribute('aria-pressed', 'true')
    expect(axis()).toEqual(['0', '12,500', '25,000', '37,500', '50,000'])
    // 12 videos beside 40,000: under a pixel.
    expect(sum(pieces(2))).toBeLessThan(1)

    await user.click(card.getByRole('button', { name: 'Log' }))
    expect(card.getByRole('button', { name: 'Log' })).toHaveAttribute('aria-pressed', 'true')
    expect(card.getByRole('button', { name: 'Linear' })).toHaveAttribute('aria-pressed', 'false')
    expect(axis()).toEqual(['0', '1', '10', '100', '1,000', '10,000', '50,000'])
    // log10(13) / log10(50,001) of the 190 pixels, shared 8 : 3 : 1 by the videos, shorts and streams.
    expect(sum(pieces(2))).toBeCloseTo(45.05, 1)
    expect(pieces(2).map((height) => height / sum(pieces(2)))).toEqual(
      [8 / 12, 3 / 12, 1 / 12].map((share) => expect.closeTo(share)),
    )
    expect(pieces(1)).toEqual([])
    // Drawn again, not asked again.
    expect(api.requests('/api/admin/dashboard/imports')).toHaveLength(1)
    expect(localStorage.getItem('tontuyau.importScale')).toBe('log')

    unmount()
    render(DashboardPage)
    const again = within(await screen.findByRole('region', { name: 'Import history' }))
    await again.findByText('40,012 new videos', { selector: 'p' })
    expect(again.getByRole('button', { name: 'Log' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('says when the period brought no video, and when there was no sync yet', async () => {
    fakeApi({
      '/api/admin/dashboard': dashboard({ lastSync: null }),
      '/api/admin/dashboard/imports': importHistory({ total: 0, bars: [] }),
    })
    render(DashboardPage)

    expect(await screen.findByText('No new video over this period')).toBeInTheDocument()
    expect(screen.getByText('No successful sync yet')).toBeInTheDocument()
  })

  it('lists every channel biggest first; a column sorts, again the other way', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard() })
    const user = userEvent.setup()
    render(DashboardPage)

    await screen.findByRole('table', { name: 'Channels' })
    expect(names()).toEqual(['Vinyl Archive', 'Écoles du Goût', 'Atelier Bois'])
    expect(screen.getByRole('columnheader', { name: /Total/ })).toHaveAttribute('aria-sort', 'descending')

    await user.click(screen.getByRole('button', { name: 'Channel' }))
    expect(names()).toEqual(['Atelier Bois', 'Écoles du Goût', 'Vinyl Archive'])
    expect(screen.getByRole('columnheader', { name: /Channel/ })).toHaveAttribute('aria-sort', 'ascending')

    await user.click(screen.getByRole('button', { name: /Channel/ }))
    expect(names()).toEqual(['Vinyl Archive', 'Écoles du Goût', 'Atelier Bois'])

    await user.click(within(screen.getByRole('table', { name: 'Channels' })).getByRole('button', { name: 'Shorts' }))
    expect(names()).toEqual(['Écoles du Goût', 'Atelier Bois', 'Vinyl Archive'])
    expect(screen.getByRole('link', { name: 'Atelier Bois' })).toHaveAttribute('href', '/channel/UCsmall')
    expect(screen.getByRole('link', { name: 'Manage Atelier Bois' })).toHaveAttribute('href', '/admin/channels/UCsmall')
  })

  it('finds a channel by name (accents and case ignored) and keeps those with videos gone from YouTube', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard() })
    const user = userEvent.setup()
    render(DashboardPage)

    await user.type(await screen.findByRole('searchbox', { name: 'Search channels' }), 'ECOLE')
    expect(names()).toEqual(['Écoles du Goût'])
    expect(screen.getByText('1 of 3 channels')).toBeInTheDocument()

    await user.clear(screen.getByRole('searchbox'))
    const gone = screen.getByRole('button', { name: 'Gone from YouTube', pressed: false })
    await user.click(gone)
    expect(gone).toHaveAttribute('aria-pressed', 'true')
    expect(names()).toEqual(['Vinyl Archive', 'Atelier Bois'])

    await user.type(screen.getByRole('searchbox'), 'école')
    expect(names()).toEqual(['No channel'])
  })

  it('speaks French', async () => {
    i18n.setLang('fr')
    fakeApi({ '/api/admin/dashboard': dashboard() })
    render(DashboardPage)

    expect(await screen.findByRole('heading', { name: 'Tableau de bord' })).toBeInTheDocument()
    expect(await screen.findByText('Disparues de YouTube', { selector: 'dt' })).toBeInTheDocument()
    expect(screen.getByText('3 sur 3 chaînes')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Dernier mois' })).toHaveAttribute('aria-pressed', 'true')
    i18n.setLang('en')
  })

  it('forgets the session when the API says nobody is logged in', async () => {
    fakeApi({
      '/api/auth/me': { username: 'admin' },
      '/api/admin/dashboard': problemResponse(401, 'Nobody is logged in.'),
    })
    await auth.load()
    render(DashboardPage)

    await vi.waitFor(() => expect(auth.status).toBe('anonymous'))
  })
})
