import { en } from './en'
import { fr } from './fr'
import { detectLanguage, i18n, initialLanguage, interpolate, LANG_STORAGE_KEY, t, tp } from './i18n.svelte'
import type { Dictionary } from './types'

describe('i18n dictionaries', () => {
  it('have exactly the same keys in French and English', () => {
    expect(Object.keys(fr).sort()).toEqual(Object.keys(en).sort())
  })

  it('have the same placeholders in both languages', () => {
    const placeholders = (value: unknown) => [...JSON.stringify(value).matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort()
    for (const key of Object.keys(en) as (keyof typeof en)[]) {
      expect(placeholders(fr[key]), key).toEqual(placeholders(en[key]))
    }
  })

  it('call streams "Lives" in French (QA06, M5a-6), everywhere', () => {
    expect([fr['nav.streams'], fr['type.stream'], fr['feed.title.stream']]).toEqual(['Lives', 'Lives', 'Lives'])
    expect(fr['type.one.stream']).toBe('Live')
    expect(fr['count.streams']).toEqual({ one: '{count} live', other: '{count} lives' })
    expect(JSON.stringify(Object.values(fr))).not.toMatch(/stream|en direct|diffusion/i)
  })

  it('make a missing or unknown key a type error (checked by svelte-check)', () => {
    const { 'app.name': removed, ...withoutOne } = en
    // @ts-expect-error a dictionary without 'app.name' is not a Dictionary
    const broken: Dictionary = withoutOne
    // @ts-expect-error 'no.such.key' is not a key
    const unknown = () => t('no.such.key')

    expect(removed).toBe('TonTuyau')
    expect(broken).toBeDefined()
    expect(unknown).toBeTypeOf('function')
  })
})

describe('i18n language', () => {
  afterEach(() => i18n.setLang('en'))

  it('picks the first browser language it knows, else English', () => {
    expect(detectLanguage(['fr-CA', 'en-US'])).toBe('fr')
    expect(detectLanguage(['de-DE', 'en-GB'])).toBe('en')
    expect(detectLanguage(['de-DE'])).toBe('en')
    expect(detectLanguage([])).toBe('en')
  })

  it('prefers the saved choice over the browser language', () => {
    localStorage.setItem(LANG_STORAGE_KEY, 'fr')

    expect(initialLanguage()).toBe('fr')
  })

  it('ignores a saved value that is not a language', () => {
    localStorage.setItem(LANG_STORAGE_KEY, 'klingon')

    expect(['en', 'fr']).toContain(initialLanguage())
  })

  it('switches every text and the document language, and saves the choice', () => {
    expect(t('nav.home')).toBe('Home')

    i18n.setLang('fr')

    expect(t('nav.home')).toBe('Accueil')
    expect(document.documentElement.lang).toBe('fr')
    expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('fr')
  })

  it('still switches when localStorage throws (private window, blocked storage)', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError')
    })
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError')
    })

    expect(() => i18n.setLang('fr')).not.toThrow()
    expect(t('nav.home')).toBe('Accueil')
    expect(() => initialLanguage()).not.toThrow()
  })

  it('fills placeholders and picks plural forms per language', () => {
    expect(interpolate('Hello {name}, {missing}', { name: 'Ana' })).toBe('Hello Ana, {missing}')
    expect(tp('count.views', 1)).toBe('1 view')
    expect(tp('count.views', 0)).toBe('0 views')
    expect(tp('count.views', 74_198, '74K')).toBe('74K views')

    i18n.setLang('fr')

    expect(tp('count.views', 0)).toBe('0 vue')
    expect(tp('count.views', 1)).toBe('1 vue')
    expect(tp('count.views', 2)).toBe('2 vues')
  })
})
