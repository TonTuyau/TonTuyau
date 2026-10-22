<script lang="ts">
  // TonTuyau's own controls on the shorts (2.0.1), as YouTube's Shorts: play and the volume at the top left (shown
  // while it's paused, the pointer moves or the keyboard is in them; on a phone, a moment after a tap), and a thin bar
  // along the bottom edge, always there, that thickens under the pointer and can be dragged. A click or a tap on the
  // video plays or pauses. Keyboard: Space, ←/→ 5 s, M mute anywhere on the page but in a field (the page keeps ↑/↓
  // for the short before and after). The volume is the watch page's (lib/playerVolume.ts); a short always loops.
  import { formatDuration } from '../lib/format'
  import { t } from '../lib/i18n/i18n.svelte'
  import { restoreVolume, setVolume, toggleMute, volumeIcon } from '../lib/playerVolume'
  import Icon from './Icon.svelte'

  let {
    media,
    box,
    duration,
  }: {
    media: HTMLVideoElement
    /** The player's box: what the pointer moves over. */
    box: HTMLElement
    /** The API's length, until the file says its own. */
    duration: number
  } = $props()

  const IDLE_MS = 3_000
  const FLASH_MS = 500
  const STEP_SECONDS = 5
  const VOLUME_STEP = 0.05

  let paused = $state(true)
  let started = $state(false)
  let time = $state(0)
  let length = $state(0)
  let volume = $state(1)
  let muted = $state(false)
  let waiting = $state(false)
  let active = $state(false)
  let focused = $state(false)
  let dragging = $state(false)
  let flash = $state<'play' | 'pause' | null>(null)

  const shown = $derived(paused || active || focused)
  const total = $derived(length > 0 ? length : duration)

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
    }
    const onwaiting = () => (waiting = true)
    const onready = () => (waiting = false)
    const events = ['play', 'pause', 'timeupdate', 'durationchange', 'loadedmetadata', 'volumechange', 'seeked']
    for (const name of events) m.addEventListener(name, sync)
    m.addEventListener('waiting', onwaiting)
    m.addEventListener('playing', onready)
    m.addEventListener('canplay', onready)
    sync()
    return () => {
      for (const name of events) m.removeEventListener(name, sync)
      m.removeEventListener('waiting', onwaiting)
      m.removeEventListener('playing', onready)
      m.removeEventListener('canplay', onready)
    }
  })

  $effect(() => restoreVolume(media))

  // The pointer moving over the short shows the buttons for a while.
  $effect(() => {
    const onpointermove = (event: PointerEvent) => {
      if (event.pointerType === 'mouse') wake()
    }
    box.addEventListener('pointermove', onpointermove)
    return () => box.removeEventListener('pointermove', onpointermove)
  })

  // A drag along the bar isn't a swipe to the next short: its touches stop there (a listener of its own, since
  // Svelte's delegated ones run only once the event reached the page's root, after the page's swipe listener).
  let bar: HTMLElement | undefined = $state()
  $effect(() => {
    const element = bar
    if (!element) return
    const stop = (event: TouchEvent) => event.stopPropagation()
    element.addEventListener('touchstart', stop, { passive: true })
    element.addEventListener('touchend', stop)
    return () => {
      element.removeEventListener('touchstart', stop)
      element.removeEventListener('touchend', stop)
    }
  })

  let idle: ReturnType<typeof setTimeout> | undefined
  function wake() {
    active = true
    clearTimeout(idle)
    idle = setTimeout(() => (active = false), IDLE_MS)
  }
  $effect(() => () => clearTimeout(idle))

  const percent = (seconds: number) => `${total > 0 ? Math.min((seconds / total) * 100, 100) : 0}%`

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
    media.currentTime = total > 0 ? Math.min(Math.max(seconds, 0), total) : Math.max(seconds, 0)
    time = media.currentTime
  }

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
    if (dragging) {
      seekTo(timeAt(event))
    }
  }

  function onbarkeydown(event: KeyboardEvent) {
    if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault()
      seekTo(event.key === 'Home' ? 0 : total)
    }
  }

  // A click or a tap on the video plays or pauses; a tap also shows the buttons for a moment.
  function onsurfaceclick(event: MouseEvent) {
    toggle()
    if ((event as PointerEvent).pointerType !== 'mouse') {
      wake()
    }
  }

  /** A field takes the keys typed in it; a slider (the volume) doesn't type. */
  function typing(target: EventTarget | null): boolean {
    return (
      target instanceof HTMLElement &&
      target.closest('input:not([type="range"]), textarea, select, [contenteditable="true"]') !== null
    )
  }

  function onwindowkeydown(event: KeyboardEvent) {
    if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || typing(event.target)) {
      return
    }
    const target = event.target instanceof HTMLElement ? event.target : null
    switch (event.key) {
      case ' ':
        // A focused button or link takes its own Space.
        if (target?.closest('button, a')) return
        toggle()
        break
      case 'ArrowLeft':
      case 'ArrowRight':
        if (target?.matches('input[type="range"]')) return
        seekTo(media.currentTime + (event.key === 'ArrowLeft' ? -STEP_SECONDS : STEP_SECONDS))
        break
      case 'm':
      case 'M':
        toggleMute(media)
        break
      default:
        return
    }
    event.preventDefault()
    wake()
  }
