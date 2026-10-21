<script lang="ts">
  // The admin's YouTube page (1.5): the YouTube cookie TonTuyau signs in with (its state, checking it again, removing
  // it, asked first), the tools' versions, and which yt-dlp runs: the latest official release by default, or one
  // official release, or one nightly build of the last 2 months. A version chosen is applied in the background; the
  // page asks again every 3 s while it's on its way (downloaded, or waiting for the task going on to end). And which
  // comments a refresh reads: none (the default), the top N, or all; going easy on YouTube (the pause between videos,
  // how long a refresh YouTube refused waits on hold). A refusal says why; a 401 ends the session.
  import Button from '../components/Button.svelte'
  import DownloadSettings from '../components/DownloadSettings.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import HelpButton from '../components/HelpButton.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api, ApiError } from '../lib/api/client'
  import type {
    YouTubeCommentsChoice,
    YouTubeOverview,
    YouTubeSettings,
    YtdlpChoiceKind,
    YtdlpState,
    YtdlpVersions,
  } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatDate, formatNumber } from '../lib/format'
  import { i18n, t } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { pageTitle } from '../lib/title.svelte'

  const FOLLOW_EVERY = 3_000
  // A week: the backend refuses more (a longer wait is better done by hand).
  const MAX_SYNC_EVERY_HOURS = 168

  const overview = new Remote<YouTubeOverview>()
  const ytdlp = new Remote<YtdlpState>()
  const versions = new Remote<YtdlpVersions>()
  const settings = new Remote<YouTubeSettings>()

  $effect(() => overview.run((signal) => api.youTube(signal)))
  $effect(() => ytdlp.run((signal) => api.ytdlp(signal)))
  $effect(() => versions.run((signal) => api.ytdlpVersions(signal)))
  $effect(() => settings.run((signal) => api.youTubeSettings(signal)))

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

  // ---------------------------------------------------------------- reading YouTube

  let comments = $state<YouTubeCommentsChoice>('off')
  let commentsMax = $state<number | null>(500)
  let commentsInvalid = $state<string | null>(null)
  let pauseSeconds = $state<number | null>(10)
  let holdMinutes = $state<number | null>(60)
  let syncEveryHours = $state<number | null>(6)
  let pauseInvalid = $state(false)
  let holdInvalid = $state(false)
  let syncInvalid = $state(false)

  // The form starts on what's saved, and comes back to it after each save (or refusal).
  function showSaved(saved: YouTubeSettings) {
    comments = saved.comments
    commentsMax = saved.commentsMax
    pauseSeconds = saved.pauseSeconds
    holdMinutes = saved.holdMinutes
    syncEveryHours = saved.syncEveryHours
  }

  let settingsShown = false
  $effect(() => {
    if (!settingsShown && settings.data) {
      settingsShown = true
      showSaved(settings.data)
    }
  })

  async function saveComments(event: SubmitEvent) {
    event.preventDefault()
    const max = commentsMax
    commentsInvalid = comments === 'top' && !wholeFrom(max, 1) ? t('youTube.comments.max.invalid') : null
    pauseInvalid = !wholeFrom(pauseSeconds, 0)
    holdInvalid = !wholeFrom(holdMinutes, 1)
    syncInvalid = !wholeFrom(syncEveryHours, 0) || (syncEveryHours ?? 0) > MAX_SYNC_EVERY_HOURS
    if (
      commentsInvalid !== null ||
      pauseInvalid ||
      holdInvalid ||
      syncInvalid ||
      pauseSeconds === null ||
      holdMinutes === null ||
      syncEveryHours === null
    ) {
      return
    }
    pending = true
    refused = null
    try {
      // Off and all keep the number for the next time the top ones are chosen.
      await api.saveYouTubeSettings(
        comments,
        commentsMax ?? settings.data?.commentsMax ?? 500,
        pauseSeconds,
        holdMinutes,
        syncEveryHours,
      )
    } catch (error) {
      refused = t('youTube.refused', { reason: reasonOf(error) })
    } finally {
      pending = false
      await settings.refresh()
      if (settings.data) {
        showSaved(settings.data)
      }
    }
  }

  function wholeFrom(value: number | null, least: number): boolean {
    return value !== null && Number.isInteger(value) && value >= least
  }

  function savedComments(saved: YouTubeSettings): string {
    return saved.comments === 'top'
      ? t('youTube.comments.savedTop', { max: formatNumber(saved.commentsMax, i18n.lang) })
      : t(`youTube.comments.saved.${saved.comments}`)
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
        <div class="head">
          <h2 id="{uid}-cookie">{t('youTube.cookie')}</h2>
          <HelpButton zone={t('youTube.cookie')}><p>{t('youTube.cookie.hint')}</p></HelpButton>
        </div>
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
    <div class="head">
      <h2 id="{uid}-ytdlp">yt-dlp</h2>
      <HelpButton zone="yt-dlp">
        <p>{t('ytdlp.intro')}</p>
        <dl>
          <dt>{t('ytdlp.latest')}</dt>
          <dd>{t('ytdlp.latestHint')}</dd>
          <dt>{t('ytdlp.official')}</dt>
          <dd>{t('ytdlp.officialHint')}</dd>
          <dt>{t('ytdlp.nightly')}</dt>
          <dd>{t('ytdlp.nightlyHint')}</dd>
        </dl>
        <p>{t('ytdlp.hint')}</p>
      </HelpButton>
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
            {#if latest}<small>{t('ytdlp.latestNow', { version: latest })}</small>{/if}
          </label>
          {#if versions.data}
            {@const offered = versions.data}
            {@const image = state.bundled ? state.running : null}
            <label class="choice">
              <input type="radio" name="{uid}-kind" value="official" bind:group={kind} />
              <b>{t('ytdlp.official')}</b>
              <select bind:value={official} disabled={kind !== 'official'} aria-label={t('ytdlp.official')}>
                {#each offered.official as release (release.version)}
                  <option value={release.version}>{optionLabel(release.version, release.publishedAt, image)}</option>
                {/each}
              </select>
            </label>
            <label class="choice">
              <input type="radio" name="{uid}-kind" value="nightly" bind:group={kind} />
              <b>{t('ytdlp.nightly')}</b>
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
    {/if}
  </section>

  <section class="card" aria-labelledby="{uid}-reading">
    <div class="head">
      <h2 id="{uid}-reading">{t('youTube.reading')}</h2>
      <HelpButton zone={t('youTube.reading')}>
        <p>{t('youTube.reading.intro')}</p>
        <dl>
          <dt>{t('youTube.comments')}, {t('youTube.comments.off')}</dt>
          <dd>{t('youTube.comments.offHint')}</dd>
          <dt>{t('youTube.comments')}, {t('youTube.comments.all')}</dt>
          <dd>{t('youTube.comments.allHint')}</dd>
          <dt>{t('youTube.easy.pause')}</dt>
          <dd>{t('youTube.easy.pauseHint')}</dd>
          <dt>{t('youTube.easy.hold')}</dt>
          <dd>{t('youTube.easy.holdHint')}</dd>
          <dt>{t('youTube.sync.every')}</dt>
          <dd>{t('youTube.sync.everyHint')}</dd>
        </dl>
      </HelpButton>
    </div>
    {#if settings.status === 'loading'}
      <Skeleton layout="page" />
    {:else if settings.status === 'error' && settings.error}
      <ErrorState error={settings.error} onretry={() => settings.retry()} />
    {:else if settings.data}
      {@const current = settings.data}
      <form class="choose" onsubmit={saveComments} novalidate>
        <fieldset>
          <legend>{t('youTube.comments')}</legend>
          <label class="choice">
            <input type="radio" name="{uid}-comments" value="off" bind:group={comments} />
            <b>{t('youTube.comments.off')}</b>
          </label>
          <label class="choice">
            <input type="radio" name="{uid}-comments" value="top" bind:group={comments} />
            <b>{t('youTube.comments.top')}</b>
            <small>{t('youTube.comments.topHint')}</small>
            <span class="max">
              <input
                type="number"
                min="1"
                step="1"
                inputmode="numeric"
                aria-label={t('youTube.comments.max')}
                aria-invalid={commentsInvalid !== null}
                bind:value={commentsMax}
                disabled={comments !== 'top'}
              />
              <small>{t('youTube.comments.perVideo')}</small>
            </span>
            {#if commentsInvalid}
              <small class="invalid" role="alert">{commentsInvalid}</small>
            {/if}
          </label>
          <label class="choice">
            <input type="radio" name="{uid}-comments" value="all" bind:group={comments} />
            <b>{t('youTube.comments.all')}</b>
            <small>{t('youTube.comments.allShort')}</small>
          </label>
        </fieldset>
        <fieldset>
          <legend>{t('youTube.easy')}</legend>
          <label class="easy">
            <span>
              <b>{t('youTube.easy.pause')}</b>
            </span>
            <span class="number">
              <input
                type="number"
                min="0"
                step="1"
                inputmode="numeric"
                aria-invalid={pauseInvalid}
                bind:value={pauseSeconds}
              />
              <small>{t('youTube.easy.seconds')}</small>
            </span>
            {#if pauseInvalid}
              <small class="invalid" role="alert">{t('youTube.easy.pause.invalid')}</small>
            {/if}
          </label>
          <label class="easy">
            <span>
              <b>{t('youTube.easy.hold')}</b>
            </span>
            <span class="number">
              <input
                type="number"
                min="1"
                step="1"
                inputmode="numeric"
                aria-invalid={holdInvalid}
                bind:value={holdMinutes}
              />
              <small>{t('youTube.easy.minutes')}</small>
            </span>
            {#if holdInvalid}
              <small class="invalid" role="alert">{t('youTube.easy.hold.invalid')}</small>
            {/if}
          </label>
        </fieldset>
        <fieldset>
          <legend>{t('youTube.sync')}</legend>
          <label class="easy">
            <span>
              <b>{t('youTube.sync.every')}</b>
            </span>
            <span class="number">
              <input
                type="number"
                min="0"
                max={MAX_SYNC_EVERY_HOURS}
                step="1"
                inputmode="numeric"
                aria-invalid={syncInvalid}
                bind:value={syncEveryHours}
              />
              <small>{t('youTube.sync.hours')}</small>
            </span>
            {#if syncInvalid}
              <small class="invalid" role="alert">{t('youTube.sync.every.invalid')}</small>
            {/if}
          </label>
        </fieldset>
        <div class="actions">
          <Button type="submit" variant="filled" disabled={pending}>{t('youTube.comments.save')}</Button>
        </div>
      </form>
      <p class="hint">
        {savedComments(current)}
        {t('youTube.easy.saved', { pause: current.pauseSeconds, hold: current.holdMinutes })}
        {current.syncEveryHours > 0
          ? t('youTube.sync.saved', { hours: current.syncEveryHours })
          : t('youTube.sync.savedByHand')}
        {#if current.changedBy && current.changedAt}
          · {t('youTube.comments.changedBy', {
            by: current.changedBy,
            ago: formatAge(current.changedAt, i18n.lang),
          })}
        {/if}
      </p>
    {/if}
  </section>

  {#if settings.data}
    <DownloadSettings current={settings.data} onsaved={() => settings.refresh()} />
  {/if}
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

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
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

  .max,
  .number {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .max {
    grid-column: 2;
  }

  .max input,
  .number input {
    width: 8rem;
    height: var(--button-height);
    padding: 0 var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-input-bg);
    color: var(--color-text);
    font: inherit;
  }

  .max input[aria-invalid='true'],
  .number input[aria-invalid='true'] {
    border-color: var(--color-error);
  }

  .invalid {
    grid-column: 2;
    color: var(--color-error);
  }

  .easy .invalid {
    grid-column: 1 / -1;
  }

  .easy {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: var(--space-1) var(--space-4);
    align-items: center;
    padding: var(--space-3) var(--space-4);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
  }

  .easy > span:first-child {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .easy b {
    font-weight: var(--font-weight-medium);
  }

  .number input {
    width: 6rem;
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
