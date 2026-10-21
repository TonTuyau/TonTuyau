<script lang="ts">
  // The admin dashboard: the library's totals and its play time by type, as GET /api/admin/dashboard gives them (the
  // shares and averages too), the import history of the chosen period (its own call: changing the period reloads the
  // chart only; the choice is remembered), drawn on a linear or log scale (remembered too, no call), and the channels
  // (their own calls too, a page at a time: ChannelTable). A 401 means the session ended meanwhile: the shell then goes
  // to the login page.
  import ChannelTable from '../components/ChannelTable.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import ImportChart, { type ImportScale } from '../components/ImportChart.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import { api } from '../lib/api/client'
  import type { Dashboard, ImportHistory, ImportPeriod, VideoType } from '../lib/api/types'
  import { auth } from '../lib/auth.svelte'
  import { formatAge, formatNumber, formatPlayTime } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import type { TextKey } from '../lib/i18n/types'
  import { Remote } from '../lib/remote.svelte'
  import { readPreference, writePreference } from '../lib/storage'
  import { pageTitle } from '../lib/title.svelte'

  const IMPORT_PERIOD_STORAGE_KEY = 'tontuyau.importPeriod'
  const IMPORT_SCALE_STORAGE_KEY = 'tontuyau.importScale'
  const periods: ImportPeriod[] = ['day', 'week', 'month', 'year', 'all']
  const scales: ImportScale[] = ['linear', 'log']
  const saved = readPreference(IMPORT_PERIOD_STORAGE_KEY)
  const savedScale = readPreference(IMPORT_SCALE_STORAGE_KEY)

  const remote = new Remote<Dashboard>()
  const imports = new Remote<ImportHistory>()
  let period = $state<ImportPeriod>(periods.find((p) => p === saved) ?? 'month')
  let scale = $state<ImportScale>(scales.find((s) => s === savedScale) ?? 'linear')

  $effect(() => remote.run((signal) => api.dashboard(signal)))
  $effect(() => imports.run((signal) => api.imports(period, signal)))

  $effect(() => {
    if (remote.error?.status === 401 || imports.error?.status === 401) {
      auth.expired()
    }
  })

  function choose(value: ImportPeriod) {
    period = value
    writePreference(IMPORT_PERIOD_STORAGE_KEY, value)
  }

  function chooseScale(value: ImportScale) {
    scale = value
    writePreference(IMPORT_SCALE_STORAGE_KEY, value)
  }

  pageTitle(() => t('admin.dashboard'))

  const typeLabels: Record<VideoType, TextKey> = { video: 'type.video', short: 'type.short', stream: 'type.stream' }
  const percent = $derived(new Intl.NumberFormat(i18n.lang, { style: 'percent', maximumFractionDigits: 0 }))
  const number = (value: number) => formatNumber(value, i18n.lang)
</script>

