<script lang="ts">
  // The admin's YouTube page (1.5): the YouTube cookie TonTuyau signs in with (its state, checking it again, removing
  // it, asked first), the tools' versions, and which yt-dlp runs: the latest official release by default, or one
  // official release, or one nightly build of the last 2 months. A version chosen is applied in the background; the
  // page asks again every 3 s while it's on its way (downloaded, or waiting for the task going on to end). A refusal
  // says why; a 401 ends the session.
  import Button from '../components/Button.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type { YouTubeOverview, YtdlpChoiceKind, YtdlpState, YtdlpVersions } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDate } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { pageTitle } from '../lib/title.svelte'

  const FOLLOW_EVERY = 3_000

  const overview = new Remote<YouTubeOverview>()
  const ytdlp = new Remote<YtdlpState>()
  const versions = new Remote<YtdlpVersions>()

  $effect(() => overview.run((signal) => api.youTube(signal)))
  $effect(() => ytdlp.run((signal) => api.ytdlp(signal)))
  $effect(() => versions.run((signal) => api.ytdlpVersions(signal)))

  $effect(() => {
    if (overview.error?.status === 401 || ytdlp.error?.status === 401) {
      auth.expired()
    }
  })

  /** Why the last action was refused. */
  let refused = $state<string | null>(null)
  let pending = $state(false)
  let confirmingRemove = $state(false)

  // ---------------------------------------------------------------- the cookie

  async function checkCookie() {
    await act(async () => {
      await api.checkYouTubeCookie()
    })
  }

  async function removeCookie() {
    await act(async () => {
      await api.removeYouTubeCookie()
    })
    confirmingRemove = false
  }

  async function act(work: () => Promise<void>) {
    pending = true
    refused = null
    try {
      await work()
    } catch (error) {
      refused = t('youTube.refused', { reason: reasonOf(error) })
    } finally {
      pending = false
      await overview.refresh()
    }
  }

  // ---------------------------------------------------------------- yt-dlp

  let kind = $state<YtdlpChoiceKind>('latest')
  let official = $state('')
  let nightly = $state('')
  /** The version the last choice is waiting for, until it runs (or fails). */
  let applying = $state<string | null>(null)

  // The form starts on what's chosen.
  let shown = false
  $effect(() => {
    const state = ytdlp.data
    const offered = versions.data
    if (!shown && state && offered) {
      shown = true
      kind = state.choice.kind
      official = (state.choice.kind === 'official' ? state.choice.version : null) ?? offered.official[0]?.version ?? ''
      nightly = (state.choice.kind === 'nightly' ? state.choice.version : null) ?? offered.nightly[0]?.version ?? ''
    }
  })

  const latest = $derived(versions.data?.official[0]?.version ?? null)
  const following = $derived(applying !== null || (ytdlp.data?.waiting ?? null) !== null)

  // Asked again while a version is on its way; it stops once it runs, or the check failed.
  $effect(() => {
    if (!following) {
      return
    }
    const timer = setInterval(async () => {
      await ytdlp.refresh()
      await overview.refresh()
      const state = ytdlp.data
      if (state && applying !== null && (state.running === applying || state.error !== null)) {
        applying = null
      }
    }, FOLLOW_EVERY)
    return () => clearInterval(timer)
  })

  async function choose(event: SubmitEvent) {
    event.preventDefault()
    const version = kind === 'official' ? official : kind === 'nightly' ? nightly : null
    pending = true
    refused = null
    try {
      const state = await api.chooseYtdlp(kind, version)
      ytdlp.data = state
      const target = version ?? latest
      applying = target !== null && target !== state.running ? target : null
    } catch (error) {
      refused = t('youTube.refused', { reason: reasonOf(error) })
    } finally {
      pending = false
    }
  }

  function chosenLabel(state: YtdlpState): string {
    const { kind: chosen, version } = state.choice
    return chosen === 'latest' ? t('ytdlp.choice.latest') : t(`ytdlp.choice.${chosen}`, { version: version ?? '' })
  }

  function optionLabel(version: string, publishedAt: string, image: string | null): string {
    const date = formatDate(publishedAt, i18n.lang)
    return version === image ? t('ytdlp.optionImage', { version, date }) : t('ytdlp.option', { version, date })
  }

  function reasonOf(error: unknown): string {
    if (error instanceof ApiError && error.status === 401) {
      auth.expired()
    }
    return error instanceof ApiError ? (error.problem.detail ?? error.problem.title) : String(error)
  }

  pageTitle(() => t('admin.youTube'))
  const uid = $props.id()
</script>

