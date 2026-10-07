<script lang="ts">
  // Loading placeholders shaped like the content that's coming. Announced once for screen readers.
  import { t } from '../lib/i18n/i18n.svelte'

  let { layout = 'grid', count = 8 }: { layout?: 'grid' | 'shorts' | 'rows' | 'page'; count?: number } = $props()
</script>

<div class="skeleton {layout}" role="status" aria-busy="true">
  <span class="visually-hidden">{t('state.loading')}</span>
  {#each { length: layout === 'page' ? 1 : count }, index (index)}
    <div class="item" aria-hidden="true">
      <div class="box"></div>
      <div class="line"></div>
      <div class="line short"></div>
    </div>
  {/each}
</div>

<style>
  .skeleton {
    display: grid;
    gap: var(--space-10) var(--grid-gap-x);
  }

  /* The same columns as the lists they stand for (VideoList). */
  .grid {
    grid-template-columns: var(--grid-columns);
  }

  .shorts {
    grid-template-columns: var(--shorts-columns);
    column-gap: var(--shorts-gap-x);
  }

  .rows,
  .page {
    grid-template-columns: 1fr;
  }

  .item {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .box {
    aspect-ratio: 16 / 9;
    border-radius: var(--radius-lg);
    background: var(--color-skeleton);
  }

  .shorts .box {
    aspect-ratio: 9 / 16;
  }

  .rows .box {
    width: min(var(--row-thumb-width), 45%);
  }

  .page .box {
    max-height: 60vh;
  }

  .line {
    height: var(--space-4);
    width: 90%;
    border-radius: var(--radius-sm);
    background: var(--color-skeleton);
  }

  .line.short {
    width: 60%;
  }
</style>
