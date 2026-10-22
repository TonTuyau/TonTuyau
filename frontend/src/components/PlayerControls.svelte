<script lang="ts">
  // TonTuyau's own controls on the watch page (2.0, C1), over the bottom of the video: hidden after 3 s without moving
  // while it plays, shown while it's paused, a menu is open or the keyboard is in them. The bar shows the chapters as
  // segments (hovered: the time and the chapter there; dragged: the video follows); then play, the volume (remembered),
  // the time and the chapter playing (a click lists them), the ⚙ menu (speed, loop), the small/large player and full
  // screen. A click on the video plays or pauses (a double click: full screen); on a phone a tap does, and shows the
  // controls for a moment. Keyboard (the user's small set): Space, ←/→ 5 s, M mute, F full screen anywhere on the page
  // but in a field; ↑/↓ the volume once the player has the focus (elsewhere they scroll the page).
  import type { Chapter } from '../lib/api/types'
  import { formatDuration } from '../lib/format'
  import { t } from '../lib/i18n/i18n.svelte'
  import { barSegments, filledPart, type Segment } from '../lib/playerBar'
  import type { PlayerSize } from '../lib/playerSize.svelte'
  import {
    restoreVolume,
    setVolume as keepVolume,
    toggleMute as keepMute,
    volumeIcon as iconFor,
  } from '../lib/playerVolume'
  import Icon from './Icon.svelte'

  let {
    media,
    box,
    chapters,
    duration,
    size = 'small',
    onsizechange,
  }: {
    media: HTMLVideoElement
    /** The player's box: what goes full screen. */
    box: HTMLElement
    chapters: Chapter[]
    /** The API's length, until the file says its own. */
    duration: number
    size?: PlayerSize
    onsizechange?: (size: PlayerSize) => void
  } = $props()

  const IDLE_MS = 3_000
  const FLASH_MS = 500
  const STEP_SECONDS = 5
  const VOLUME_STEP = 0.05
  const SPEEDS = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2]

  let paused = $state(true)
  let started = $state(false)
  let time = $state(0)
  let length = $state(0)
  let buffered = $state(0)
  let volume = $state(1)
  let muted = $state(false)
  let rate = $state(1)
  let loop = $state(false)
  let waiting = $state(false)
  let fullscreen = $state(false)
  let active = $state(true)
  let focused = $state(false)
  let dragging = $state(false)
  let menu = $state<'settings' | 'chapters' | null>(null)
  /** The time under the pointer on the bar; null when it isn't on it. */
  let hover = $state<number | null>(null)
  let flash = $state<'play' | 'pause' | null>(null)

  const shown = $derived(paused || active || focused || dragging || menu !== null)
  const total = $derived(length > 0 ? length : duration)
  const segments = $derived(barSegments(chapters, total))
  const playing = $derived(chapterAt(time))
  const tip = $derived(hover === null ? null : { at: hover, chapter: chapterAt(hover) })

  // The file's state, as its events say it.
  $effect(() => {
    const m = media
    const sync = () => {
      paused = m.paused
      // Already playing when the controls came (it started by itself): no big play button.
      started ||= !m.paused
      time = m.currentTime
      length = Number.isFinite(m.duration) ? m.duration : 0
      volume = m.volume
      muted = m.muted
      rate = m.playbackRate
      loop = m.loop
      buffered = bufferedEnd(m)
    }
    const onplay = () => (started = true)
    const onwaiting = () => (waiting = true)
    const onready = () => (waiting = false)
    const events = [
      'play',
      'pause',
      'timeupdate',
      'durationchange',
      'loadedmetadata',
      'volumechange',
      'ratechange',
      'progress',
      'seeked',
      'ended',
    ]
    for (const name of events) m.addEventListener(name, sync)
    m.addEventListener('play', onplay)
    m.addEventListener('waiting', onwaiting)
    m.addEventListener('playing', onready)
    m.addEventListener('canplay', onready)
    sync()
    return () => {
      for (const name of events) m.removeEventListener(name, sync)
      m.removeEventListener('play', onplay)
      m.removeEventListener('waiting', onwaiting)
      m.removeEventListener('playing', onready)
      m.removeEventListener('canplay', onready)
    }
  })

  // The volume as left last time (this browser), on the shorts too.
  $effect(() => restoreVolume(media))

  $effect(() => {
    const onchange = () => (fullscreen = document.fullscreenElement === box)
    document.addEventListener('fullscreenchange', onchange)
    return () => document.removeEventListener('fullscreenchange', onchange)
  })

  // An open menu closes at a click outside the player (inside, the video's own click does).
  $effect(() => {
    if (menu === null) return
    const onpointerdown = (event: PointerEvent) => {
      if (event.target instanceof Node && !box.contains(event.target)) {
        menu = null
      }
    }
    document.addEventListener('pointerdown', onpointerdown)
    return () => document.removeEventListener('pointerdown', onpointerdown)
  })

  let idle: ReturnType<typeof setTimeout> | undefined
  function wake() {
    active = true
    clearTimeout(idle)
    idle = setTimeout(() => (active = false), IDLE_MS)
  }
  $effect(() => () => clearTimeout(idle))

  /** Where the file is downloaded up to, from where it plays. */
  function bufferedEnd(m: HTMLVideoElement): number {
    for (let i = 0; i < m.buffered.length; i++) {
      if (m.buffered.start(i) <= m.currentTime && m.currentTime <= m.buffered.end(i)) {
        return m.buffered.end(i)
      }
    }
    return 0
  }

  function chapterAt(seconds: number): Chapter | null {
    return chapters.find((chapter) => chapter.startSeconds <= seconds && seconds < chapter.endSeconds) ?? null
  }

  /** How much of a segment is before `seconds`, in % of the segment. */
  const filled = (seconds: number, segment: Segment) => `${filledPart(seconds, segment) * 100}%`

  /** A segment's width: its share of the video; the whole bar when the length isn't known. */
  const span = (segment: Segment) => (total > 0 ? percent(segment.end - segment.start) : '100%')

  const percent = (seconds: number) => `${total > 0 ? (seconds / total) * 100 : 0}%`

  function toggle() {
    if (media.paused) {
      media.play()?.catch(() => {})
      flash = 'play'
    } else {
      media.pause()
      flash = 'pause'
    }
    setTimeout(() => (flash = null), FLASH_MS)
  }

  function seekTo(seconds: number) {
    // Within the video; its length not known yet, only not before its start.
    media.currentTime = total > 0 ? Math.min(Math.max(seconds, 0), total) : Math.max(seconds, 0)
    time = media.currentTime
  }

  const setVolume = (value: number) => keepVolume(media, value)
  const toggleMute = () => keepMute(media)

  function toggleFullscreen() {
    if (document.fullscreenElement) {
      void document.exitFullscreen()
    } else {
      box.requestFullscreen?.().catch(() => {})
    }
  }

  function setRate(value: number) {
    media.playbackRate = value
  }

  function toggleLoop() {
    media.loop = !media.loop
    loop = media.loop
  }

  // The bar: the time under the pointer, from its own width (the chapters' gaps are drawn over it, not in it).
  function timeAt(event: PointerEvent & { currentTarget: HTMLElement }): number {
    const rect = event.currentTarget.getBoundingClientRect()
    return (Math.min(Math.max(event.clientX - rect.left, 0), rect.width) / rect.width) * total
  }

  function onbardown(event: PointerEvent & { currentTarget: HTMLElement }) {
    if (event.button !== 0) return
    event.currentTarget.setPointerCapture(event.pointerId)
    dragging = true
    seekTo(timeAt(event))
  }

  function onbarmove(event: PointerEvent & { currentTarget: HTMLElement }) {
    hover = timeAt(event)
    if (dragging) {
      seekTo(hover)
    }
  }

  function onbarup(event: PointerEvent) {
    dragging = false
    if (event.pointerType !== 'mouse') {
      hover = null
    }
  }

  function onbarkeydown(event: KeyboardEvent) {
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault()
      seekTo(event.key === 'Home' ? 0 : total)
    }
  }

  // A click on the video: play or pause; on a touch screen, a tap does it too and shows the controls (the user's
  // choice: no double tap). An open menu closes first.
  function onsurfaceclick(event: MouseEvent) {
    if (menu !== null) {
      menu = null
      return
    }
    toggle()
    if ((event as PointerEvent).pointerType === 'touch') {
      wake()
    }
  }

  function onsurfacedblclick(event: MouseEvent) {
    if ((event as PointerEvent).pointerType !== 'touch') {
      toggleFullscreen()
    }
  }

  /** A field takes the keys typed in it; a slider (the volume) or a box ticked doesn't type. */
  function typing(target: EventTarget | null): boolean {
    return (
      target instanceof HTMLElement &&
      target.closest(
        'input:not([type="range"]):not([type="checkbox"]):not([type="radio"]), textarea, select, [contenteditable="true"]',
      ) !== null
    )
  }

  function onwindowkeydown(event: KeyboardEvent) {
    if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || typing(event.target)) {
      return
    }
    const target = event.target instanceof HTMLElement ? event.target : null
    const inPlayer = target !== null && box.contains(target)
    switch (event.key) {
      case ' ':
        // A focused button or link takes its own Space.
        if (target?.closest('button, a')) return
        toggle()
        break
      case 'ArrowLeft':
      case 'ArrowRight':
        // The volume slider moves with them.
        if (target?.matches('input[type="range"]')) return
        seekTo(media.currentTime + (event.key === 'ArrowLeft' ? -STEP_SECONDS : STEP_SECONDS))
        break
      case 'ArrowUp':
      case 'ArrowDown':
        if (!inPlayer || target?.matches('input[type="range"]')) return
        setVolume((media.muted ? 0 : media.volume) + (event.key === 'ArrowUp' ? VOLUME_STEP : -VOLUME_STEP))
        break
      case 'm':
      case 'M':
        toggleMute()
        break
      case 'f':
      case 'F':
        toggleFullscreen()
        break
      case 'Escape':
        if (menu === null) return
        menu = null
        break
      default:
        return
    }
    event.preventDefault()
    wake()
  }

  const volumeIcon = $derived(iconFor(volume, muted))
  const speedLabel = (value: number) => (value === 1 ? t('player.normal') : `${value}×`)