<div class="page">
  <header>
    <h1>{t('admin.youTube')}</h1>
    <p class="intro">{t('youTube.intro')}</p>
  </header>

  {#if refused}
    <p class="refused" role="alert">{refused}</p>
  {/if}

  {#if overview.status === 'loading'}
    <Skeleton layout="page" />
  {:else if overview.status === 'error' && overview.error}
    <ErrorState error={overview.error} onretry={() => overview.retry()} />
  {:else if overview.data}
    {@const data = overview.data}
    <div class="two">
      <section class="card" aria-labelledby="{uid}-cookie">
        <h2 id="{uid}-cookie">{t('youTube.cookie')}</h2>
        {#if data.cookie}
          {@const cookie = data.cookie}
          <dl>
            <div>
              <dt>{t('youTube.cookie.state')}</dt>
              <dd>
                <span class="pill {cookie.state}">{t(`youTube.cookie.state.${cookie.state}`)}</span>
                {#if cookie.checkedAt}
                  <small>{t('youTube.cookie.checked', { ago: formatAge(cookie.checkedAt, i18n.lang) })}</small>
                {/if}
              </dd>
            </div>
            <div>
              <dt>{t('youTube.cookie.received')}</dt>
              <dd>
                {t('youTube.cookie.receivedFrom', {
                  ago: formatAge(cookie.receivedAt, i18n.lang),
                  by: cookie.receivedBy,
                })}
              </dd>
            </div>
            {#if cookie.reason}
              <div>
                <dt>{t('youTube.cookie.reason')}</dt>
                <dd>{cookie.reason}</dd>
              </div>
            {/if}
          </dl>
          <div class="actions">
            {#if confirmingRemove}
              <div class="confirm" role="group" aria-label={t('youTube.cookie.remove')}>
                <p>{t('youTube.cookie.confirmRemove')}</p>
                <div>
                  <Button variant="filled" disabled={pending} onclick={() => void removeCookie()}>
                    {t('youTube.cookie.remove')}
                  </Button>
                  <Button onclick={() => (confirmingRemove = false)}>{t('youTube.cookie.keep')}</Button>
                </div>
              </div>
            {:else}
              <Button disabled={pending} onclick={() => void checkCookie()}>{t('youTube.cookie.check')}</Button>
              <Button disabled={pending} onclick={() => (confirmingRemove = true)}>{t('youTube.cookie.remove')}</Button>
            {/if}
          </div>
        {:else}
          <dl>
            <div>
              <dt>{t('youTube.cookie.state')}</dt>
              <dd><span class="pill none">{t('youTube.cookie.none')}</span></dd>
            </div>
          </dl>
          <p class="hint">{t('youTube.cookie.noneHint')}</p>
        {/if}
        <p class="hint">{t('youTube.cookie.hint')}</p>
      </section>

      <section class="card" aria-labelledby="{uid}-tools">
        <h2 id="{uid}-tools">{t('youTube.tools')}</h2>
        <dl>
          <div>
            <dt>yt-dlp</dt>
            <dd>
              {#if data.ytdlp}
                <code>{data.ytdlp}</code>
                {#if ytdlp.data}
                  <small>{ytdlp.data.bundled ? t('youTube.tools.image') : t('youTube.tools.downloaded')}</small>
                {/if}
              {:else}
                <small class="missing">{t('youTube.tools.missing')}</small>
              {/if}
            </dd>
          </div>
          <div>
            <dt>Deno</dt>
            <dd>
              {#if data.deno}
                <code>{data.deno}</code> <small>{t('youTube.tools.deno')}</small>
              {:else}
                <small class="missing">{t('youTube.tools.missing')}</small>
              {/if}
            </dd>
          </div>
        </dl>
      </section>
    </div>
  {/if}

  <section class="card" aria-labelledby="{uid}-ytdlp">
    <div>
      <h2 id="{uid}-ytdlp">yt-dlp</h2>
      <p class="hint">{t('ytdlp.intro')}</p>
    </div>
    {#if ytdlp.status === 'loading'}
      <Skeleton layout="page" />
    {:else if ytdlp.status === 'error' && ytdlp.error}
      <ErrorState error={ytdlp.error} onretry={() => ytdlp.retry()} />
    {:else if ytdlp.data}
      {@const state = ytdlp.data}
      <dl>
        <div>
          <dt>{t('ytdlp.running')}</dt>
          <dd>
            {#if state.running}
              <code>{state.running}</code>
              <small>{state.bundled ? t('youTube.tools.image') : t('youTube.tools.downloaded')}</small>
            {:else}
              <small class="missing">{t('youTube.tools.missing')}</small>
            {/if}
          </dd>
        </div>
        <div>
          <dt>{t('ytdlp.chosen')}</dt>
          <dd>
            {chosenLabel(state)}
            <small>{state.choice.chosenBy ? t('ytdlp.by', { by: state.choice.chosenBy }) : t('ytdlp.byDefault')}</small>
          </dd>
        </div>
        <div>
          <dt>{t('ytdlp.lastCheck')}</dt>
          <dd>{state.checkedAt ? formatAge(state.checkedAt, i18n.lang) : t('ytdlp.notYet')}</dd>
        </div>
      </dl>
      {#if state.waiting}
        <p class="note busy" role="status">{t('ytdlp.waiting', { version: state.waiting })}</p>
      {:else if applying}
        <p class="note busy" role="status">{t('ytdlp.applying', { version: applying })}</p>
      {/if}
      {#if state.error}
        <p class="note failed">{state.error}</p>
      {/if}

      <form class="choose" onsubmit={choose} novalidate>
        <fieldset>
          <legend>{t('ytdlp.version')}</legend>
          <label class="choice">
            <input type="radio" name="{uid}-kind" value="latest" bind:group={kind} />
            <b>{t('ytdlp.latest')}</b>
            <small>
              {t('ytdlp.latestHint')}
              {#if latest}{t('ytdlp.latestNow', { version: latest })}{/if}
            </small>
          </label>
          {#if versions.data}
            {@const offered = versions.data}
            {@const image = state.bundled ? state.running : null}
            <label class="choice">
              <input type="radio" name="{uid}-kind" value="official" bind:group={kind} />
              <b>{t('ytdlp.official')}</b>
              <small>{t('ytdlp.officialHint')}</small>
              <select bind:value={official} disabled={kind !== 'official'} aria-label={t('ytdlp.official')}>
                {#each offered.official as release (release.version)}
                  <option value={release.version}>{optionLabel(release.version, release.publishedAt, image)}</option>
                {/each}
              </select>
            </label>
            <label class="choice">
              <input type="radio" name="{uid}-kind" value="nightly" bind:group={kind} />
              <b>{t('ytdlp.nightly')}</b>
              <small>{t('ytdlp.nightlyHint')}</small>
              <select bind:value={nightly} disabled={kind !== 'nightly'} aria-label={t('ytdlp.nightly')}>
                {#each offered.nightly as release (release.version)}
                  <option value={release.version}>{optionLabel(release.version, release.publishedAt, null)}</option>
                {/each}
              </select>
            </label>
          {:else if versions.status === 'error' && versions.error}
            <p class="note failed">
              {t('ytdlp.noVersions', { reason: versions.error.problem.detail ?? versions.error.problem.title })}
            </p>
          {/if}
        </fieldset>
        <div class="actions">
          <Button type="submit" variant="filled" disabled={pending}>{t('ytdlp.use')}</Button>
        </div>
      </form>
      <p class="hint">{t('ytdlp.hint')}</p>
    {/if}
  </section>
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
    max-width: var(--admin-page-max-width);
    margin: 0 auto;
    padding: var(--space-6) var(--space-8) var(--space-12);
  }

  h1 {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
  }

  h2 {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .intro {
    margin-top: var(--space-1);
    color: var(--color-text-secondary);
  }

  .two {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(var(--card-min-width), 1fr));
    gap: var(--space-4);
    align-items: start;
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

  dl {
    display: grid;
    gap: var(--space-2);
    margin: 0;
  }

  dl div {
    display: grid;
    grid-template-columns: var(--task-label-width) minmax(0, 1fr);
    gap: var(--space-2);
    align-items: baseline;
  }

  dt {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  dd {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
    margin: 0;
    overflow-wrap: anywhere;
  }

  small,
  .hint {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .missing {
    color: var(--color-error);
  }

  .pill {
    display: inline-flex;
    align-items: center;
    height: var(--badge-height);
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-surface);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .pill.works {
    background: var(--color-success-bg);
    color: var(--color-success);
  }

  .pill.refused {
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .refused,
  .note.failed {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-error-bg);
    color: var(--color-error);
  }

  .note.busy {
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    background: var(--color-running-bg);
    color: var(--color-running);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .confirm {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    width: 100%;
    padding: var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-surface);
  }

  .confirm div {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .choose {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  fieldset {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    border: none;
  }

  legend {
    margin-bottom: var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .choice {
    display: grid;
    grid-template-columns: var(--icon-size) minmax(0, 1fr);
    gap: var(--space-1) var(--space-3);
    align-items: start;
    padding: var(--space-3) var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    cursor: pointer;
  }

  .choice:has(input:checked) {
    border-color: var(--color-brand);
    box-shadow: 0 0 0 var(--border-width) var(--color-brand);
  }

  .choice input {
    margin: var(--space-1) 0 0;
    accent-color: var(--color-brand);
  }

  .choice b {
    font-weight: var(--font-weight-medium);
  }

  .choice small,
  .choice select {
    grid-column: 2;
  }

  .choice select {
    justify-self: start;
    max-width: 100%;
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
  }

  .choice select:disabled {
    opacity: 0.5;
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }
  }
</style>
