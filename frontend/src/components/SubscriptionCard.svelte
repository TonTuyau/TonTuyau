<script lang="ts">
  // A channel's subscription on its admin page (2.0, B4): whether TonTuyau is subscribed, since when and by whom (an
  // account, TubeArchivist at the import, the command line), and which tabs the YouTube syncs download. Subscribe,
  // change the tabs, unsubscribe (the channel and its videos stay). Subscribed: "Download everything" (B5), its full
  // sync alone, asked first (it can take hours), then its run opens. onchange: the page reads the channel again.
  import { api, ApiError } from '../lib/api/client'
  import type { ChannelTabs, Subscription } from '../lib/api/types'
  import { formatAge } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { channelTabs } from '../lib/runs'
  import Button from './Button.svelte'
  import HelpButton from './HelpButton.svelte'

  let {
    channelId,
    subscription,
    onchange,
  }: {
    channelId: string
    subscription: Subscription | null
    onchange: () => Promise<void>
  } = $props()

  const all: ChannelTabs = { videos: true, shorts: true, streams: true }
  // The tabs shown follow the subscription read, until they're changed here (each change a new object).
  let tabs = $derived<ChannelTabs>({ ...(subscription?.tabs ?? all) })

  let pending = $state(false)
  let askingEverything = $state(false)
  let message = $state<{ kind: 'saved' | 'refused'; text: string } | null>(null)

  const changed = $derived(subscription === null || channelTabs.some((tab) => tabs[tab] !== subscription?.tabs[tab]))
  const none = $derived(!channelTabs.some((tab) => tabs[tab]))
  const uid = $props.id()

  function since(current: Subscription): string {
    const when = formatAge(current.since, i18n.lang)
    if (current.origin === 'tubeArchivist') {
      return t('subscription.sinceTubeArchivist', { when })
    }
    return current.by ? t('subscription.since', { when, by: current.by }) : t('subscription.sinceCli', { when })
  }

  async function downloadEverything() {
    message = null
    pending = true
    try {
      const started = await api.startTask('youTubeFullSync', channelId)
      router.navigate(href.adminRun(started.id))
    } catch (error) {
      const reason = error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
      message = { kind: 'refused', text: t('task.refused', { reason }) }
    } finally {
      pending = false
      askingEverything = false
    }
  }

  async function act(work: () => Promise<unknown>) {
    message = null
    pending = true
    try {
      await work()
      message = { kind: 'saved', text: t('subscription.saved') }
      await onchange()
    } catch (error) {
      const reason = error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
      message = { kind: 'refused', text: t('subscription.refused', { reason }) }
    } finally {
      pending = false
    }
  }
</script>

<section class="card" aria-labelledby="{uid}-title">
  <div class="head">
    <h2 id="{uid}-title">{t('subscription.title')}</h2>
    <HelpButton zone={t('subscription.title')}><p>{t('subscription.hint')}</p></HelpButton>
  </div>
  <p class="state">{subscription ? since(subscription) : t('subscription.none')}</p>
  <fieldset disabled={pending}>
    <legend>{t('tabs.legend')}</legend>
    {#each channelTabs as tab (tab)}
      <label
        ><input
          type="checkbox"
          checked={tabs[tab]}
          onchange={(event) => (tabs = { ...tabs, [tab]: event.currentTarget.checked })}
        />{t(`tabs.${tab}`)}</label
      >
    {/each}
  </fieldset>
  {#if none}
    <small class="invalid" role="alert">{t('tabs.none')}</small>
  {/if}
  <div class="actions">
    <Button
      variant="filled"
      disabled={pending || none || !changed}
      onclick={() => void act(() => api.subscribe(channelId, { ...tabs }))}
    >
      {subscription ? t('subscription.save') : t('subscription.subscribe')}
    </Button>
    {#if subscription}
      <Button disabled={pending} onclick={() => void act(() => api.unsubscribe(channelId))}>
        {t('subscription.unsubscribe')}
      </Button>
      <Button disabled={pending || askingEverything} onclick={() => (askingEverything = true)}>
        {t('subscription.everything')}
      </Button>
    {/if}
  </div>
  {#if subscription && askingEverything}
    <div class="confirm" role="group" aria-label={t('subscription.everything')}>
      <p>{t('subscription.everythingConfirm')}</p>
      <div class="actions">
        <Button variant="filled" disabled={pending} onclick={() => void downloadEverything()}>
          {t('subscription.everything')}
        </Button>
        <Button onclick={() => (askingEverything = false)}>{t('task.back')}</Button>
      </div>
    </div>
  {/if}
  {#if message}
    <p class={message.kind} role={message.kind === 'refused' ? 'alert' : 'status'}>{message.text}</p>
  {/if}
</section>

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

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
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
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  label {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .confirm {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .saved {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .invalid,
  .refused {
    color: var(--color-error);
    font-size: var(--font-size-xs);
  }
</style>
