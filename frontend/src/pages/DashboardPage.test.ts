import { fireEvent, render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { auth } from '../lib/auth.svelte'
import { i18n } from '../lib/i18n/i18n.svelte'
import { dashboard } from '../test/data'
import { fakeApi, problemResponse } from '../test/fakeApi'
import DashboardPage from './DashboardPage.svelte'

/** The channel names in the table, top to bottom. */
const names = () =>
  within(screen.getByRole('table', { name: 'Channels' }))
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).queryByRole('link')?.textContent?.trim() ?? row.textContent?.trim())

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

  it('draws a bar per import, with its date, time and counts on hover', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard() })
    render(DashboardPage)

    const card = within(await screen.findByRole('region', { name: 'Import history' }))
    expect(card.getByText('Last 2 imports, 40,012 new videos')).toBeInTheDocument()
    expect(card.getAllByTestId('import-bar')).toHaveLength(2)
    // A run of 1.1: one grey piece, and the legend says why.
    expect(card.getAllByTestId('import-bar')[0]?.querySelectorAll('rect.unknown')).toHaveLength(1)
    expect(card.getAllByTestId('import-bar')[1]?.querySelectorAll('rect')).toHaveLength(3)
    expect(card.getAllByText('Types not recorded (before 1.2)').length).toBeGreaterThan(0)

    const second = document.querySelectorAll('rect.hit')[1]
    if (!second) throw new Error('no hover zone for the second bar')
    await fireEvent.pointerMove(second)
    const tip = card.getByRole('status')
    expect(tip).toHaveTextContent('12 new videos')
    expect(tip).toHaveTextContent('8 videos · 3 shorts · 1 stream')
    expect(tip).toHaveTextContent(
      new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short' }).format(new Date('2026-10-11T09:30:00Z')),
    )
  })

  it('says when there was no import yet', async () => {
    fakeApi({ '/api/admin/dashboard': dashboard({ imports: [], lastSync: null }) })
    render(DashboardPage)

    expect(await screen.findByText('No import yet')).toBeInTheDocument()
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
