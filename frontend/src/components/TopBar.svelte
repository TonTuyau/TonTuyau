<script lang="ts">
  import { t } from '../lib/i18n/i18n.svelte'
  import Icon from './Icon.svelte'
  import Logo from './Logo.svelte'
  import SearchBox from './SearchBox.svelte'
  import SettingsMenu from './SettingsMenu.svelte'

  // `menu: false` on phones (QA07, M5a-21): no ☰, the bottom bar is the only navigation there.
  let { onmenu, menu = true }: { onmenu: () => void; menu?: boolean } = $props()

  // Phone width: the search box opens over the whole bar.
  let searching = $state(false)
</script>

<header class="topbar" class:searching>
  <div class="start">
    {#if menu}
      <button type="button" class="icon-button" aria-label={t('nav.menu')} onclick={onmenu}>
        <Icon name="menu" />
      </button>
    {/if}
    <Logo />
  </div>

  <div class="center">
    {#if searching}
      <button type="button" class="icon-button back" aria-label={t('search.close')} onclick={() => (searching = false)}>
        <Icon name="back" />
      </button>
    {/if}
    <SearchBox autofocus={searching} onnavigate={() => (searching = false)} />
  </div>

  <div class="end">
    <button
      type="button"
      class="icon-button open-search"
      aria-label={t('search.open')}
      onclick={() => (searching = true)}
    >
      <Icon name="search" />
    </button>
    <SettingsMenu />
  </div>
</header>

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
    background: var(--color-bg);
  }

  .start,
  .end {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    flex: none;
  }

  .center {
    display: flex;
    flex: 1;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    min-width: 0;
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

  .open-search {
    display: none;
  }

  @media (max-width: 600px) {
    .topbar {
      padding: 0 var(--space-2);
      gap: var(--space-2);
    }

    .center {
      display: none;
    }

    .open-search {
      display: grid;
    }

    .searching .start,
    .searching .end {
      display: none;
    }

    .searching .center {
      display: flex;
    }
  }
</style>
