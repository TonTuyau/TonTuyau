<script lang="ts">
  // "Copy" (1.4): copies `text`, says "Copied" for a moment, or that the browser refused (then select it).
  import { copyText } from '../lib/clipboard'
  import { t } from '../lib/i18n/i18n.svelte'

  let { text, label }: { text: () => string; label: string } = $props()

  let said = $state<'copied' | 'refused' | null>(null)

  async function onclick() {
    said = (await copyText(text())) ? 'copied' : 'refused'
    setTimeout(() => (said = null), 1_500)
  }
</script>

<button type="button" class="copy" aria-label={label} {onclick}>
  {said === 'copied' ? t('run.copied') : said === 'refused' ? t('run.copyFailed') : t('run.copy')}
</button>

<style>
  .copy {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-button-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .copy:hover {
    background: var(--color-button-secondary-hover);
  }
</style>
