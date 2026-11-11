<script lang="ts">
  // The top bar's account corner (1.2): a "Log in" link while nobody is logged in (back to this page afterwards; not on
  // the login page itself); once logged in, the name's initial, opening a small menu with the name (2.3.0: and
  // the role), My account, Administration (admins only) and Log out. Menu button like the settings menu: Enter / Space / ↓ open on the first item, ↑ / ↓ move between the items;
  // Esc, Tab or a click outside close; focus back on the button.
  import { tick } from 'svelte'
  import { auth } from '../lib/auth.svelte'
  import { t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import Icon from './Icon.svelte'

  const uid = $props.id()
  const menuId = `${uid}-menu`

  let open = $state(false)
  let trigger: HTMLButtonElement | undefined = $state()
  let root: HTMLElement | undefined = $state()
  let menu: HTMLElement | undefined = $state()

  const initial = $derived((auth.username ?? '').slice(0, 1).toUpperCase())
  const loginHref = $derived(href.login(router.path + router.search))

  const items = () => [...(menu?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])]

  async function show() {
    open = true
    await tick()
    items()[0]?.focus()
  }

  function hide(restoreFocus = true) {
    open = false
    if (restoreFocus) {
      trigger?.focus()
    }
  }

  function ontriggerkeydown(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      void show()
    }
  }

  function onmenukeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault()
      hide()
    } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const all = items()
      const step = event.key === 'ArrowDown' ? 1 : -1
      const at = all.indexOf(document.activeElement as HTMLElement)
      all[(at + step + all.length) % all.length]?.focus()
    } else if (event.key === 'Tab') {
      hide(false)
    }
  }

  $effect(() => {
    if (!open) {
      return
    }
    const onpointerdown = (event: PointerEvent) => {
      if (root && event.target instanceof Node && !root.contains(event.target)) {
        hide(false)
      }
    }
    document.addEventListener('pointerdown', onpointerdown)
    return () => document.removeEventListener('pointerdown', onpointerdown)
  })

  async function logout() {
    try {
      await auth.logout()
      hide(false)
    } catch {
      // The backend couldn't be reached: still logged in, the menu stays open to try again.
    }
  }
</script>

{#if auth.status === 'anonymous' && router.route.name !== 'login'}
  <a class="login" href={loginHref}>
    <Icon name="person" />
    <span>{t('account.login')}</span>
  </a>
{:else if auth.status === 'authenticated' && auth.username !== null}
  <div class="account" bind:this={root}>
    <button
      bind:this={trigger}
      type="button"
      class="trigger"
      aria-haspopup="menu"
      aria-expanded={open}
      aria-controls={menuId}
      aria-label={t('account.menu', { user: auth.username })}
      onclick={() => (open ? hide() : void show())}
      onkeydown={ontriggerkeydown}
    >
      <span class="initial" aria-hidden="true">{initial}</span>
    </button>
    {#if open}
      <div
        bind:this={menu}
        id={menuId}
        class="menu"
        role="menu"
        tabindex="-1"
        aria-label={auth.username}
        onkeydown={onmenukeydown}
      >
        <p class="name">
          {auth.displayName ?? auth.username}
          <small>{t(`accounts.role.${auth.role ?? 'user'}`)}</small>
        </p>
        <hr />
        <a href={href.account()} role="menuitem" class="item" tabindex="-1" onclick={() => hide(false)}>
          <Icon name="person" />
          <span>{t('account.mine')}</span>
        </a>
        {#if auth.isAdmin}
          <a href={href.admin()} role="menuitem" class="item" tabindex="-1" onclick={() => hide(false)}>
            <Icon name="dashboard" />
            <span>{t('account.admin')}</span>
          </a>
        {/if}
        <button type="button" role="menuitem" class="item" tabindex="-1" onclick={logout}>
          <Icon name="logout" />
          <span>{t('account.logout')}</span>
        </button>
      </div>
    {/if}
  </div>
{/if}

<style>
  .login {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-pill);
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
    white-space: nowrap;
  }

  .login:hover {
    background: var(--color-surface);
  }

  .account {
    position: relative;
  }

  .trigger {
    display: grid;
    place-items: center;
    width: var(--icon-button-size);
    height: var(--icon-button-size);
    border: none;
    border-radius: var(--radius-round);
    background: transparent;
  }

  .initial {
    display: grid;
    place-items: center;
    width: var(--space-8);
    height: var(--space-8);
    border-radius: var(--radius-round);
    background: var(--color-brand);
    color: var(--color-on-brand);
    font-weight: var(--font-weight-medium);
  }

  .menu {
    position: absolute;
    top: calc(100% + var(--space-1));
    right: 0;
    z-index: var(--z-menu);
    width: var(--menu-width);
    max-width: calc(100vw - var(--space-4));
    padding: var(--space-2) 0;
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
    color: var(--color-text);
    box-shadow: var(--shadow-menu);
    outline: none;
  }

  .name {
    display: flex;
    flex-direction: column;
    padding: var(--space-2) var(--space-4);
    font-weight: var(--font-weight-medium);
    overflow-wrap: anywhere;
  }

  .name small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-regular);
  }

  hr {
    margin: var(--space-2) 0;
    border: none;
    border-top: var(--border-width) solid var(--color-divider);
  }

  .item {
    display: flex;
    align-items: center;
    gap: var(--space-4);
    width: 100%;
    min-height: var(--icon-button-size);
    padding: 0 var(--space-4);
    border: none;
    background: transparent;
    color: var(--color-text);
    font-size: var(--font-size-sm);
    text-align: start;
  }

  .item:hover,
  .item:focus-visible {
    background: var(--color-surface);
    outline: none;
  }
</style>
