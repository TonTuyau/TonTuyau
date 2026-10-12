<script lang="ts">
  // The import history: one bar per sync run that brought new videos (oldest left), stacked by type; a run of 1.1,
  // which didn't record the types, is one grey bar. Hovering a bar shows its date, time and counts. The same numbers
  // are in a table for screen readers.
  import type { SyncImport, VideoType } from '../lib/api/types'
  import { formatNumber } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'

  let { imports }: { imports: SyncImport[] } = $props()

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

  const added = $derived(imports.reduce((sum, item) => sum + item.total, 0))
  const max = $derived(niceMax(Math.max(1, ...imports.map((item) => item.total))))
  const slot = $derived((width - left) / Math.max(1, imports.length))
  const barWidth = $derived(Math.min(Math.max(4, slot * 0.62), 28))
  // A date under every 5th bar, every 10th on a phone.
  const labelEvery = $derived(width < 480 ? 10 : 5)
  const unrecorded = $derived(imports.some((item) => item.byType === null))
  const dateFormat = $derived(new Intl.DateTimeFormat(i18n.lang, { day: 'numeric', month: 'short' }))
  const timeFormat = $derived(new Intl.DateTimeFormat(i18n.lang, { hour: '2-digit', minute: '2-digit' }))

  /** The axis' top: the next 1, 2, 2.5 or 5 times a power of ten. */
  function niceMax(value: number): number {
    const step = 10 ** Math.floor(Math.log10(value))
    return ([1, 2, 2.5, 5, 10].find((m) => m * step >= value) ?? 10) * step
  }

  const y = (value: number) => top + plotHeight - (value / max) * plotHeight

  /** The bar's pieces from the bottom: by type, or the whole when the types weren't recorded. */
  function segments(item: SyncImport): { kind: VideoType | 'unknown'; from: number; to: number }[] {
    const parts: [VideoType | 'unknown', number][] = item.byType
      ? [
          ['video', item.byType.videos],
          ['short', item.byType.shorts],
          ['stream', item.byType.streams],
        ]
      : [['unknown', item.total]]
    let base = 0
    return parts
      .filter(([, count]) => count > 0)
      .map(([kind, count]) => {
        const from = base
        base += count
        return { kind, from, to: base }
      })
  }

  function when(item: SyncImport): string {
    const at = new Date(item.at)
    return `${dateFormat.format(at)}, ${timeFormat.format(at)}`
  }

  function detail(item: SyncImport): string {
    const byType = item.byType
    if (!byType) {
      return t('dashboard.notRecorded')
    }
    const counts = { video: byType.videos, short: byType.shorts, stream: byType.streams }
    return types.map((type) => tp(countKeys[type], counts[type])).join(' · ')
  }

  /** The bar under the pointer: each one's whole column (taller than the bar) is its hover zone. */
  function onpointermove(event: PointerEvent) {
    const index = event.target instanceof Element ? event.target.getAttribute('data-index') : null
    active = index === null ? null : Number(index)
  }

  const shown = $derived(active === null ? null : (imports[active] ?? null))
</script>

{#if imports.length === 0}
  <p class="empty">{t('dashboard.noImports')}</p>
{:else}
  <p class="sub">
    {tp('dashboard.importsSummary', imports.length, undefined, { videos: formatNumber(added, i18n.lang) })}
  </p>
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
      aria-label={tp('dashboard.chart', imports.length)}
      {onpointermove}
      onpointerleave={() => (active = null)}
    >
      {#each [0, 1, 2, 3, 4] as tick (tick)}
        {@const value = (max / 4) * tick}
        <line class="grid" x1={left} x2={width} y1={y(value)} y2={y(value)} />
        <text class="axis" x={left - 6} y={y(value) + 4} text-anchor="end">{formatNumber(value, i18n.lang)}</text>
      {/each}
      {#each imports as item, index (index)}
        {@const x = left + index * slot + (slot - barWidth) / 2}
        <g class="bar" class:active={active === index} data-testid="import-bar">
          {#each segments(item) as part (part.kind)}
            <rect class={part.kind} {x} y={y(part.to)} width={barWidth} height={y(part.from) - y(part.to)} rx="1.5" />
          {/each}
        </g>
        {#if index % labelEvery === 0 || index === imports.length - 1}
          <text class="axis" x={x + barWidth / 2} y={height - 6} text-anchor="middle">
            {dateFormat.format(new Date(item.at))}
          </text>
        {/if}
        <rect class="hit" data-index={index} x={left + index * slot} y={top} width={slot} height={plotHeight} />
      {/each}
    </svg>
    {#if shown && active !== null}
      <div class="tip" role="status" style:--x={(left + (active + 0.5) * slot) / width}>
        <b>{when(shown)}</b>
        <span>{tp('dashboard.newVideos', shown.total)}</span>
        <span>{detail(shown)}</span>
      </div>
    {/if}
  </div>

  <!-- A table is never narrower than its content: the wrapper hides it. -->
  <div class="visually-hidden">
    <table>
      <caption>{tp('dashboard.chart', imports.length)}</caption>
      <tbody>
        {#each imports as item, index (index)}
          <tr>
            <th scope="row">{when(item)}</th>
            <td>{tp('dashboard.newVideos', item.total)}</td>
            <td>{detail(item)}</td>
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
