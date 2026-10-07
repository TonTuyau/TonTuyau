<script lang="ts">
  // Phone width (QA06, M5a-15): YouTube's bottom navigation bar, in place of the mini rail. Same destinations
  // as the left guide, in the same order.
  import { t } from '../lib/i18n/i18n.svelte'
  import { navItems } from '../lib/navItems'
  import { router } from '../lib/router/router.svelte'
  import Icon from './Icon.svelte'

  const items = $derived.by(() => {
    const { main, explore } = navItems(router.route)
    return [...main, ...explore]
  })
</script>

<nav class="bottom-nav" aria-label={t('nav.main')} data-testid="bottom-nav">
  <ul>
    {#each items as item (item.label)}
      <li>
        <a href={item.url} class="item" aria-current={item.current ? 'page' : undefined}>
          <Icon name={item.icon} />
          <span class="label">{t(item.label)}</span>
        </a>
      </li>
    {/each}
  </ul>
</nav>

<style>
  .bottom-nav {
    position: fixed;
    right: 0;
    bottom: 0;
    left: 0;
    z-index: var(--z-topbar);
    padding-bottom: env(safe-area-inset-bottom);
    border-top: var(--border-width) solid var(--color-divider);
    background: var(--color-bg);
  }

  ul {
    display: flex;
    height: var(--bottom-nav-height);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  li {
    flex: 1;
    min-width: 0;
  }

  .item {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-1);
    height: 100%;
    color: var(--color-text);
    font-size: var(--font-size-xxs);
  }

  .item[aria-current='page'] {
    font-weight: var(--font-weight-bold);
  }

  .item:active {
    background: var(--color-surface);
  }

  .label {
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
