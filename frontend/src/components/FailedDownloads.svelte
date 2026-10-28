<script lang="ts">
  // A channel's failed downloads on its admin page (2.0, B5): each video the YouTube syncs couldn't download, why
  // (as yt-dlp said it), how many times and when last, and whether the incremental sync leaves it now (3 failures).
  // Try again downloads one (the task downloadVideo), Try them all again the channel's (retryDownloads): through the
  // run queue, a line says so with a link to the run. Nothing shown while nothing failed. `refreshes` going up (a run
  // ended): read again.
  import { api, ApiError } from '../lib/api/client'
  import type { DownloadFailure, Run } from '../lib/api/types'
  import { formatAge } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { href } from '../lib/router/routes'
  import Button from './Button.svelte'
  import HelpButton from './HelpButton.svelte'

  let { channelId, refreshes }: { channelId: string; refreshes: number } = $props()

  const failures = new Remote<DownloadFailure[]>()
  $effect(() => {
    void refreshes
    return failures.run((signal) => api.failedDownloads(channelId, signal))
  })

  let pending = $state(false)
  let asked = $state<Run | null>(null)
  let refused = $state<string | null>(null)
  const uid = $props.id()

  async function tryAgain(work: () => Promise<Run>) {
    pending = true
    asked = null
    refused = null
    try {
      asked = await work()
    } catch (error) {
      refused = t('task.refused', {
        reason: error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error),
      })
    } finally {
      pending = false
    }
  }
</script>

{#if failures.data && failures.data.length > 0}
  {@const list = failures.data}
  <section class="failed" aria-labelledby="{uid}-title">
    <div class="head">
      <h2 id="{uid}-title">{tp('failed.title', list.length)}</h2>
      <div class="tools">
        <Button disabled={pending} onclick={() => void tryAgain(() => api.startTask('retryDownloads', channelId))}>
          {t('failed.tryAll')}
        </Button>
        <HelpButton zone={tp('failed.title', list.length)}><p>{t('failed.hint')}</p></HelpButton>
      </div>
    </div>
    {#if asked}
      <p class="asked" role="status">
        {t('failed.asked')} <a href={href.adminRun(asked.id)}>{t('task.follow')}</a>
      </p>
    {/if}
    {#if refused}
      <p class="refused" role="alert">{refused}</p>
    {/if}
    <div class="table-box">
      <table aria-labelledby="{uid}-title">
        <thead>
          <tr>
            <th>{t('failed.video')}</th>
            <th>{t('failed.reason')}</th>
            <th>{t('failed.tries')}</th>
            <th><span class="visually-hidden">{t('channelAdmin.actions')}</span></th>
          </tr>
        </thead>
        <tbody>
          {#each list as failure (failure.videoId)}
            <tr>
              <td>
                <div class="video">
                  <a href={failure.youTubeUrl} target="_blank" rel="noreferrer noopener">
                    <b>{failure.title ?? failure.videoId}</b>
                  </a>
                  <code>{failure.videoId}</code>
                </div>
              </td>
              <td class="reason">{failure.reason}</td>
              <td class="tries">
                {tp('failed.triesCount', failure.tries)}
                <small>{t('failed.last', { ago: formatAge(failure.lastFailedAt, i18n.lang) })}</small>
                {#if failure.skipped}<small class="skipped">{t('failed.skipped')}</small>{/if}
              </td>
              <td>
                <Button
                  disabled={pending}
                  onclick={() => void tryAgain(() => api.startTask('downloadVideo', failure.videoId))}
                >
                  {t('failed.tryAgain')} <span class="visually-hidden">{failure.title ?? failure.videoId}</span>
                </Button>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </section>
{/if}

<style>
  .failed {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .head {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .tools {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  code,
  small {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .asked {
    color: var(--color-text-secondary);
  }

  /* "Follow its log" (2.1.1): a link in the sentence, so it looks like one. */
  .asked a {
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
    text-decoration: underline;
    text-underline-offset: var(--underline-offset);
  }

  .refused {
    color: var(--color-error);
  }

  .table-box {
    overflow-x: auto;
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th,
  td {
    padding: var(--space-2) var(--space-3);
    border-bottom: var(--border-width) solid var(--color-border);
    text-align: left;
    vertical-align: top;
  }

  /* Its title over its id, in a box of its own: the cell stays a table cell, its rows' lines aligned with the others'. */
  .video {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .reason {
    max-width: 40ch;
    overflow-wrap: anywhere;
  }

  .tries {
    white-space: nowrap;
  }

  .tries small {
    display: block;
  }

  .skipped {
    align-self: flex-start;
    margin-top: var(--space-1);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
  }
</style>