</script>

<svelte:window onkeydown={onwindowkeydown} />

<div
  class="controls"
  class:shown
  class:fullscreen
  role="group"
  aria-label={t('watch.player')}
  onpointermove={wake}
  onfocusin={(event) => (focused = event.target instanceof HTMLElement && event.target.matches(':focus-visible'))}
  onfocusout={() => (focused = false)}
>
  <!-- The video itself: a click or a tap plays or pauses. The keyboard has Space, and the play button. -->
  <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
  <div class="surface" onclick={onsurfaceclick} ondblclick={onsurfacedblclick}></div>

  {#if waiting && !paused}
    <div class="spinner" aria-hidden="true"></div>
  {:else if paused && !started}
    <button type="button" class="big" aria-label={t('player.play')} onclick={toggle}><Icon name="play" /></button>
  {/if}
  {#if flash}
    <div class="flash" aria-hidden="true"><Icon name={flash} /></div>
  {/if}

  {#if menu === 'settings'}
    <div class="panel settings" id="player-settings">
      <button type="button" class="row" aria-pressed={loop} onclick={toggleLoop}>
        <Icon name="loop" />
        <span>{t('player.loop')}</span>
        <span class="state">{loop ? t('player.on') : t('player.off')}</span>
      </button>
      <div class="group" role="group" aria-label={t('player.speed')}>
        <span class="label"><Icon name="speed" />{t('player.speed')}</span>
        <div class="speeds">
          {#each SPEEDS as value (value)}
            <button type="button" class="speed" aria-pressed={rate === value} onclick={() => setRate(value)}>
              {speedLabel(value)}
            </button>
          {/each}
        </div>
      </div>
    </div>
  {:else if menu === 'chapters'}
    <div class="panel chapters" id="player-chapters">
      {#each chapters as chapter (chapter.startSeconds)}
        <button
          type="button"
          class="row"
          aria-current={playing === chapter ? 'true' : undefined}
          onclick={() => {
            seekTo(chapter.startSeconds)
            menu = null
          }}
        >
          <span class="at">{formatDuration(chapter.startSeconds)}</span>
          <span>{chapter.title}</span>
        </button>
      {/each}
    </div>
  {/if}

  <div class="bottom">
    <div
      class="bar"
      class:dragging
      role="slider"
      tabindex="0"
      aria-label={t('player.seek')}
      aria-valuemin={0}
      aria-valuemax={Math.round(total)}
      aria-valuenow={Math.round(time)}
      aria-valuetext={`${formatDuration(time)} / ${formatDuration(total)}${playing ? ` · ${playing.title}` : ''}`}
      onpointerdown={onbardown}
      onpointermove={onbarmove}
      onpointerup={onbarup}
      onpointerleave={() => (hover = null)}
      onkeydown={onbarkeydown}
    >
      {#each segments as segment, index (segment.start)}
        <div
          class="segment"
          class:last={index === segments.length - 1}
          class:under={hover !== null && segment.start <= hover && hover < segment.end}
          style:left={percent(segment.start)}
          style:width={span(segment)}
        >
          <div class="buffered" style:width={filled(buffered, segment)}></div>
          {#if hover !== null}<div class="hovered" style:width={filled(hover, segment)}></div>{/if}
          <div class="played" style:width={filled(time, segment)}></div>
        </div>
      {/each}
      <div class="thumb" style:left={percent(time)}></div>
      {#if tip}
        <div class="tip" style:--at={tip.at / total}>
          {#if tip.chapter}<b>{tip.chapter.title}</b>{/if}
          <span>{formatDuration(tip.at)}</span>
        </div>
      {/if}
    </div>

    <div class="row-buttons">
      <button
        type="button"
        aria-label={paused ? t('player.play') : t('player.pause')}
        title={`${paused ? t('player.play') : t('player.pause')} (${t('player.key.space')})`}
        onclick={toggle}
      >
        <Icon name={paused ? 'play' : 'pause'} />
      </button>
      <div class="volume">
        <button
          type="button"
          aria-label={muted ? t('player.unmute') : t('player.mute')}
          title={`${muted ? t('player.unmute') : t('player.mute')} (m)`}
          onclick={toggleMute}
        >
          <Icon name={volumeIcon} />
        </button>
        <input
          type="range"
          min="0"
          max="1"
          step={VOLUME_STEP}
          value={muted ? 0 : volume}
          aria-label={t('player.volume')}
          oninput={(event) => setVolume(Number(event.currentTarget.value))}
          style:--level={muted ? 0 : volume}
        />
      </div>
      <span class="time">{formatDuration(time)} / {formatDuration(total)}</span>
      {#if playing}
        <button
          type="button"
          class="chapter"
          aria-expanded={menu === 'chapters'}
          aria-controls="player-chapters"
          title={t('player.chapters')}
          onclick={() => (menu = menu === 'chapters' ? null : 'chapters')}
        >
          <span aria-hidden="true">·</span>
          {playing.title}
          <span aria-hidden="true">›</span>
        </button>
      {/if}
      <span class="spacer"></span>
      <button
        type="button"
        aria-label={t('player.settings')}
        title={t('player.settings')}
        aria-expanded={menu === 'settings'}
        aria-controls="player-settings"
        class:turning={menu === 'settings'}
        onclick={() => (menu = menu === 'settings' ? null : 'settings')}
      >
        <Icon name="settings" />
        {#if rate !== 1}<span class="badge">{rate}×</span>{/if}
      </button>
      {#if onsizechange && !fullscreen}
        <button
          type="button"
          class="size"
          aria-label={size === 'small' ? t('player.large') : t('player.small')}
          title={size === 'small' ? t('player.large') : t('player.small')}
          aria-pressed={size === 'large'}
          onclick={() => onsizechange(size === 'small' ? 'large' : 'small')}
        >
          <Icon name={size === 'small' ? 'playerLarge' : 'playerSmall'} />
        </button>
      {/if}
      <button
        type="button"
        aria-label={fullscreen ? t('player.exitFullscreen') : t('player.fullscreen')}
        title={`${fullscreen ? t('player.exitFullscreen') : t('player.fullscreen')} (f)`}
        onclick={toggleFullscreen}
      >
        <Icon name={fullscreen ? 'fullscreenExit' : 'fullscreen'} />
      </button>
    </div>
  </div>
</div>

<style>
  .controls {
    position: absolute;
    inset: 0;
    color: var(--color-on-player);
    --icon-size: var(--player-icon-size);
  }

  .controls:not(.shown) {
    cursor: none;
  }

  .surface {
    position: absolute;
    inset: 0;
  }

  .bottom {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    padding: var(--space-8) var(--space-3) var(--space-1);
    background: var(--player-shade);
    opacity: 0;
    transition: opacity 200ms ease-out;
    pointer-events: none;
  }

  .shown .bottom {
    opacity: 1;
    pointer-events: auto;
  }

  /* ---------------------------------------------------------------- the bar and its chapters */

  .bar {
    position: relative;
    height: var(--player-bar-height);
    cursor: pointer;
    touch-action: none;
  }

  .bar:focus-visible {
    outline: var(--focus-ring);
    outline-offset: var(--player-focus-offset);
  }

  .segment {
    position: absolute;
    top: 50%;
    height: var(--player-track-height);
    overflow: hidden;
    /* The gap between two chapters, inside the segment's end: the bar's own width stays the timeline. */
    border-right: var(--player-chapter-gap) solid transparent;
    background: var(--player-track);
    background-clip: padding-box;
    transform: translateY(-50%);
    transition: height var(--transition-fast);
  }

  .segment.last {
    border-right: none;
  }

  .bar:hover .segment,
  .bar.dragging .segment {
    height: var(--player-track-height-hover);
  }

  .bar .segment.under {
    height: var(--player-track-height-under);
  }

  .buffered,
  .hovered,
  .played {
    position: absolute;
    top: 0;
    bottom: 0;
    left: 0;
  }

  .buffered {
    background: var(--player-buffered);
  }

  .hovered {
    background: var(--player-hovered);
  }

  .played {
    background: var(--color-brand);
  }

  .thumb {
    position: absolute;
    top: 50%;
    width: var(--player-thumb-size);
    height: var(--player-thumb-size);
    border-radius: var(--radius-round);
    background: var(--color-brand);
    transform: translate(-50%, -50%) scale(0);
    transition: transform var(--transition-fast);
  }

  .bar:hover .thumb,
  .bar:focus-visible .thumb,
  .bar.dragging .thumb {
    transform: translate(-50%, -50%) scale(1);
  }

  .tip {
    position: absolute;
    bottom: calc(100% + var(--space-2));
    /* Centred on the pointer, kept inside the bar. */
    left: clamp(var(--player-tip-edge), calc(var(--at) * 100%), calc(100% - var(--player-tip-edge)));
    display: flex;
    flex-direction: column;
    align-items: center;
    max-width: var(--player-tip-width);
    padding: var(--space-1) var(--space-2);
    border-radius: var(--radius-sm);
    background: var(--player-panel);
    font-size: var(--font-size-xs);
    text-align: center;
    white-space: nowrap;
    transform: translateX(-50%);
    pointer-events: none;
  }

  .tip b {
    overflow: hidden;
    max-width: 100%;
    font-weight: var(--font-weight-medium);
    text-overflow: ellipsis;
  }

  /* ---------------------------------------------------------------- the buttons */

  .row-buttons {
    display: flex;
    align-items: center;
    gap: var(--space-1);
    min-width: 0;
    height: var(--player-row-height);
  }

  .row-buttons button {
    position: relative;
    display: grid;
    flex: none;
    place-items: center;
    width: var(--icon-button-size);
    height: var(--icon-button-size);
    padding: 0;
    border: none;
    border-radius: var(--radius-round);
    background: none;
    color: inherit;
  }

  .row-buttons button:hover {
    background: var(--player-hover);
  }

  .row-buttons button:focus-visible,
  .big:focus-visible,
  .panel button:focus-visible {
    outline: var(--focus-ring);
  }

  .turning :global(svg) {
    transform: rotate(30deg);
    transition: transform var(--transition-fast);
  }

  .badge {
    position: absolute;
    right: 0;
    bottom: var(--player-focus-offset);
    padding: 0 var(--space-1);
    border-radius: var(--radius-sm);
    background: var(--color-brand);
    font-size: var(--player-badge-size);
    font-weight: var(--font-weight-medium);
    line-height: var(--player-badge-height);
  }

  .volume {
    display: flex;
    align-items: center;
  }

  /* The slider opens on hover or focus, as YouTube's. */
  .volume input {
    width: 0;
    height: var(--player-volume-height);
    margin: 0;
    opacity: 0;
    accent-color: var(--color-on-player);
    transition:
      width var(--transition-fast),
      opacity var(--transition-fast),
      margin var(--transition-fast);
  }

  .volume:hover input,
  .volume:focus-within input {
    width: var(--player-volume-width);
    margin: 0 var(--space-2);
    opacity: 1;
  }

  .time {
    padding: 0 var(--space-2);
    font-size: var(--font-size-sm);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .row-buttons .chapter {
    display: flex;
    flex: 0 1 auto;
    gap: var(--space-1);
    width: auto;
    min-width: 0;
    padding: 0 var(--space-2);
    border-radius: var(--radius-md);
    font-size: var(--font-size-sm);
    white-space: nowrap;
  }

  .chapter {
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .spacer {
    flex: 1;
  }

  /* Below the watch page's one-column breakpoint the two sizes look the same: no button. */
  @media (max-width: 1312px) {
    .size {
      display: none !important;
    }
  }

  /* ---------------------------------------------------------------- the menus */

  /* Over the video, above the bar and its buttons: as tall as the player leaves room for, and over the shade of the
     bottom (which would take the clicks of its last rows). */
  .panel {
    position: absolute;
    z-index: 1;
    right: var(--space-3);
    bottom: var(--player-panel-bottom);
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    max-height: calc(100% - var(--player-panel-bottom) - var(--space-3));
    overflow-y: auto;
    padding: var(--space-2);
    border-radius: var(--radius-lg);
    background: var(--player-panel);
    font-size: var(--font-size-sm);
  }

  .chapters {
    right: auto;
    left: var(--space-3);
    width: min(var(--player-chapters-width), calc(100% - 2 * var(--space-3)));
  }

  .settings {
    width: min(var(--player-menu-width), calc(100% - 2 * var(--space-3)));
  }

  .panel .row {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
    padding: var(--space-2) var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: none;
    color: inherit;
    text-align: left;
  }

  .panel .row:hover,
  .panel .row[aria-current='true'] {
    background: var(--player-hover);
  }

  .row .state {
    margin-left: auto;
    opacity: 0.7;
  }

  .row[aria-pressed='true'] .state {
    color: var(--color-brand);
    opacity: 1;
  }

  .at {
    min-width: 3.5em;
    opacity: 0.7;
    font-variant-numeric: tabular-nums;
  }

  .group {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-3);
  }

  .label {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  .speeds {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: var(--space-1);
  }

  .speed {
    padding: var(--space-1) 0;
    border: none;
    border-radius: var(--radius-md);
    background: var(--player-chip);
    color: inherit;
    font-size: var(--font-size-xs);
    font-variant-numeric: tabular-nums;
  }

  .speed:hover {
    background: var(--player-chip-hover);
  }

  .speed[aria-pressed='true'] {
    background: var(--color-on-player);
    color: var(--color-player-bg);
    font-weight: var(--font-weight-medium);
  }

  /* ---------------------------------------------------------------- the middle */

  .big,
  .flash,
  .spinner {
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
  }

  .big {
    display: grid;
    place-items: center;
    width: var(--player-big-size);
    height: var(--player-big-size);
    border: none;
    border-radius: var(--radius-round);
    background: var(--player-scrim);
    color: inherit;
    --icon-size: var(--player-big-icon-size);
  }

  .big:hover {
    background: var(--color-brand);
  }

  .flash {
    display: grid;
    place-items: center;
    width: var(--player-flash-size);
    height: var(--player-flash-size);
    border-radius: var(--radius-round);
    background: var(--player-scrim);
    animation: flash 500ms ease-out forwards;
    pointer-events: none;
    --icon-size: var(--player-flash-icon-size);
  }

  @keyframes flash {
    from {
      opacity: 1;
      transform: translate(-50%, -50%) scale(1);
    }
    to {
      opacity: 0;
      transform: translate(-50%, -50%) scale(1.5);
    }
  }

  .spinner {
    width: var(--player-spinner-size);
    height: var(--player-spinner-size);
    border: var(--player-spinner-width) solid var(--player-track);
    border-top-color: var(--color-on-player);
    border-radius: var(--radius-round);
    animation: spin 900ms linear infinite;
    pointer-events: none;
  }

  @keyframes spin {
    to {
      transform: translate(-50%, -50%) rotate(360deg);
    }
  }

  /* Phones: fewer things on the row. */
  @media (max-width: 480px) {
    .row-buttons .chapter {
      display: none;
    }

    .volume input {
      display: none;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .flash {
      animation: none;
      opacity: 0;
    }
  }
</style>
