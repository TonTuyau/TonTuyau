<script lang="ts">
  // An API error, explained from its status (lib/errors.ts) in the current language, with the server's own
  // ProblemDetails as a technical line under it. Retry only where retrying can help (not on a 400 or a 404); a
  // connection problem also retries by itself when the browser says it's back online.
  import type { ApiError } from '../lib/api/client'
  import { errorKind, errorTexts, isConnectionProblem, isRetryable } from '../lib/errors'
  import { t } from '../lib/i18n/i18n.svelte'
  import Button from './Button.svelte'
  import StateMessage from './StateMessage.svelte'

  let { error, onretry }: { error: ApiError; onretry?: () => void } = $props()

  const kind = $derived(errorKind(error))
  const texts = $derived(errorTexts[kind])
  const technical = $derived(
    error.fromServer
      ? [t('state.errorStatus', { status: error.status }), error.problem.detail ?? error.problem.title].join(' · ')
      : error.isNetworkError
        ? undefined
        : t('state.errorStatus', { status: error.status }),
  )

  $effect(() => {
    const retry = onretry
    if (!retry || !isConnectionProblem(kind)) {
      return
    }
    globalThis.addEventListener('online', retry)
    return () => globalThis.removeEventListener('online', retry)
  })
</script>

<StateMessage icon="warning" title={t(texts.title)} detail={t(texts.hint)} role="alert">
  {#if technical}
    <p class="technical">{technical}</p>
  {/if}
  {#if onretry && isRetryable(kind)}
    <Button onclick={onretry}>{t('state.retry')}</Button>
  {/if}
</StateMessage>

<style>
  .technical {
    margin-bottom: var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    overflow-wrap: anywhere;
  }
</style>
