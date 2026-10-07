// QA06 "Also confirmed": the UI preferences (language, theme, guide, shorts player, and since M5 the player size) live in this browser's
// localStorage only. Each is saved when chosen, never sent anywhere, and comes back on the next load.
import { guide, GUIDE_STORAGE_KEY } from './guide.svelte'
import { i18n, LANG_STORAGE_KEY } from './i18n/i18n.svelte'
import { PLAYER_SIZE_STORAGE_KEY, playerSize } from './playerSize.svelte'
import { SHORTS_PLAYER_STORAGE_KEY, shortsPlayer } from './shortsPlayer.svelte'
import { theme, THEME_STORAGE_KEY } from './theme.svelte'

function chooseEverything(): void {
  i18n.setLang('fr')
  theme.set('dark')
  if (!guide.expanded) {
    guide.toggle()
  }
  shortsPlayer.set('normal')
  playerSize.set('large')
}

describe('UI preferences', () => {
  afterEach(() => {
    i18n.setLang('en')
    theme.set('system')
    localStorage.clear()
    guide.reload()
    shortsPlayer.reload()
    playerSize.reload()
    vi.resetModules()
  })

  it('are saved in localStorage under their own keys, and nowhere else', () => {
    const fetch = vi.spyOn(globalThis, 'fetch')

    chooseEverything()

    expect(fetch).not.toHaveBeenCalled()
    expect(Object.fromEntries(Object.entries(localStorage))).toEqual({
      [LANG_STORAGE_KEY]: 'fr',
      [THEME_STORAGE_KEY]: 'dark',
      [GUIDE_STORAGE_KEY]: 'expanded',
      [SHORTS_PLAYER_STORAGE_KEY]: 'normal',
      [PLAYER_SIZE_STORAGE_KEY]: 'large',
    })
  })

  it('all come back on a reload (fresh modules, same storage)', async () => {
    chooseEverything()
    // What a reload starts from: a bare <html>, nothing in memory, the browser's storage intact.
    delete document.documentElement.dataset.theme
    document.documentElement.lang = 'en'
    vi.resetModules()

    const fresh = {
      i18n: (await import('./i18n/i18n.svelte')).i18n,
      theme: (await import('./theme.svelte')).theme,
      guide: (await import('./guide.svelte')).guide,
      shortsPlayer: (await import('./shortsPlayer.svelte')).shortsPlayer,
      playerSize: (await import('./playerSize.svelte')).playerSize,
    }

    expect(fresh.i18n).not.toBe(i18n)
    expect(fresh.i18n.lang).toBe('fr')
    expect(document.documentElement.lang).toBe('fr')
    expect(fresh.theme.preference).toBe('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(fresh.guide.expanded).toBe(true)
    expect(fresh.shortsPlayer.preference).toBe('normal')
    expect(fresh.playerSize.size).toBe('large')
  })

  it('fall back to the defaults on a reload with empty storage', async () => {
    vi.resetModules()

    expect((await import('./theme.svelte')).theme.preference).toBe('system')
    expect((await import('./guide.svelte')).guide.expanded).toBe(false)
    expect((await import('./shortsPlayer.svelte')).shortsPlayer.preference).toBe('vertical')
    expect((await import('./playerSize.svelte')).playerSize.size).toBe('small')
  })
})
