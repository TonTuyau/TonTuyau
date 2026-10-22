<script lang="ts">
  // A refresh on YouTube of the Tasks page (1.5), or a download (2.0, "Add a video"): what it does, its command, and
  // its video or channel, pasted as a link or an id. Refused here when it isn't one (the API would refuse it too); the page starts it (onstart) and opens its run.
  // One run at a time: while one goes on (or waits), it's added to the run queue (2.0) and waits its turn.
  import type { RunTask } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { taskCommands, taskHelp } from '../lib/runs'
  import { channelIdOf, videoIdOf } from '../lib/youtubeTargets'
  import Button from './Button.svelte'
  import HelpButton from './HelpButton.svelte'

  let {
    task,
    waits,
    onstart,
  }: {
    task: RunTask
    waits: boolean
    onstart: (task: RunTask, target: string) => Promise<void>
  } = $props()

  let value = $state('')
  let invalid = $state<string | null>(null)
  let pending = $state(false)

  const video = $derived(task === 'refreshVideo' || task === 'downloadVideo')
  const uid = $props.id()
  const help = $derived(taskHelp[task])

  async function submit(event: SubmitEvent) {
    event.preventDefault()
    const target = video ? videoIdOf(value) : channelIdOf(value)
    if (value.trim() === '') {
      invalid = t('task.target.empty')
      return
    }
    if (target === null) {
      invalid = video ? t('task.target.notVideo') : t('task.target.notChannel')
      return
    }
    invalid = null
    pending = true
    try {
      await onstart(task, target)
    } finally {
      pending = false
    }
  }
</script>

<article class="card" aria-labelledby="{uid}-title">
  <header>
    <div class="title">
      <h2 id="{uid}-title">{t(`task.${task}`)}</h2>
      {#if help}
        <HelpButton zone={t(`task.${task}`)}><p>{t(help)}</p></HelpButton>
      {/if}
    </div>
    <code>{taskCommands[task]}</code>
  </header>
  <p class="what">{t(`task.${task}.what`)}</p>
  <form novalidate onsubmit={submit}>
    <label for="{uid}-target">{video ? t('task.target.video') : t('task.target.channel')}</label>
    <input
      id="{uid}-target"
      bind:value
      autocomplete="off"
      spellcheck="false"
      placeholder={video ? 'https://youtu.be/k3Fq8xZ0aQw' : 'https://www.youtube.com/channel/UC…'}
      aria-invalid={invalid !== null}
      aria-describedby={invalid ? `${uid}-invalid` : undefined}
    />
    {#if invalid}
      <small id="{uid}-invalid" class="invalid" role="alert">{invalid}</small>
    {/if}
    <div class="actions">
      <Button type="submit" variant="filled" disabled={pending}>
        {#if waits}
          {t('task.addToQueue')}
        {:else}
          {task === 'downloadVideo'
            ? t('task.download')
            : task === 'refreshChannelVideos'
              ? t('task.refreshThem')
              : t('task.refresh')}
        {/if}
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

  .title {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
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

  label {
    font-size: var(--font-size-xs);
    color: var(--color-text-secondary);
  }

  input {
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
    font: inherit;
  }

  input:focus {
    border-color: var(--color-focus);
    outline: none;
    box-shadow: 0 0 0 var(--border-width) var(--color-focus);
  }

  input[aria-invalid='true'] {
    border-color: var(--color-error);
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
