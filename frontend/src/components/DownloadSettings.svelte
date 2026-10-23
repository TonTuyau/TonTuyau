<script lang="ts">
  // The YouTube page's download settings (2.0, B6): the format a download asks yt-dlp for, in its own syntax (checked
  // here for one line, not empty, not too long; a format that picks nothing fails the download, saying why), and the
  // tabs a channel added gets when none is named ("Add a channel", the browser extension). Saved apart from the reading
  // settings (which stay as they are); onsaved: the page reads them again.
  import { api, ApiError } from '../lib/api/client'
  import type { ChannelTabs, YouTubeSettings } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { channelTabs, tabsText } from '../lib/runs'
  import Button from './Button.svelte'
  import HelpButton from './HelpButton.svelte'

  let { current, onsaved }: { current: YouTubeSettings; onsaved: () => Promise<void> } = $props()

  const MAX_FORMAT = 200

  // The form follows what's saved, until it's changed here (each change a new value).
  let format = $derived(current.downloadFormat)
  let tabs = $derived<ChannelTabs>({ ...current.defaultTabs })
  let formatInvalid = $state(false)
  let pending = $state(false)
  let refused = $state<string | null>(null)
  const noTab = $derived(!channelTabs.some((tab) => tabs[tab]))
  const uid = $props.id()

  async function save(event: SubmitEvent) {
    event.preventDefault()
    const typed = format.trim()
    formatInvalid = typed.length === 0 || typed.length > MAX_FORMAT || [...typed].some((c) => c < ' ')
    if (formatInvalid || noTab) {
      return
    }
    pending = true
    refused = null
    try {
      await api.saveDownloadSettings(current.comments, typed, { ...tabs })
    } catch (error) {
      refused = t('youTube.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
      await onsaved()
    }
  }
</script>

<section class="card" aria-labelledby="{uid}-title">
  <div class="head">
    <h2 id="{uid}-title">{t('youTube.downloads')}</h2>
    <HelpButton zone={t('youTube.downloads')}>
      <p>{t('youTube.downloads.intro')}</p>
      <dl>
        <dt>{t('youTube.downloads.format')}</dt>
        <dd>{t('youTube.downloads.formatHint')}</dd>
      </dl>
    </HelpButton>
  </div>
  <form onsubmit={save} novalidate>
    <label class="format">
      <b>{t('youTube.downloads.format')}</b>
      <input
        bind:value={format}
        autocomplete="off"
        spellcheck="false"
        maxlength={MAX_FORMAT}
        aria-invalid={formatInvalid}
        aria-describedby={formatInvalid ? `${uid}-format-invalid` : undefined}
      />
      {#if formatInvalid}
        <small id="{uid}-format-invalid" class="invalid" role="alert">{t('youTube.downloads.format.invalid')}</small>
      {/if}
    </label>
    <fieldset>
      <legend>{t('youTube.downloads.tabs')}</legend>
      {#each channelTabs as tab (tab)}
        <label class="tab"
          ><input
            type="checkbox"
            checked={tabs[tab]}
            onchange={(event) => (tabs = { ...tabs, [tab]: event.currentTarget.checked })}
          />{t(`tabs.${tab}`)}</label
        >
      {/each}
    </fieldset>
    {#if noTab}
      <small class="invalid" role="alert">{t('tabs.none')}</small>
    {/if}
    {#if refused}
      <p class="invalid" role="alert">{refused}</p>
    {/if}
    <div class="actions">
      <Button type="submit" variant="primary" disabled={pending || noTab}>{t('youTube.comments.save')}</Button>
    </div>
  </form>
  <p class="hint">
    {t('youTube.downloads.saved', { format: current.downloadFormat, tabs: tabsText(current.defaultTabs) })}
  </p>
</section>

<style>
  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .format {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  input:not([type='checkbox']) {
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  input[aria-invalid='true'] {
    border-color: var(--color-error);
  }

  fieldset {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1) var(--space-4);
    padding: 0;
    border: 0;
  }

  legend {
    width: 100%;
    margin-bottom: var(--space-1);
    font-weight: var(--font-weight-medium);
  }

  .tab {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
  }

  .hint,
  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .invalid {
    color: var(--color-error);
  }

  .actions {
    display: flex;
    gap: var(--space-2);
  }
</style>
