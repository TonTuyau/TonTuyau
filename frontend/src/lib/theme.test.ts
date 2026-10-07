import { applyTheme, initialTheme, theme, THEME_STORAGE_KEY } from './theme.svelte'

describe('theme preference', () => {
  afterEach(() => theme.set('system'))

  it('defaults to the device theme: no data-theme, so prefers-color-scheme decides', () => {
    expect(initialTheme()).toBe('system')

    applyTheme('system')

    expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
  })

  it('forces dark or light with data-theme on <html>, and saves the choice', () => {
    theme.set('dark')
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')

    theme.set('light')
    expect(document.documentElement.dataset.theme).toBe('light')

    theme.set('system')
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
  })

  it('reads the saved choice back, ignoring unknown values', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark')
    expect(initialTheme()).toBe('dark')

    localStorage.setItem(THEME_STORAGE_KEY, 'sepia')
    expect(initialTheme()).toBe('system')
  })

  it('still applies when localStorage throws', () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError')
    })

    expect(() => theme.set('dark')).not.toThrow()
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  describe('before the first paint (public/theme-init.js, loaded by index.html)', () => {
    const files = import.meta.glob(['/index.html', '/public/theme-init.js'], {
      query: '?raw',
      import: 'default',
      eager: true,
    }) as Record<string, string>
    const html = files['/index.html'] ?? ''
    const script = files['/public/theme-init.js'] ?? ''

    /** Runs the file as the browser would, with what localStorage holds. */
    function runWith(saved: string | null): void {
      localStorage.clear()
      if (saved !== null) {
        localStorage.setItem(THEME_STORAGE_KEY, saved)
      }
      delete document.documentElement.dataset.theme
      new Function(script)()
    }

    it('is a blocking script in <head>, before the app, and not inline (the CSP allows no inline script)', () => {
      const head = html.slice(0, html.indexOf('</head>'))

      expect(head).toContain('<script src="/theme-init.js"></script>')
      expect(html.indexOf('/theme-init.js')).toBeLessThan(html.indexOf('src/main.ts'))
    })

    it('uses the same key and values', () => {
      expect(script).toContain(`localStorage.getItem('${THEME_STORAGE_KEY}')`)
      expect(script).toMatch(/theme === 'dark' \|\| theme === 'light'/)
    })

    it.each([
      ['dark', 'dark'],
      ['light', 'light'],
      ['system', undefined],
      ['sepia', undefined],
      [null, undefined],
    ])('applies a saved "%s" as data-theme %s', (saved, expected) => {
      runWith(saved)

      expect(document.documentElement.dataset.theme).toBe(expected)
    })

    it('leaves the device theme when localStorage throws', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new DOMException('denied', 'SecurityError')
      })

      expect(() => runWith(null)).not.toThrow()
      expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
    })
  })
})
