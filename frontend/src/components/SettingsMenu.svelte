<script lang="ts">
  // The top bar's settings menu (review item #3), YouTube style: a "⋮" button opening a popover with
  // "Appearance: …", "Language: …" and "Shorts player: …" (QA06); each opens its list in place, with a back arrow.
  // Under them, TonTuyau's version (QA13-4), plain text: not a menu item.
  // ARIA menu button: Enter / Space / ↓ ↑ open; ↑ ↓ Home End move; → or Enter opens a list, ← goes back;
  // Esc, Tab or a click outside close; focus returns to the button.
  import { tick } from 'svelte'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { languages, type Lang, type TextKey } from '../lib/i18n/types'
  import { shortsPlayer, shortsPlayerPreferences, type ShortsPlayerPreference } from '../lib/shortsPlayer.svelte'
  import { theme, themePreferences, type ThemePreference } from '../lib/theme.svelte'
  import Icon, { type IconName } from './Icon.svelte'

  type View = 'main' | 'appearance' | 'language' | 'shorts'

  const uid = $props.id()
  const menuId = `${uid}-menu`

  let open = $state(false)
  let view = $state<View>('main')
  let trigger: HTMLButtonElement | undefined = $state()
  let root: HTMLElement | undefined = $state()
  let menu: HTMLElement | undefined = $state()

  function items(): HTMLElement[] {
    return menu ? [...menu.querySelectorAll<HTMLElement>('[role^="menuitem"]')] : []
  }

  /** Focuses the item matching `selector` in the current view, else the first / last item. */
  async function focusItem(selector: string | 'first' | 'last') {
    await tick()
    const all = items()
    const target =
      selector === 'first'
        ? all[0]
        : selector === 'last'
          ? all.at(-1)
          : (menu?.querySelector<HTMLElement>(selector) ?? all[0])
    target?.focus()
  }

  function show(focus: 'first' | 'last' = 'first') {
    view = 'main'
    open = true
    void focusItem(focus)
  }

  function hide(restoreFocus = true) {
    open = false
    view = 'main'
    if (restoreFocus) {
      trigger?.focus()
    }
  }

  function enter(next: Exclude<View, 'main'>) {
    view = next
    void focusItem('[aria-checked="true"]')
  }

  function back() {
    const from = view
    view = 'main'
    void focusItem(`[data-view="${from}"]`)
  }

  function chooseTheme(preference: ThemePreference) {
    theme.set(preference)
    hide()
  }

  function chooseLang(lang: Lang) {
    i18n.setLang(lang)
    hide()
  }

  function chooseShortsPlayer(preference: ShortsPlayerPreference) {
    shortsPlayer.set(preference)
    hide()
  }

  function ontriggerkeydown(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      show(event.key === 'ArrowUp' ? 'last' : 'first')
    }
  }

  function onmenukeydown(event: KeyboardEvent) {
    const all = items()
    const index = all.findIndex((item) => item === document.activeElement)
    const move = (to: number) => {
      event.preventDefault()
      all[(to + all.length) % all.length]?.focus()
    }
    const current = all[index]
    switch (event.key) {
      case 'ArrowDown':
        move(index + 1)
        break
      case 'ArrowUp':
        move(index <= 0 ? all.length - 1 : index - 1)
        break
      case 'Home':
        move(0)
        break
      case 'End':
        move(all.length - 1)
        break
      case 'ArrowRight':
        if (view === 'main' && current?.dataset.view) {
          event.preventDefault()
          enter(current.dataset.view as Exclude<View, 'main'>)
        }
        break
      case 'ArrowLeft':
        if (view !== 'main') {
          event.preventDefault()
          back()
        }
        break
      case 'Escape':
        event.preventDefault()
        hide()
        break
      case 'Tab':
        hide(false)
        break
    }
  }

  // A click anywhere outside closes the menu.
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

  const themeLabels: Record<ThemePreference, TextKey> = {
    system: 'theme.system',
    dark: 'theme.dark',
    light: 'theme.light',
  }
  const rows: { view: Exclude<View, 'main'>; icon: IconName; label: () => string }[] = [
    {
      view: 'appearance',
      icon: 'appearance',
      label: () => t('settings.appearance', { value: t(`theme.current.${theme.preference}`) }),
    },
    { view: 'language', icon: 'translate', label: () => t('settings.language', { value: t(`lang.${i18n.lang}`) }) },
    {
      view: 'shorts',
      icon: 'shorts',
      label: () => t('settings.shortsPlayer', { value: t(`shortsPlayer.${shortsPlayer.preference}`) }),
    },
  ]
  const viewTitles: Record<Exclude<View, 'main'>, TextKey> = {
    appearance: 'theme.title',
    language: 'lang.label',
    shorts: 'shortsPlayer.title',
  }
