<script lang="ts">
  // The left guide. `full`: icons + labels in sections (desktop expanded, and the drawer);
  // `mini`: the narrow icon rail YouTube shows when the guide is collapsed.
  import { t } from '../lib/i18n/i18n.svelte'
  import { navItems, type NavItem } from '../lib/navItems'
  import { router } from '../lib/router/router.svelte'
  import Icon from './Icon.svelte'

  let { variant }: { variant: 'full' | 'mini' } = $props()

  const items = $derived(navItems(router.route))
  const main = $derived(items.main)
  const explore = $derived(items.explore)
</script>

{#snippet link(item: NavItem)}
  <li>
    <a href={item.url} class="item" aria-current={item.current ? 'page' : undefined}>
      <Icon name={item.icon} />
      <span class="label">{t(item.label)}</span>
    </a>
  </li>
{/snippet}

<nav class="sidebar {variant}" aria-label={t('nav.main')}>
  <ul>
    {#each main as item (item.label)}
      {@render link(item)}
    {/each}
  </ul>
  {#if variant === 'full'}
    <hr />
    <h2 class="section">{t('nav.explore')}</h2>
  {/if}
  <ul>
    {#each explore as item (item.label)}
      {@render link(item)}
    {/each}
  </ul>
</nav>

<style>
  .sidebar {
    display: flex;
    flex-direction: column;
    padding: var(--space-3);
    overflow-y: auto;
  }

  ul {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .item {
    display: flex;
    align-items: center;
    gap: var(--space-6);
    height: var(--icon-button-size);
    padding: 0 var(--space-3);
    border-radius: var(--radius-md);
    font-size: var(--font-size-sm);
  }

  .item:hover {
    background: var(--color-surface);
  }

  .item[aria-current='page'] {
    background: var(--color-surface);
    font-weight: var(--font-weight-medium);
  }

  .item[aria-current='page']:hover {
    background: var(--color-surface-hover);
  }

  hr {
    width: 100%;
    margin: var(--space-3) 0;
    border: none;
    border-top: var(--border-width) solid var(--color-divider);
  }

  .section {
    padding: var(--space-2) var(--space-3);
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .mini {
    padding: var(--space-1);
  }

  .mini .item {
    flex-direction: column;
    justify-content: center;
    gap: var(--space-1);
    height: auto;
    padding: var(--space-4) 0 var(--space-3);
    font-size: var(--font-size-xs);
  }

  .mini .item[aria-current='page'] {
    background: transparent;
  }

  .mini .item:hover {
    background: var(--color-surface);
  }

  .mini .label {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
