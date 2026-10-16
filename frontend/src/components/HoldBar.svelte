<script lang="ts">
  // A refresh on hold (1.5): YouTube refused, it waits for its cool-down. What it read and has left, when it resumes
  // by itself; Resume now, or Cancel it (it won't resume). On the Tasks page and on the run's page.
  import Button from './Button.svelte'
  import RunStatePill from './RunStatePill.svelte'
  import type { Run } from '../lib/api/types'
  import { formatDateTime, formatNumber } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { href } from '../lib/router/routes'

  let {
    run,
    showTask = true,
    disabled = false,
    onresume,
    oncancel,
  }: {
    run: Run
    /** Its task and target, linked to its page (the Tasks page); the run's own page has them already. */
    showTask?: boolean
    disabled?: boolean
    onresume: (runId: string) => void
    oncancel: (runId: string) => void
  } = $props()

  const n = (value: number) => formatNumber(value, i18n.lang)
</script>

<section class="bar" aria-label={t('run.state.onHold')}>
  <RunStatePill state={run.state} />
  <p>
    {#if showTask}
      <a href={href.adminRun(run.id)}><b>{t(`task.${run.task}`)}</b> <code>{run.target}</code></a> ·
    {/if}
    {t('hold.text', {
      read: n(run.counts.videosRead + run.counts.channelsRead),
      left: n(run.left),
      at: run.finishedAt ? formatDateTime(run.finishedAt, i18n.lang) : '–',
      resume: run.resumeAt ? formatDateTime(run.resumeAt, i18n.lang) : '–',
    })}
  </p>
  <div class="actions">
    <Button variant="filled" {disabled} onclick={() => onresume(run.id)}>{t('hold.resume')}</Button>
    <Button {disabled} onclick={() => oncancel(run.id)}>{t('hold.cancel')}</Button>
  </div>
</section>

<style>
  .bar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-3) var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }

  p {
    flex: 1 1 20rem;
  }

  a {
    color: inherit;
  }

  b {
    font-weight: var(--font-weight-medium);
  }

  code {
    font-size: var(--font-size-xs);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }
</style>