</script>

{#snippet backRow(title: string)}
  <button type="button" role="menuitem" class="item back" tabindex="-1" onclick={back}>
    <Icon name="back" /><span class="visually-hidden">{t('settings.back')}:</span>
    <span>{title}</span>
  </button>
  <hr />
{/snippet}

{#snippet radio(checked: boolean, label: string, onclick: () => void, lang?: string)}
  <button type="button" role="menuitemradio" aria-checked={checked} class="item" tabindex="-1" {lang} {onclick}>
    <span class="check"
      >{#if checked}<Icon name="check" />{/if}</span
    >
    <span>{label}</span>
  </button>
{/snippet}

<div class="settings" bind:this={root}>
  <button
    bind:this={trigger}
    type="button"
    class="trigger"
    aria-haspopup="menu"
    aria-expanded={open}
    aria-controls={menuId}
    aria-label={t('settings.label')}
    onclick={() => (open ? hide() : show())}
    onkeydown={ontriggerkeydown}
  >
    <Icon name="more" />
  </button>

  {#if open}
    <div
      bind:this={menu}
      id={menuId}
      class="menu"
      role="menu"
      tabindex="-1"
      aria-label={view === 'main' ? t('settings.label') : t(viewTitles[view])}
      data-view={view}
      onkeydown={onmenukeydown}
    >
      {#if view === 'main'}
        {#each rows as row (row.view)}
          <button
            type="button"
            role="menuitem"
            class="item"
            tabindex="-1"
            data-view={row.view}
            onclick={() => enter(row.view)}
          >
            <Icon name={row.icon} />
            <span class="label">{row.label()}</span>
            <Icon name="chevron" />
          </button>
        {/each}
        <hr />
        <p class="version">TonTuyau {__TONTUYAU_VERSION__}</p>
      {:else if view === 'appearance'}
        {@render backRow(t('theme.title'))}
        <p class="hint" aria-hidden="true">{t('theme.hint')}</p>
        {#each themePreferences as preference (preference)}
          {@render radio(theme.preference === preference, t(themeLabels[preference]), () => chooseTheme(preference))}
        {/each}
      {:else if view === 'language'}
        {@render backRow(t('lang.label'))}
        {#each languages as lang (lang)}
          {@render radio(i18n.lang === lang, t(`lang.${lang}`), () => chooseLang(lang), lang)}
        {/each}
      {:else}
        {@render backRow(t('shortsPlayer.title'))}
        <p class="hint" aria-hidden="true">{t('shortsPlayer.hint')}</p>
        {#each shortsPlayerPreferences as preference (preference)}
          {@render radio(shortsPlayer.preference === preference, t(`shortsPlayer.${preference}`), () =>
            chooseShortsPlayer(preference),
          )}
        {/each}
      {/if}
    </div>
  {/if}
</div>

<style>
  .settings {
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
    color: var(--color-text);
  }

  .trigger:hover,
  .trigger[aria-expanded='true'] {
    background: var(--color-surface);
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

  .label {
    flex: 1;
  }

  .back {
    font-size: var(--font-size-md);
  }

  hr {
    margin: var(--space-2) 0;
    border: none;
    border-top: var(--border-width) solid var(--color-divider);
  }

  .hint {
    padding: var(--space-1) var(--space-4) var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .version {
    padding: var(--space-1) var(--space-4);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .check {
    display: inline-grid;
    width: var(--icon-size);
    height: var(--icon-size);
  }
</style>
