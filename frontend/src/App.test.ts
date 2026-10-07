import { render, screen, within } from '@testing-library/svelte'
import userEvent from '@testing-library/user-event'
import App from './App.svelte'
import { guide, GUIDE_STORAGE_KEY } from './lib/guide.svelte'
import { i18n } from './lib/i18n/i18n.svelte'
import { fakeApi, goTo } from './test/fakeApi'

const layout = () => document.querySelector('[data-guide]')

describe('App shell', () => {
  it('shows the brand, the search box and the main navigation', () => {
    fakeApi({ '/api/home': { sections: [] } })

    render(App)

    expect(screen.getByText('TonTuyau')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Search' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument()
  })

  it('only asks the API what the current page needs', async () => {
    const api = fakeApi({ '/api/home': { sections: [] } })

    render(App)

    await screen.findByText('The library is empty')
    expect(api.calls.map((url) => url.pathname)).toEqual(['/api/home'])
  })

  it('shows the not-found page for an unknown URL, without calling the API', () => {
    const api = fakeApi({})
    goTo('/no/such/page/here')

    render(App)

    expect(screen.getByText("This page isn't available")).toBeInTheDocument()
    expect(api.spy).not.toHaveBeenCalled()
  })

  describe('left guide', () => {
    beforeEach(() => guide.reload())
    afterEach(() => vi.unstubAllGlobals())

    it('is collapsed (the mini rail with icons and labels) by default', () => {
      fakeApi({ '/api/home': { sections: [] } })

      render(App)

      expect(layout()).toHaveAttribute('data-guide', 'mini')
      expect(within(screen.getByRole('navigation', { name: 'Main navigation' })).getByText('Shorts')).toBeVisible()
    })

    it('expands with the hamburger, and the choice is remembered', async () => {
      fakeApi({ '/api/home': { sections: [] } })
      const user = userEvent.setup()
      const { unmount } = render(App)

      await user.click(screen.getByRole('button', { name: 'Menu' }))

      expect(layout()).toHaveAttribute('data-guide', 'full')
      expect(localStorage.getItem(GUIDE_STORAGE_KEY)).toBe('expanded')
      unmount()
      guide.reload()
      render(App)
      expect(layout()).toHaveAttribute('data-guide', 'full')

      await user.click(screen.getByRole('button', { name: 'Menu' }))
      expect(layout()).toHaveAttribute('data-guide', 'mini')
      expect(localStorage.getItem(GUIDE_STORAGE_KEY)).toBe('collapsed')
    })

    it('opens as an overlay drawer on narrow screens, closed by Esc', async () => {
      vi.stubGlobal('matchMedia', (query: string) => ({
        matches: false,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }))
      fakeApi({ '/api/home': { sections: [] } })
      const user = userEvent.setup()
      render(App)

      await user.click(screen.getByRole('button', { name: 'Menu' }))

      expect(screen.getByRole('dialog', { name: 'Main navigation' })).toBeInTheDocument()
      expect(layout()).toHaveAttribute('data-guide', 'mini')
      await user.keyboard('{Escape}')
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })
  })

  describe('phone bottom bar (QA06, M5a-15)', () => {
    /** matchMedia for a viewport `width` px wide, answering the shell's min-/max-width queries. */
    function viewport(width: number) {
      const listeners: (() => void)[] = []
      const state = { width }
      vi.stubGlobal('matchMedia', (query: string) => {
        const min = /min-width:\s*(\d+)px/.exec(query)
        const max = /max-width:\s*(\d+)px/.exec(query)
        return {
          get matches() {
            return (!min || state.width >= Number(min[1])) && (!max || state.width <= Number(max[1]))
          },
          media: query,
          addEventListener: (_type: string, listener: () => void) => listeners.push(listener),
          removeEventListener: () => {},
        }
      })
      return {
        resize(to: number) {
          state.width = to
          for (const listener of listeners) listener()
        },
      }
    }

    beforeEach(() => guide.reload())
    afterEach(() => vi.unstubAllGlobals())

    it('replaces the mini rail on phones, with every guide destination in order', () => {
      viewport(375)
      fakeApi({ '/api/home': { sections: [] } })

      render(App)

      expect(layout()).toHaveAttribute('data-guide', 'bottom')
      expect(document.querySelector('aside.guide')).toBeNull()
      const bar = screen.getByTestId('bottom-nav')
      expect(
        within(bar)
          .getAllByRole('link')
          .map((link) => [link.textContent?.trim(), link.getAttribute('href')]),
      ).toEqual([
        ['Home', '/'],
        ['Shorts', '/feed/shorts'],
        ['Channels', '/channels'],
        ['Videos', '/feed/videos'],
        ['Streams', '/feed/streams'],
      ])
      expect(within(bar).getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page')
    })

    it('marks the current destination and speaks French', async () => {
      viewport(375)
      fakeApi({ '/api/channels': { items: [], total: 0, nextCursor: null } })
      goTo('/channels')
      i18n.setLang('fr')

      render(App)

      const bar = screen.getByTestId('bottom-nav')
      expect(within(bar).getByRole('link', { name: 'Chaînes' })).toHaveAttribute('aria-current', 'page')
      expect(within(bar).getByRole('link', { name: 'Lives' })).toBeInTheDocument()
      i18n.setLang('en')
    })

    it('keeps the mini rail and no bottom bar on tablets and desktops', () => {
      viewport(800)
      fakeApi({ '/api/home': { sections: [] } })
      const { unmount } = render(App)

      expect(layout()).toHaveAttribute('data-guide', 'mini')
      expect(screen.queryByTestId('bottom-nav')).not.toBeInTheDocument()
      unmount()

      viewport(1440)
      render(App)
      expect(layout()).toHaveAttribute('data-guide', 'mini')
      expect(screen.queryByTestId('bottom-nav')).not.toBeInTheDocument()
    })

    it('has no ☰ on phones: the bottom bar is the only navigation (QA07, M5a-21)', () => {
      viewport(375)
      fakeApi({ '/api/home': { sections: [] } })

      render(App)

      expect(screen.queryByRole('button', { name: 'Menu' })).not.toBeInTheDocument()
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
      expect(screen.getAllByRole('navigation', { name: 'Main navigation' })).toEqual([screen.getByTestId('bottom-nav')])
    })

    it('keeps the ☰ on tablets (drawer) and desktops (expands the guide)', async () => {
      const user = userEvent.setup()
      viewport(800)
      fakeApi({ '/api/home': { sections: [] } })
      const { unmount } = render(App)

      await user.click(screen.getByRole('button', { name: 'Menu' }))
      expect(screen.getByRole('dialog', { name: 'Main navigation' })).toBeInTheDocument()
      unmount()

      viewport(1440)
      render(App)
      await user.click(screen.getByRole('button', { name: 'Menu' }))
      expect(layout()).toHaveAttribute('data-guide', 'full')
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    it('closes an open drawer when the window shrinks to phone width, and it stays closed', async () => {
      const user = userEvent.setup()
      const screenSize = viewport(800)
      fakeApi({ '/api/home': { sections: [] } })
      render(App)
      await user.click(screen.getByRole('button', { name: 'Menu' }))
      expect(screen.getByRole('dialog', { name: 'Main navigation' })).toBeInTheDocument()

      screenSize.resize(375)

      await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(screen.queryByRole('button', { name: 'Menu' })).not.toBeInTheDocument()
      screenSize.resize(800)
      await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Menu' })).toBeInTheDocument())
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    })

    it('follows the viewport when it crosses the phone width', async () => {
      const screenSize = viewport(800)
      fakeApi({ '/api/home': { sections: [] } })
      render(App)

      screenSize.resize(375)
      expect(await screen.findByTestId('bottom-nav')).toBeInTheDocument()
      expect(layout()).toHaveAttribute('data-guide', 'bottom')

      screenSize.resize(800)
      await vi.waitFor(() => expect(screen.queryByTestId('bottom-nav')).not.toBeInTheDocument())
      expect(layout()).toHaveAttribute('data-guide', 'mini')
    })
  })
})
