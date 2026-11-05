<script lang="ts" module>
  import type { YouTubeSettings } from '../lib/api/types'

  /** What a run takes this once (2.3.0): only what differs from the YouTube page's settings; none when unread. */
  export function downloadOnce(
    settings: YouTubeSettings | null,
    format: string,
    withoutCookie: boolean,
  ): { format?: string; retryWithoutCookie?: boolean } {
    const typed = format.trim()
    return {
      format: settings && typed.length > 0 && typed !== settings.downloadFormat ? typed : undefined,
      retryWithoutCookie: settings && withoutCookie !== settings.retryWithoutCookie ? withoutCookie : undefined,
    }
  }
</script>

<script lang="ts">
  // A dialog's download settings for one run (2.3.0: "Download everything", downloading videos again): yt-dlp's format
  // and the retry without the cookie, filled in with the YouTube page's settings; disabled until they're read (not
  // read: the run takes them as they are).
  import { t } from '../lib/i18n/i18n.svelte'

  let {
    settings,
    format = $bindable(''),
    withoutCookie = $bindable(true),
    disabled = false,
  }: {
    settings: YouTubeSettings | null
    format: string
    withoutCookie: boolean
    disabled?: boolean
  } = $props()

  const uid = $props.id()
</script>

<div class="field">
  <label for="{uid}-format">{t('subscription.everythingFormat')}</label>
  <input
    id="{uid}-format"
    bind:value={format}
    disabled={disabled || settings === null}
    autocomplete="off"
    spellcheck="false"
    maxlength={200}
  />
  <small>{t('subscription.everythingFormatHint')}</small>
</div>
<label class="check">
  <input type="checkbox" bind:checked={withoutCookie} disabled={disabled || settings === null} />
  {t('youTube.downloads.retryWithoutCookie')}
</label>

<style>
  /* As the YouTube page's fields. */
  .field {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .field label {
    color: var(--color-text);
    font-size: var(--font-size-sm);
  }

  .field input {
    width: 100%;
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  .field small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .check {
    display: inline-flex;
    align-items: flex-start;
    gap: var(--space-2);
    color: var(--color-text);
  }

  .check input {
    margin-top: var(--space-1);
  }
</style>