</script>

<svelte:window onkeydown={onwindowkeydown} />

<div
  class="controls"
  class:shown
  role="group"
  aria-label={t('watch.player')}
  onfocusin={(event) => (focused = event.target instanceof HTMLElement && event.target.matches(':focus-visible'))}
  onfocusout={() => (focused = false)}
>
  <!-- The video itself: a click or a tap plays or pauses. The keyboard has Space, and the play button. -->
  <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
  <div class="surface" onclick={onsurfaceclick}></div>

  {#if waiting && !paused}
    <div class="spinner" aria-hidden="true"></div>
  {:else if paused && !started}
    <button type="button" class="big" aria-label={t('player.play')} onclick={toggle}><Icon name="play" /></button>
  {/if}
  {#if flash}
    <div class="flash" aria-hidden="true"><Icon name={flash} /></div>
  {/if}

  <div class="top">
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
        onclick={() => toggleMute(media)}
      >
        <Icon name={volumeIcon(volume, muted)} />
      </button>
      <input
        type="range"
        min="0"
        max="1"
        step={VOLUME_STEP}
        value={muted ? 0 : volume}
        aria-label={t('player.volume')}
        oninput={(event) => setVolume(media, Number(event.currentTarget.value))}
      />
    </div>
  </div>

  <div
    bind:this={bar}
    class="bar"
    class:dragging
    role="slider"
    tabindex="0"
    aria-label={t('player.seek')}
    aria-valuemin={0}
    aria-valuemax={Math.round(total)}
    aria-valuenow={Math.round(time)}
    aria-valuetext={`${formatDuration(time)} / ${formatDuration(total)}`}
    onpointerdown={onbardown}
    onpointermove={onbarmove}
    onpointerup={() => (dragging = false)}
    onkeydown={onbarkeydown}
  >
    <div class="track"><div class="played" style:width={percent(time)}></div></div>
    <div class="thumb" style:left={percent(time)}></div>
  </div>
</div>

<style>
  .controls {
    position: absolute;
    inset: 0;
    color: var(--color-on-player);
    --icon-size: var(--player-icon-size);
  }

  .surface {
    position: absolute;
    inset: 0;
  }

  /* ---------------------------------------------------------------- play and the volume, at the top left */

  .top {
    position: absolute;
    top: 0;
    right: 0;
    left: 0;
    display: flex;
    align-items: center;
    gap: var(--space-1);
    padding: var(--space-2);
    background: var(--player-shade-top);
    opacity: 0;
    transition: opacity 200ms ease-out;
    pointer-events: none;
  }

  .shown .top {
    opacity: 1;
  }

  .shown .top button,
  .shown .top input {
    pointer-events: auto;
  }

  .top button {
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

  .top button:hover {
    background: var(--player-hover);
  }

  .top button:focus-visible,
  .big:focus-visible,
  .bar:focus-visible {
    outline: var(--focus-ring);
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

  /* ---------------------------------------------------------------- the bar, along the bottom edge */

  /* Over the title (the page lays it over the bottom of the picture), its track on the very edge. */
  .bar {
    position: absolute;
    z-index: 1;
    right: 0;
    bottom: 0;
    left: 0;
    height: var(--player-bar-height);
    cursor: pointer;
    touch-action: none;
  }

  .track {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    height: var(--player-track-height);
    background: var(--player-track);
    transition: height var(--transition-fast);
  }

  .bar:hover .track,
  .bar.dragging .track {
    height: var(--player-track-height-hover);
  }

  .played {
    height: 100%;
    background: var(--color-brand);
  }

  .thumb {
    position: absolute;
    bottom: calc(var(--player-track-height-hover) / 2);
    width: var(--player-thumb-size);
    height: var(--player-thumb-size);
    border-radius: var(--radius-round);
    background: var(--color-brand);
    transform: translate(-50%, 50%) scale(0);
    transition: transform var(--transition-fast);
  }

  .bar:hover .thumb,
  .bar:focus-visible .thumb,
  .bar.dragging .thumb {
    transform: translate(-50%, 50%) scale(1);
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

  /* Phones: the volume is the phone's own buttons; only mute. */
  @media (max-width: 480px) {
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