<div class="page">
  <header>
    <h1>{t('admin.dashboard')}</h1>
    {#if remote.data}
      <p class="synced">
        {remote.data.lastSync
          ? t('dashboard.synced', { age: formatAge(remote.data.lastSync, i18n.lang) })
          : t('dashboard.neverSynced')}
      </p>
    {/if}
  </header>

  {#if remote.status === 'loading'}
    <Skeleton layout="page" />
  {:else if remote.status === 'error' && remote.error}
    <ErrorState error={remote.error} onretry={() => remote.retry()} />
  {:else if remote.data}
    {@const data = remote.data}
    <dl class="totals">
      <div class="total">
        <dt>{t('dashboard.channels')}</dt>
        <dd>{number(data.totals.channels)}</dd>
      </div>
      <div class="total">
        <dt>{t('type.video')}</dt>
        <dd>{number(data.totals.videos)}</dd>
      </div>
      <div class="total">
        <dt>{t('type.short')}</dt>
        <dd>{number(data.totals.shorts)}</dd>
      </div>
      <div class="total">
        <dt>{t('type.stream')}</dt>
        <dd>{number(data.totals.streams)}</dd>
      </div>
      <div class="total alert" title={t('dashboard.goneHint')}>
        <dt>{t('dashboard.gone')}</dt>
        <dd>{number(data.totals.gone)}</dd>
      </div>
    </dl>

    <div class="pair">
      <section class="card" aria-labelledby="play-time">
        <h2 id="play-time">{t('dashboard.playTime')}</h2>
        <p class="big">
          {formatPlayTime(data.playTime.totalSeconds, i18n.lang)}
          <small>{tp('dashboard.nonStop', Math.round(data.playTime.totalSeconds / 86_400))}</small>
        </p>
        <div class="share" aria-hidden="true">
          {#each data.playTime.types as type (type.type)}
            <span class="{type.type} segment" style:--share={type.share}></span>
          {/each}
        </div>
        <dl class="kinds">
          {#each data.playTime.types as type (type.type)}
            <div class="kind">
              <span class="{type.type} dot" aria-hidden="true"></span>
              <dt>
                {t(typeLabels[type.type])}
                <small>
                  {t('dashboard.typeDetail', {
                    share: percent.format(type.share),
                    average: formatPlayTime(type.averageSeconds, i18n.lang),
                  })}
                </small>
              </dt>
              <dd>{formatPlayTime(type.seconds, i18n.lang)}</dd>
            </div>
          {/each}
        </dl>
      </section>

      <section class="card" aria-labelledby="imports">
        <h2 id="imports">{t('dashboard.imports')}</h2>
        <div class="tools">
          <div class="periods" role="group" aria-label={t('dashboard.period')}>
            {#each periods as value (value)}
              <button type="button" class="chip" aria-pressed={period === value} onclick={() => choose(value)}>
                {t(`dashboard.period.${value}`)}
              </button>
            {/each}
          </div>
          <div class="scale" role="group" aria-label={t('dashboard.scale')}>
            {#each scales as value (value)}
              <button type="button" aria-pressed={scale === value} onclick={() => chooseScale(value)}>
                {t(`dashboard.scale.${value}`)}
              </button>
            {/each}
          </div>
        </div>
        {#if imports.status === 'error' && imports.error}
          <ErrorState error={imports.error} onretry={() => imports.retry()} />
        {:else if imports.data}
          <!-- The previous period's chart stays, dimmed, until the next one comes. -->
          <div class:loading={imports.status === 'loading'} aria-busy={imports.status === 'loading'}>
            <ImportChart history={imports.data} {scale} />
          </div>
        {:else}
          <p class="waiting" role="status">{t('state.loading')}</p>
        {/if}
      </section>
    </div>

    <ChannelTable count={data.totals.channels} />
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

  .synced {
    margin-top: var(--space-1);
    color: var(--color-text-secondary);
  }

  dl,
  dd {
    margin: 0;
  }

  .totals {
    display: grid;
    grid-template-columns: repeat(5, minmax(0, 1fr));
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-lg);
    background: var(--color-bg-elevated);
  }

  .total {
    padding: var(--space-4) var(--space-5);
    border-left: var(--border-width) solid var(--color-divider);
  }

  .total:first-child {
    border-left: none;
  }

  .total dt {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .total dd {
    margin-top: var(--space-1);
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .alert dd {
    color: var(--color-error);
  }

  .pair {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 2fr);
    gap: var(--space-4);
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

  /* The periods on the left, the scale on the right (under them when the card is too narrow). */
  .tools {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-2);
  }

  .periods {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  /* A switch: the track, the chosen side raised on it. */
  .scale {
    display: inline-flex;
    padding: var(--space-1);
    border-radius: var(--radius-md);
    background: var(--color-surface);
  }

  .scale button {
    height: calc(var(--chip-height) - 2 * var(--space-1));
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-sm);
    background: transparent;
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .scale button[aria-pressed='true'] {
    background: var(--color-bg-elevated);
    color: var(--color-text);
    box-shadow: 0 0 0 var(--border-width) var(--color-border);
  }

  .chip {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-surface);
    font-weight: var(--font-weight-medium);
  }

  .chip:hover {
    background: var(--color-surface-hover);
  }

  .chip[aria-pressed='true'] {
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
  }

  .loading {
    opacity: 0.6;
  }

  .waiting {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .big {
    font-size: var(--font-size-stat);
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .big small {
    margin-left: var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-regular);
  }

  .share {
    display: flex;
    gap: var(--border-width);
    height: var(--space-2);
    overflow: hidden;
    border-radius: var(--radius-sm);
    background: var(--color-surface);
  }

  .segment {
    flex: var(--share) 0 0;
  }

  .kinds {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
  }

  .kind {
    display: grid;
    grid-template-columns: var(--space-3) 1fr auto;
    align-items: baseline;
    gap: var(--space-3);
  }

  .dot {
    align-self: center;
    width: var(--space-3);
    height: var(--space-3);
    border-radius: var(--radius-sm);
  }

  .kind small {
    margin-left: var(--space-2);
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .kind dd {
    font-weight: var(--font-weight-medium);
    font-variant-numeric: tabular-nums;
  }

  .video {
    background: var(--color-type-video);
  }

  .short {
    background: var(--color-type-short);
  }

  .stream {
    background: var(--color-type-stream);
  }

  @media (max-width: 1000px) {
    .pair {
      grid-template-columns: minmax(0, 1fr);
    }
  }

  @media (max-width: 900px) {
    .totals {
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    .total:nth-child(4) {
      border-left: none;
    }

    .total:nth-child(n + 4) {
      border-top: var(--border-width) solid var(--color-divider);
    }
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-4);
    }

    .totals {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .total,
    .total:nth-child(4) {
      border-left: none;
      border-top: var(--border-width) solid var(--color-divider);
    }

    .total:nth-child(-n + 2) {
      border-top: none;
    }

    .total:nth-child(even) {
      border-left: var(--border-width) solid var(--color-divider);
    }

    .total dd {
      font-size: var(--font-size-xl);
    }
  }
</style>
