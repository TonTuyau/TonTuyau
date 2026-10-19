<script module lang="ts">
  export type ImportScale = 'linear' | 'log'
</script>

<script lang="ts">
  // The import history of a period: one bar per step (an import, a day, a week or a month, oldest left), stacked
  // by type, the runs of 1.1 (which didn't record the types) in grey. Every step has its slot, an empty one too, so
  // time stays linear. Hovering a bar names its step and its counts. Dates are those of TONTUYAU_TZ, as
  // the backend counted them. The vertical scale is linear or logarithmic (log10(1 + n): 0 stays on the baseline and
  // a single video still has a bar), for a big import not to flatten the others. The same numbers are in a table for
  // screen readers.
  import type { ImportBar, ImportHistory, VideoType } from '../lib/api/types'
  import { formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'

  let { history, scale }: { history: ImportHistory; scale: ImportScale } = $props()

  // Drawn at the card's own width (in CSS pixels, so the labels keep their size on a phone); 640 until measured.
  let measured = $state(0)
  let chart: HTMLDivElement | undefined = $state()
  const width = $derived(Math.max(240, measured || 640))
  const height = 220
  const left = 48
  const top = 8
  const bottom = 22
  const plotHeight = height - top - bottom
  const types: VideoType[] = ['video', 'short', 'stream']
  const countKeys = { video: 'count.videos', short: 'count.shorts', stream: 'count.streams' } as const

  let active = $state<number | null>(null)

  // Not bind:clientWidth: it needs ResizeObserver, which jsdom (the tests) doesn't have. Without it, the width at
  // first drawing stays.
  $effect(() => {
    if (!chart) {
      return
    }
    measured = chart.clientWidth
    if (typeof ResizeObserver === 'undefined') {
      return
    }
    const observer = new ResizeObserver(([entry]) => (measured = entry?.contentRect.width ?? 0))
    observer.observe(chart)
    return () => observer.disconnect()
  })

  const bars = $derived(history.bars)
  const max = $derived(niceMax(Math.max(1, ...bars.map((bar) => bar.total))))
  const slot = $derived((width - left) / Math.max(1, bars.length))
  const barWidth = $derived(Math.min(Math.max(2, slot * 0.62), 28))
  // About 7 dates under the bars, 4 on a phone.
  const labelEvery = $derived(Math.max(1, Math.ceil(bars.length / (width < 480 ? 4 : 7))))
  const unrecorded = $derived(bars.some((bar) => bar.notRecorded > 0))

  const format = (options: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat(i18n.lang, { ...options, timeZone: history.timeZone })
  const dayFormat = $derived(format({ day: 'numeric', month: 'short' }))
  const timeFormat = $derived(format({ hour: '2-digit', minute: '2-digit' }))
  const monthFormat = $derived(format({ month: 'short', year: '2-digit' }))
  const longMonthFormat = $derived(format({ month: 'long', year: 'numeric' }))

  /** The axis' top: the next 1, 2, 2.5 or 5 times a power of ten. */
  function niceMax(value: number): number {
    const step = 10 ** Math.floor(Math.log10(value))
    return ([1, 2, 2.5, 5, 10].find((m) => m * step >= value) ?? 10) * step
  }

  /** The value's height, from 0 (the baseline) to 1 (the axis' top). */
  const share = (value: number) => (scale === 'log' ? Math.log10(1 + value) / Math.log10(1 + max) : value / max)
  const y = (value: number) => top + plotHeight - share(value) * plotHeight
  // Linear: 5 even steps. Log: the powers of ten under the top, then the top.
  const ticks = $derived(
    scale === 'log'
      ? [0, ...[1, 10, 100, 1_000, 10_000, 100_000, 1_000_000].filter((value) => value < max), max]
      : [0, 1, 2, 3, 4].map((tick) => (max / 4) * tick),
  )

  /**
   * The bar's pieces from the bottom: by type, then the part whose types weren't recorded. The bar is as tall as its
   * total; the pieces share that height by their counts (stacking the counts on a log axis would make the bottom one
   * look far bigger than it is; on a linear one, it's the same).
   */
  function segments(bar: ImportBar): { kind: VideoType | 'unknown'; y: number; height: number }[] {
    const parts: [VideoType | 'unknown', number][] = [
      ['video', bar.byType.videos],
      ['short', bar.byType.shorts],
      ['stream', bar.byType.streams],
      ['unknown', bar.notRecorded],
    ]
    const barHeight = y(0) - y(bar.total)
    let base = y(0)
    return parts
      .filter(([, count]) => count > 0)
      .map(([kind, count]) => {
        const height = (count / bar.total) * barHeight
        base -= height
        return { kind, y: base, height }
      })
  }

  /** Under the bar: the run's time, the day, the week's Monday, the month. */
  function label(bar: ImportBar): string {
    const at = new Date(bar.start)
    if (history.step === 'run') {
      return timeFormat.format(at)
    }
    return (history.step === 'month' ? monthFormat : dayFormat).format(at)
  }

  /** The step, named: "12 Oct, 09:30" (a run), "12 Oct", "Week of 6 Oct", "October 2026". */
  function when(bar: ImportBar): string {
    const at = new Date(bar.start)
    switch (history.step) {
      case 'run':
        return `${dayFormat.format(at)}, ${timeFormat.format(at)}`
      case 'week':
        return t('dashboard.weekOf', { date: dayFormat.format(at) })
      case 'month':
        return longMonthFormat.format(at)
      default:
        return dayFormat.format(at)
    }
  }

  function detail(bar: ImportBar): string {
    if (bar.notRecorded === bar.total) {
      return t('dashboard.notRecorded')
    }
    const counts = { video: bar.byType.videos, short: bar.byType.shorts, stream: bar.byType.streams }
    const parts = types.map((type) => tp(countKeys[type], counts[type]))
    if (bar.notRecorded > 0) {
      parts.push(tp('dashboard.notRecordedCount', bar.notRecorded))
    }
    return parts.join(' · ')
  }

  /** The bar under the pointer: each one's whole column (taller than the bar) is its hover zone. */
  function onpointermove(event: PointerEvent) {
    const index = event.target instanceof Element ? event.target.getAttribute('data-index') : null
    active = index === null ? null : Number(index)
  }

  const shown = $derived(active === null ? null : (bars[active] ?? null))
</script>

{#if history.total === 0}
  <p class="empty">{t('dashboard.noImports')}</p>
{:else}
  <p class="sub">{tp('dashboard.newVideos', history.total)}</p>
  <ul class="legend" aria-hidden="true">
    {#each types as type (type)}
      <li><span class="{type} swatch"></span>{t(`type.${type}`)}</li>
    {/each}
    {#if unrecorded}
      <li><span class="unknown swatch"></span>{t('dashboard.notRecorded')}</li>
    {/if}
  </ul>

  <div class="chart" bind:this={chart}>
    <svg
      viewBox="0 0 {width} {height}"
      role="img"
      aria-label={t(`dashboard.chart.${history.step}`)}
      {onpointermove}
      onpointerleave={() => (active = null)}
    >
      {#each ticks as value (value)}
        <line class="grid" x1={left} x2={width} y1={y(value)} y2={y(value)} />
        <text class="axis" x={left - 6} y={y(value) + 4} text-anchor="end">{formatNumber(value, i18n.lang)}</text>
      {/each}
      {#each bars as bar, index (index)}
        {@const x = left + index * slot + (slot - barWidth) / 2}
        <g class="bar" class:active={active === index} data-testid="import-bar">
          {#each segments(bar) as part (part.kind)}
            <rect class={part.kind} {x} y={part.y} width={barWidth} height={part.height} rx="1.5" />
          {/each}
        </g>
        {#if index % labelEvery === 0}
          <text class="axis" x={x + barWidth / 2} y={height - 6} text-anchor="middle">{label(bar)}</text>
        {/if}
        <rect class="hit" data-index={index} x={left + index * slot} y={top} width={slot} height={plotHeight} />
      {/each}
    </svg>
    {#if shown && active !== null}
      <div class="tip" role="status" style:--x={(left + (active + 0.5) * slot) / width}>
        <b>{when(shown)}</b>
        <span>{tp('dashboard.newVideos', shown.total)}</span>
        {#if shown.total > 0}
          <span>{detail(shown)}</span>
        {/if}
      </div>
    {/if}
  </div>

  <!-- A table is never narrower than its content: the wrapper hides it. The steps with new videos only. -->
  <div class="visually-hidden">
    <table>
      <caption>{t(`dashboard.chart.${history.step}`)}</caption>
      <tbody>
        {#each bars.filter((bar) => bar.total > 0) as bar (bar.start)}
          <tr>
            <th scope="row">{when(bar)}</th>
            <td>{tp('dashboard.newVideos', bar.total)}</td>
            <td>{detail(bar)}</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
{/if}

<style>
  .sub,
  .empty {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .legend {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-4);
    margin: 0;
    padding: 0;
    list-style: none;
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .legend li {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
  }

  .swatch {
    width: var(--space-3);
    height: var(--space-3);
    border-radius: var(--radius-sm);
  }

  .swatch.video {
    background: var(--color-type-video);
  }

  .swatch.short {
    background: var(--color-type-short);
  }

  .swatch.stream {
    background: var(--color-type-stream);
  }

  .swatch.unknown {
    background: var(--color-type-unknown);
  }

  .chart {
    position: relative;
  }

  svg {
    display: block;
    width: 100%;
    height: auto;
    overflow: visible;
  }

  .grid {
    stroke: var(--color-chart-grid);
  }

  .axis {
    fill: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  /* The card's colour between the stacked pieces. */
  .bar rect {
    stroke: var(--color-bg-elevated);
  }

  rect.video {
    fill: var(--color-type-video);
  }

  rect.short {
    fill: var(--color-type-short);
  }

  rect.stream {
    fill: var(--color-type-stream);
  }

  rect.unknown {
    fill: var(--color-type-unknown);
  }

  .bar.active rect {
    opacity: 0.75;
  }

  .hit {
    fill: transparent;
  }

  .tip {
    position: absolute;
    top: 0;
    left: clamp(calc(var(--chart-tip-width) / 2), calc(var(--x) * 100%), calc(100% - var(--chart-tip-width) / 2));
    display: flex;
    flex-direction: column;
    width: max-content;
    max-width: var(--chart-tip-width);
    padding: var(--space-2) var(--space-3);
    border: var(--border-width) solid var(--color-border);
    border-radius: var(--radius-md);
    background: var(--color-bg-elevated);
    box-shadow: var(--shadow-menu);
    font-size: var(--font-size-xs);
    pointer-events: none;
    transform: translateX(-50%);
  }

  .tip b {
    font-weight: var(--font-weight-medium);
  }

  .tip span {
    color: var(--color-text-secondary);
  }
</style>
