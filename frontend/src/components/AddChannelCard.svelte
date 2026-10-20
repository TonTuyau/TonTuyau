<script lang="ts">
  // "Add a channel" of the Tasks page (2.0, B4): its id, @handle or a link to either, and the tabs to subscribe to
  // (the three ticked by default). Refused here when it isn't one or no tab is ticked (the API would refuse it too);
  // the page starts it (onstart) and opens its run. While a run goes on (or waits), it's added to the run queue.
  import type { ChannelTabs, RunTask } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { channelTabs, taskCommands } from '../lib/runs'
  import { channelTargetOf } from '../lib/youtubeTargets'
  import Button from './Button.svelte'

  let {
    waits,
    onstart,
  }: {
    waits: boolean
    onstart: (task: RunTask, target: string, tabs: ChannelTabs) => Promise<void>
  } = $props()

  let value = $state('')
  let tabs = $state<ChannelTabs>({ videos: true, shorts: true, streams: true })
  let invalid = $state<string | null>(null)
  let pending = $state(false)

  const uid = $props.id()

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    const target = channelTargetOf(value)
    if (value.trim() === '') {
      invalid = t('task.target.empty')
      return
    }
    if (target === null) {
      invalid = t('task.target.notChannelOrHandle')
      return
    }
    if (!channelTabs.some((tab) => tabs[tab])) {
      invalid = t('tabs.none')
      return
    }
    invalid = null
    pending = true
    try {
      await onstart('addChannel', target, { ...tabs })
    } finally {
      pending = false
    }
  }
</script>

<article class="card" aria-labelledby="{uid}-title">
  <header>
    <h2 id="{uid}-title">{t('task.addChannel')}</h2>
    <code>{taskCommands.addChannel}</code>
  </header>
  <p class="what">{t('task.addChannel.what')}</p>
  <form novalidate onsubmit={submit}>
    <label for="{uid}-target">{t('task.target.channelOrHandle')}</label>
    <input
      id="{uid}-target"
      bind:value
      autocomplete="off"
      spellcheck="false"
      placeholder="https://www.youtube.com/@…"
      aria-invalid={invalid !== null}
      aria-describedby={invalid ? `${uid}-invalid` : undefined}
    />
    <fieldset>
      <legend>{t('tabs.legend')}</legend>
      {#each channelTabs as tab (tab)}
        <label class="tab"><input type="checkbox" bind:checked={tabs[tab]} />{t(`tabs.${tab}`)}</label>
      {/each}
    </fieldset>
    {#if invalid}
      <small id="{uid}-invalid" class="invalid" role="alert">{invalid}</small>
    {/if}
    <div class="actions">
      <Button type="submit" variant="filled" disabled={pending}>
        {waits ? t('task.addToQueue') : t('task.add')}
      </Button>
      {#if waits}
        <small>{t('task.waitsItsTurn')}</small>
      {/if}
    </div>
  </form>
</article>

<style>
  .card {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-5);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  header {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  code,
  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .what {
    color: var(--color-text-secondary);
    text-wrap: pretty;
  }

  form {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin-top: auto;
  }

  label,
  legend {
    font-size: var(--font-size-xs);
    color: var(--color-text-secondary);
  }

  input:not([type='checkbox']) {
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
    font: inherit;
  }

  input:not([type='checkbox']):focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
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
  }

  .tab {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--color-text);
    font-size: var(--font-size-sm);
  }

  .invalid {
    color: var(--color-error);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-3);
    margin-top: var(--space-1);
  }
</style>
