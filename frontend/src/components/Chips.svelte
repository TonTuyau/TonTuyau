<script lang="ts" generics="T extends string">
  // YouTube's chip bar: one choice among a few (sorts). Each chip is a link, so the choice lives in the URL.
  let {
    label,
    options,
    selected,
    hrefFor,
  }: { label: string; options: { value: T; label: string }[]; selected: T; hrefFor: (value: T) => string } = $props()
</script>

<nav class="chips" aria-label={label}>
  {#each options as option (option.value)}
    <a class="chip" href={hrefFor(option.value)} aria-current={option.value === selected ? 'true' : undefined}
      >{option.label}</a
    >
  {/each}
</nav>

<style>
  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-3);
  }

  .chip {
    display: inline-flex;
    align-items: center;
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-surface);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-medium);
    white-space: nowrap;
  }

  .chip:hover {
    background: var(--color-surface-hover);
  }

  .chip[aria-current='true'] {
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
  }
</style>
