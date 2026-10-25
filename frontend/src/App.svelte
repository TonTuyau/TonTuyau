<script lang="ts">
  // The shell: top bar, the left guide (expanded / mini rail / drawer) or, on phones, the bottom bar,
  // and the page for the current URL. The admin area (1.2) has its own shell.
  import AdminShell from './components/AdminShell.svelte'
  import BottomNav from './components/BottomNav.svelte'
  import Logo from './components/Logo.svelte'
  import Icon from './components/Icon.svelte'
  import Sidebar from './components/Sidebar.svelte'
  import TopBar from './components/TopBar.svelte'
  import { auth } from './lib/auth.svelte'
  import { guide } from './lib/guide.svelte'
  import { t } from './lib/i18n/i18n.svelte'
  import { router } from './lib/router/router.svelte'
  import ChannelPage from './pages/ChannelPage.svelte'
  import ChannelsPage from './pages/ChannelsPage.svelte'
  import FeedPage from './pages/FeedPage.svelte'
  import HomePage from './pages/HomePage.svelte'
  import LoginPage from './pages/LoginPage.svelte'
  import MyAccountPage from './pages/MyAccountPage.svelte'
  import NotFoundPage from './pages/NotFoundPage.svelte'
  import SearchPage from './pages/SearchPage.svelte'
  import ShortsPage from './pages/ShortsPage.svelte'
  import WatchPage from './pages/WatchPage.svelte'

  const wideQuery = '(min-width: 1024px)'
  const phoneQuery = '(max-width: 600px)'

  // Wide screens: the guide is docked, collapsed (mini rail, the default) or expanded (remembered).
  // Narrow screens: the mini rail, and the hamburger opens the full guide as a drawer.
  // Phones (QA06 M5a-15, QA07 M5a-21): the bottom navigation bar is the only navigation: no mini rail, no ☰,
  // and the drawer can't open (it closes if the window shrinks to phone width while it's open).
  // The watch page (2.1.0), as YouTube's: no guide docked at any width, the video has the room; ☰ opens the drawer.
  const matches = (query: string, fallback: boolean) => globalThis.matchMedia?.(query).matches ?? fallback
  let wide = $state(matches(wideQuery, true))
  let phone = $state(matches(phoneQuery, false))
  let drawerOpen = $state(false)

  $effect(() => router.start())

  // Who is logged in (the top bar's "Log in" or name), once at start.
  $effect(() => {
    void auth.load()
  })

  function watch(query: string, set: (value: boolean) => void): (() => void) | undefined {
    const list = globalThis.matchMedia?.(query)
    if (!list) {
      return undefined
    }
    const onchange = () => set(list.matches)
    list.addEventListener('change', onchange)
    return () => list.removeEventListener('change', onchange)
  }

  $effect(() => watch(wideQuery, (value) => (wide = value)))
  $effect(() => watch(phoneQuery, (value) => (phone = value)))

  // A navigation closes the drawer.
  $effect(() => {
    void router.path
    void router.search
    drawerOpen = false
  })

  // Shrinking to phone width closes the drawer.
  $effect(() => {
    if (phone) {
      drawerOpen = false
    }
  })

  function onmenu() {
    if (phone) {
      return
    }
    if (wide && !undocked) {
      guide.toggle()
    } else {
      drawerOpen = !drawerOpen
    }
  }

  function onkeydown(event: KeyboardEvent) {
    if (event.key === 'Escape' && drawerOpen) {
      drawerOpen = false
    }
  }

  const route = $derived(router.route)
  /** No guide docked on the left: the watch page. */
  const undocked = $derived(route.name === 'watch')
  const full = $derived(wide && guide.expanded && !undocked)
</script>

<svelte:window {onkeydown} />

<a class="skip" href="#main">{t('a11y.skipToContent')}</a>
{#if route.name === 'admin'}
  <AdminShell {route} />
{:else}
  <TopBar {onmenu} menu={!phone} />

  <div
    class="layout"
    class:full
    class:phone
    class:undocked
    data-guide={phone ? 'bottom' : undocked ? 'drawer' : full ? 'full' : 'mini'}
  >
    {#if !phone && !undocked}
      <aside class="guide">
        <Sidebar variant={full ? 'full' : 'mini'} />
      </aside>
    {/if}

    <main id="main" tabindex="-1">
      {#if route.name === 'home'}
        <HomePage />
      {:else if route.name === 'feed'}
        <FeedPage type={route.type} />
      {:else if route.name === 'channels'}
        <ChannelsPage />
      {:else if route.name === 'channel'}
        <ChannelPage id={route.id} tab={route.tab} />
      {:else if route.name === 'watch'}
        <WatchPage />
      {:else if route.name === 'shorts'}
        <ShortsPage id={route.id} />
      {:else if route.name === 'results'}
        <SearchPage />
      {:else if route.name === 'login'}
        <LoginPage />
      {:else if route.name === 'account'}
        <MyAccountPage />
      {:else}
        <NotFoundPage />
      {/if}
    </main>
  </div>

  {#if phone}
    <BottomNav />
  {/if}

  {#if drawerOpen && !phone}
    <button type="button" class="scrim" aria-label={t('nav.menu')} onclick={() => (drawerOpen = false)}></button>
    <div class="drawer" role="dialog" aria-modal="true" aria-label={t('nav.main')}>
      <div class="drawer-head">
        <button type="button" class="icon-button" aria-label={t('nav.menu')} onclick={() => (drawerOpen = false)}>
          <Icon name="menu" />
        </button>
        <Logo />
      </div>
      <Sidebar variant="full" />
    </div>
  {/if}
{/if}

<style>
  .skip {
    position: absolute;
    left: var(--space-2);
    top: calc(-1 * var(--topbar-height));
    z-index: var(--z-drawer);
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-button);
    color: var(--color-on-button);
  }

  .skip:focus {
    top: var(--space-2);
  }

  .layout {
    display: grid;
    grid-template-columns: var(--sidebar-mini-width) minmax(0, 1fr);
  }

  .layout.full {
    grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
  }

  /* The watch page: no rail, the drawer only. */
  .layout.undocked {
    grid-template-columns: minmax(0, 1fr);
  }

  /* Phones: no rail; the page keeps clear of the fixed bottom bar (pages that size to the viewport read it). */
  .layout.phone {
    grid-template-columns: minmax(0, 1fr);
    padding-bottom: var(--bottom-inset);
    --bottom-inset: var(--bottom-nav-height);
  }

  .guide {
    position: sticky;
    top: var(--topbar-height);
    height: calc(100vh - var(--topbar-height));
    overflow-y: auto;
  }

  main {
    min-width: 0;
    outline: none;
  }

  .scrim {
    position: fixed;
    inset: 0;
    z-index: var(--z-drawer);
    border: none;
    background: var(--color-scrim);
  }

  .drawer {
    position: fixed;
    top: 0;
    bottom: 0;
    left: 0;
    z-index: var(--z-drawer);
    width: var(--sidebar-width);
    overflow-y: auto;
    background: var(--color-bg);
  }

  .drawer-head {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    height: var(--topbar-height);
    padding: 0 var(--space-4);
  }

  .icon-button {
    display: grid;
    place-items: center;
    width: var(--icon-button-size);
    height: var(--icon-button-size);
    border: none;
    border-radius: var(--radius-round);
    background: transparent;
    color: var(--color-text);
  }

  .icon-button:hover {
    background: var(--color-surface);
  }
</style>
