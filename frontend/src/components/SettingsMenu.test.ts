import { render, screen } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import { i18n, LANG_STORAGE_KEY } from '../lib/i18n/i18n.svelte'
import { SHORTS_PLAYER_STORAGE_KEY, shortsPlayer } from '../lib/shortsPlayer.svelte'
import { theme, THEME_STORAGE_KEY } from '../lib/theme.svelte'
import SettingsMenu from './SettingsMenu.svelte'

describe('SettingsMenu', () => {
  beforeEach(() => {
    i18n.setLang('en')
    theme.set('system')
    shortsPlayer.reload()
  })
  afterEach(() => {
    i18n.setLang('en')
    theme.set('system')
  })

  it('opens the main list with the current appearance and language', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)
    const trigger = screen.getByRole('button', { name: 'Settings' })

    expect(trigger).toHaveAttribute('aria-haspopup', 'menu')
    await user.click(trigger)

    expect(trigger).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('menu', { name: 'Settings' })).toBeInTheDocument()
    const rows = screen.getAllByRole('menuitem').map((row) => row.textContent?.trim())
    expect(rows).toEqual(['Appearance: Device theme', 'Language: English', 'Shorts player: Standard'])
    expect(screen.getByRole('menuitem', { name: 'Appearance: Device theme' })).toHaveFocus()
  })

  it("shows TonTuyau's version under the rows, as plain text and not as a menu item", async () => {
    const user = userEvent.setup()
    render(SettingsMenu)

    await user.click(screen.getByRole('button', { name: 'Settings' }))

    expect(__TONTUYAU_VERSION__).toMatch(/^\d+\.\d+\.\d+(-(alpha|beta|rc)\.\d+)?$/)
    const version = screen.getByText(`TonTuyau ${__TONTUYAU_VERSION__}`)
    expect(screen.getByRole('menu')).toContainElement(version)
    expect(version.closest('[role^="menuitem"]')).toBeNull()
    expect(screen.getAllByRole('menuitem')).toHaveLength(3)
  })

  it('goes into Appearance, picks Dark, applies and saves it, and closes', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)

    await user.click(screen.getByRole('button', { name: 'Settings' }))
    await user.click(screen.getByRole('menuitem', { name: 'Appearance: Device theme' }))

    expect(screen.getByRole('menu', { name: 'Appearance' })).toBeInTheDocument()
    const options = screen.getAllByRole('menuitemradio')
    expect(options.map((option) => option.textContent?.trim())).toEqual([
      'Use device theme',
      'Dark theme',
      'Light theme',
    ])
    expect(screen.getByRole('menuitemradio', { name: 'Use device theme' })).toHaveAttribute('aria-checked', 'true')
    expect(screen.getByRole('menuitemradio', { name: 'Use device theme' })).toHaveFocus()

    await user.click(screen.getByRole('menuitemradio', { name: 'Dark theme' }))

    expect(theme.preference).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Settings' })).toHaveFocus()
  })

  it('switches the language entirely with the keyboard: → in, ↓, Enter', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)
    screen.getByRole('button', { name: 'Settings' }).focus()

    await user.keyboard('{Enter}')
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('menuitem', { name: 'Language: English' })).toHaveFocus()
    await user.keyboard('{ArrowRight}')
    expect(screen.getByRole('menuitemradio', { name: 'English' })).toHaveFocus()
    await user.keyboard('{ArrowDown}{Enter}')

    expect(i18n.lang).toBe('fr')
    expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('fr')
    expect(screen.getByRole('button', { name: 'Paramètres' })).toHaveFocus()
  })

  it('goes back to the main list with the back arrow or ←', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)
    await user.click(screen.getByRole('button', { name: 'Settings' }))
    await user.click(screen.getByRole('menuitem', { name: 'Language: English' }))

    await user.click(screen.getByRole('menuitem', { name: /Back/ }))
    expect(screen.getByRole('menu', { name: 'Settings' })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: 'Language: English' })).toHaveFocus()

    await user.keyboard('{ArrowRight}{ArrowLeft}')
    expect(screen.getByRole('menuitem', { name: 'Language: English' })).toHaveFocus()
  })

  it('closes on Esc (focus back) and on a click outside, without changing anything', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)
    const trigger = screen.getByRole('button', { name: 'Settings' })

    await user.click(trigger)
    await user.keyboard('{ArrowRight}{Escape}')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()

    await user.click(trigger)
    await user.click(document.body)
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
    expect(theme.preference).toBe('system')
  })

  it('shows its labels in French', async () => {
    i18n.setLang('fr')
    theme.set('light')
    const user = userEvent.setup()
    render(SettingsMenu)

    await user.click(screen.getByRole('button', { name: 'Paramètres' }))

    expect(screen.getAllByRole('menuitem').map((row) => row.textContent?.trim())).toEqual([
      'Apparence : Clair',
      'Langue : Français',
      'Lecteur de Shorts : Standard',
    ])
  })

  it('switches the shorts player to landscape and back, saved like the theme', async () => {
    const user = userEvent.setup()
    render(SettingsMenu)

    await user.click(screen.getByRole('button', { name: 'Settings' }))
    await user.click(screen.getByRole('menuitem', { name: 'Shorts player: Standard' }))

    expect(screen.getByRole('menu', { name: 'Shorts player' })).toBeInTheDocument()
    expect(screen.getAllByRole('menuitemradio').map((option) => option.textContent?.trim())).toEqual([
      'Standard',
      'Landscape',
    ])
    expect(screen.getByRole('menuitemradio', { name: 'Standard' })).toHaveAttribute('aria-checked', 'true')

    await user.click(screen.getByRole('menuitemradio', { name: 'Landscape' }))

    expect(shortsPlayer.preference).toBe('normal')
    expect(localStorage.getItem(SHORTS_PLAYER_STORAGE_KEY)).toBe('normal')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Settings' }))
    expect(screen.getByRole('menuitem', { name: 'Shorts player: Landscape' })).toBeInTheDocument()
  })
})
