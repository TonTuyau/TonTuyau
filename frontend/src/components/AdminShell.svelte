<script lang="ts">
  // The admin area (1.2), YouTube Studio style: its own top bar and a left menu (Dashboard, Tasks (1.4), Back to
  // TonTuyau), for a logged-in account only: anyone else goes to the login page, and comes back here once logged in.
  // The menu: full or icons only with the ☰, remembered (wide screens); icons only under 900 px, the ☰ opening it for
  // the visit; on phones a row of links, no ☰.
  import { adminGuide } from '../lib/adminGuide.svelte'
  import { auth } from '../lib/auth.svelte'
  import { t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href, type AdminRoute } from '../lib/router/routes'
  import DashboardPage from '../pages/DashboardPage.svelte'
  import RunPage from '../pages/RunPage.svelte'
  import TasksPage from '../pages/TasksPage.svelte'
  import AccountMenu from './AccountMenu.svelte'
  import Icon from './Icon.svelte'
  import Logo from './Logo.svelte'
  import SettingsMenu from './SettingsMenu.svelte'

  let { route }: { route: AdminRoute } = $props()

  // A run's page is part of the Tasks (the menu says so).
  const section = $derived(route.page === 'dashboard' ? 'dashboard' : 'tasks')

  const uid = $props.id()
  const navId = `${uid}-nav`
  const narrowQuery = '(max-width: 900px)'
  const phoneQuery = '(max-width: 600px)'

  const matches = (query: string) => globalThis.matchMedia?.(query).matches ?? false
  let narrow = $state(matches(narrowQuery))
  let phone = $state(matches(phoneQuery))
  let opened = $state(false)

  function watch(query: string, set: (value: boolean) => void): (() => void) | undefined {
    const list = globalThis.matchMedia?.(query)
    if (!list) {
      return undefined
    }
    const onchange = () => set(list.matches)
    list.addEventListener('change', onchange)
    return () => list.removeEventListener('change', onchange)
  }

  $effect(() => watch(narrowQuery, (value) => (narrow = value)))
  $effect(() => watch(phoneQuery, (value) => (phone = value)))

  // Nobody logged in (or the session ended): the login page, then back here.
  $effect(() => {
    if (auth.status === 'anonymous') {
      router.navigate(href.login(router.path + router.search), { replace: true })
    }
  })

  const full = $derived(phone || (narrow ? opened : !adminGuide.collapsed))

  function onmenu() {
    if (narrow) {
      opened = !opened
    } else {
      adminGuide.toggle()
    }
  }
</script>

{#if auth.status === 'authenticated'}
  <header class="topbar">
    <div class="start">
      {#if !phone}
        <button
          type="button"
          class="icon-button"
          aria-label={full ? t('admin.collapse') : t('admin.expand')}
          aria-expanded={full}
          aria-controls={navId}
          onclick={onmenu}
        >
          <Icon name="menu" />
        </button>
      {/if}
      <Logo />
      <span class="tag">{t('admin.tag')}</span>
    </div>
    <div class="end">
      <SettingsMenu />
      <AccountMenu />
    </div>
  </header>

  <div class="shell" class:full class:phone data-guide={phone ? 'row' : full ? 'full' : 'icons'}>
    <nav id={navId} class="guide" aria-label={t('admin.nav')}>
      <a
        class="item"
        href={href.admin()}
        aria-current={section === 'dashboard' ? 'page' : undefined}
        title={full ? undefined : t('admin.dashboard')}
      >
        <Icon name="dashboard" />
        <span class="label" class:visually-hidden={!full}>{t('admin.dashboard')}</span>
      </a>
      <a
        class="item"
        href={href.adminTasks()}
        aria-current={section === 'tasks' ? 'page' : undefined}
        title={full ? undefined : t('admin.tasks')}
      >
        <Icon name="tasks" />
        <span class="label" class:visually-hidden={!full}>{t('admin.tasks')}</span>
      </a>
      <a class="item back" href={href.home()} title={full ? undefined : t('admin.back')}>
        <Icon name="back" />
        <span class="label" class:visually-hidden={!full}>{t('admin.back')}</span>
      </a>
    </nav>

    <main id="main" tabindex="-1">
      {#if route.page === 'run'}
        <RunPage id={route.id} />
      {:else if route.page === 'tasks'}
        <TasksPage />
      {:else}
        <DashboardPage />
      {/if}
    </main>
  </div>
{/if}

<style>
  .topbar {
    position: sticky;
    top: 0;
    z-index: var(--z-topbar);
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-4);
    height: var(--topbar-height);
    padding: 0 var(--space-4);
    border-bottom: var(--border-width) solid var(--color-divider);
    background: var(--color-bg);
  }

  .start,
  .end {
    display: flex;
    align-items: center;
    gap: var(--space-2);
  }

  .tag {
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
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

  .shell {
    display: grid;
    grid-template-columns: var(--sidebar-mini-width) minmax(0, 1fr);
  }

  .shell.full {
    grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
  }

  .guide {
    position: sticky;
    top: var(--topbar-height);
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    height: calc(100vh - var(--topbar-height));
    padding: var(--space-3);
    border-right: var(--border-width) solid var(--color-divider);
  }

  .item {
    display: flex;
    align-items: center;
    gap: var(--space-4);
    height: var(--icon-button-size);
    padding: 0 var(--space-3);
    border-radius: var(--radius-md);
    font-size: var(--font-size-sm);
    white-space: nowrap;
  }

  .shell:not(.full) .item {
    justify-content: center;
    padding: 0;
  }

  .item:hover,
  .item[aria-current='page'] {
    background: var(--color-surface);
  }

  .item[aria-current='page'] {
    font-weight: var(--font-weight-medium);
  }

  .back {
    margin-top: auto;
    color: var(--color-text-secondary);
  }

  main {
    min-width: 0;
    outline: none;
  }

  /* Phones: one column, the menu a row of links above the page. */
  .shell.phone {
    grid-template-columns: minmax(0, 1fr);
  }

  .phone .guide {
    position: static;
    flex-direction: row;
    height: auto;
    padding: var(--space-2) var(--space-4);
    border-right: none;
    border-bottom: var(--border-width) solid var(--color-divider);
  }

  .phone .back {
    margin-top: 0;
    margin-left: auto;
  }

  @media (max-width: 600px) {
    .topbar {
      padding: 0 var(--space-2);
    }
  }
</style>
