<script lang="ts">
  // Empty and error states: an icon, a title, an optional detail line and an optional action.
  import type { Snippet } from 'svelte'
  import Icon, { type IconName } from './Icon.svelte'

  let {
    icon = 'videos',
    title,
    detail,
    role,
    children,
  }: { icon?: IconName; title: string; detail?: string; role?: 'alert' | 'status'; children?: Snippet } = $props()
</script>

<div class="state" {role}>
  <span class="icon"><Icon name={icon} /></span>
  <p class="title">{title}</p>
  {#if detail}
    <p class="detail">{detail}</p>
  {/if}
  {#if children}
    <div class="actions">{@render children()}</div>
  {/if}
</div>

<style>
  .state {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-12) var(--space-4);
    text-align: center;
  }

  .icon {
    color: var(--color-text-secondary);
    --icon-size: var(--space-12);
  }

  .title {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .detail {
    max-width: 60ch;
    color: var(--color-text-secondary);
    overflow-wrap: anywhere;
  }

  .actions {
    margin-top: var(--space-2);
  }
</style>
