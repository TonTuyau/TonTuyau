<script lang="ts">
  // /shorts/<id> (QA06, M5a-5): the YouTube-like vertical player. One short fills the height; up / down (buttons,
  // mouse wheel, ↑ ↓ keys, a swipe on touch screens) move through a sequence that comes from the API: the short
  // opened first, then every other short of the library, latest first (QA07 M5a-18: `/api/videos?type=short`,
  // `exclude` leaves the first one out, v1.3). Moving replaces the URL, so Back leaves the player (M5a-19). The "normal" setting sends shorts to the watch page.
  import { untrack } from 'svelte'
  import { api } from '../lib/api/client'
  import type { Playability } from '../lib/codec'
  import type { VideoCard as Video, VideoDetail } from '../lib/api/types'
  import { missingResource } from '../lib/errors'
  import { formatBytes, formatCompact } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href } from '../lib/router/routes'
  import { shortsPlayer } from '../lib/shortsPlayer.svelte'
  import { pageTitle } from '../lib/title.svelte'
  import { ratioCss, type Ratio } from '../lib/videoRatio'
  import { WheelGesture, wheelPixels } from '../lib/wheelGesture'
  import { isVisitorState } from '../lib/youTubeStates'
  import Avatar from '../components/Avatar.svelte'
  import YouTubeStateBadge from '../components/YouTubeStateBadge.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Icon from '../components/Icon.svelte'
  import MissingState from '../components/MissingState.svelte'
  import Player from '../components/Player.svelte'

  let { id, probe }: { id: string; probe?: (mimeType: string) => Playability } = $props()

  const SWIPE_MIN = 60
  /** Load the next page of the sequence when this close to its end. */
  const PREFETCH = 3

  const remote = new Remote<VideoDetail>()
  const queue = new Pager<Video>()
  let first = $state<string | null>(null)
  let stopQueue: (() => void) | undefined

  // "Normal" shorts player chosen in the settings: the watch page plays it.
  $effect(() => {
    if (shortsPlayer.preference === 'normal') {
      router.navigate(href.watch(id), { replace: true })
    }
  })

  $effect(() => {
    const shortId = id
    if (shortsPlayer.preference === 'vertical') {
      return remote.run((signal) => api.video(shortId, signal))
    }
  })

  const video = $derived(remote.status === 'ready' && remote.data?.id === id ? remote.data : undefined)

  // The frame takes the short's own shape (from the player: the API's track, else the loaded file), as big as the
  // page allows (1.1.0: never letterboxed, so the title overlay sits on the picture's bottom edge), and the download
  // goes away if the file turns out to be missing on the share.
  let ratio = $state<Ratio>()
  let fileMissing = $state(false)
  $effect(() => {
    void video?.id
    fileMissing = false
    paused = false
  })

  // The title overlay sits on the bottom of the picture, where the browser draws its own controls: it steps aside
  // while they may be showing (the pointer over the video, see the CSS; paused; a few seconds after a touch).
  const TOUCH_CONTROLS_MS = 3000
  let paused = $state(false)
  let touched = $state(false)
  let touchTimer: ReturnType<typeof setTimeout> | undefined
  function noteTouch() {
    touched = true
    clearTimeout(touchTimer)
    touchTimer = setTimeout(() => (touched = false), TOUCH_CONTROLS_MS)
  }
  $effect(() => () => clearTimeout(touchTimer))

  // Not a short, says the API: the watch page plays it.
  $effect(() => {
    if (video && video.type !== 'short') {
      router.navigate(href.watch(video.id), { replace: true })
    }
  })

  const sequence = $derived(first === null ? [] : [first, ...queue.items.map((item) => item.id)])
  const index = $derived(sequence.indexOf(id))

  // A short outside the current sequence (first visit, a link, back / forward) starts a new sequence.
  $effect(() => {
    const current = video
    if (current && current.type === 'short' && untrack(() => !sequence.includes(current.id))) {
      first = current.id
      stopQueue = queue.start((cursor, signal) =>
        api.videos({ type: 'short', sort: 'latest', exclude: current.id, cursor }, signal),
      )
    }
  })
  $effect(() => () => stopQueue?.())

  $effect(() => {
    if (index >= 0 && index >= sequence.length - PREFETCH && queue.hasMore) {
      void queue.loadMore()
    }
  })

  const previous = $derived(index > 0 ? sequence[index - 1] : undefined)
  const next = $derived(index >= 0 ? sequence[index + 1] : undefined)

  function go(step: 1 | -1) {
    const target = step > 0 ? next : previous
    if (target) {
      router.navigate(href.short(target), { replace: true })
    }
  }

  function typing(target: EventTarget | null): boolean {
    return (
      target instanceof HTMLElement &&
      (target.isContentEditable || target.closest('input, textarea, select, [role="menu"], [role="listbox"]') !== null)
    )
  }

  function onkeydown(event: KeyboardEvent) {
    if (
      event.defaultPrevented ||
      event.altKey ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey ||
      typing(event.target)
    ) {
      return
    }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      go(event.key === 'ArrowDown' ? 1 : -1)
    }
  }

  let root: HTMLElement | undefined = $state()
  const wheel = new WheelGesture()

  let touchY: number | null = null

  // Wheel and swipe, as listeners: the wheel one must be able to preventDefault (no page scroll behind).
  $effect(() => {
    const element = root
    if (!element) {
      return
    }
    // One short per gesture (lib/wheelGesture.ts): Firefox's line deltas, smooth scrolling and trackpad momentum.
    const onwheel = (event: WheelEvent) => {
      event.preventDefault()
      const step = wheel.feed(wheelPixels(event.deltaY, event.deltaMode, element.clientHeight), event.timeStamp)
      if (step !== 0) {
        go(step)
      }
    }
    const ontouchstart = (event: TouchEvent) => {
      noteTouch()
      touchY = event.touches[0]?.clientY ?? null
    }
    const ontouchend = (event: TouchEvent) => {
      const end = event.changedTouches[0]?.clientY
      if (touchY !== null && end !== undefined && Math.abs(touchY - end) >= SWIPE_MIN) {
        go(touchY > end ? 1 : -1)
      }
      touchY = null
    }
    element.addEventListener('wheel', onwheel, { passive: false })
    element.addEventListener('touchstart', ontouchstart, { passive: true })
    element.addEventListener('touchend', ontouchend)
    return () => {
      element.removeEventListener('wheel', onwheel)
      element.removeEventListener('touchstart', ontouchstart)
      element.removeEventListener('touchend', ontouchend)
    }
  })

  const missing = $derived(remote.status === 'error' && remote.error ? missingResource(remote.error, 'video') : null)

  pageTitle(() => video?.title ?? (missing ? t(missing.title) : null))

  const lang = $derived(i18n.lang)
</script>

<svelte:window {onkeydown} />

<div class="shorts-page" bind:this={root}>
  {#if missing}
    <div class="error"><MissingState title={t(missing.title)} detail={t(missing.hint)} /></div>
  {:else if remote.status === 'error' && remote.error}
    <div class="error"><ErrorState error={remote.error} onretry={() => remote.retry()} /></div>
  {:else}
    <div class="stage">
      <div
        class="frame"
        class:controls={paused || touched}
        aria-busy={!video}
        style:--video-ratio={video && ratio ? ratioCss(ratio).ratio : undefined}
        style:--video-ratio-value={video && ratio ? ratioCss(ratio).value : undefined}
        onplaycapture={() => (paused = false)}
        onpausecapture={() => (paused = true)}
      >
        {#if video && video.type === 'short'}
          <Player {video} {probe} variant="shorts" bind:ratio onunavailable={() => (fileMissing = true)} />
          <div class="info">
            <a class="channel" href={href.channel(video.channel.id)}>
              <Avatar src={video.channel.thumbnailUrl} versions={video.channel.thumbnails} size="xs" />
              <span class="channel-name">{video.channel.name}</span>
            </a>
            <h1 class="title">{video.title}</h1>
            <p class="meta">{tp('count.views', video.views, formatCompact(video.views, lang))}</p>
            {#if isVisitorState(video.youTubeState)}
              <p><YouTubeStateBadge state={video.youTubeState} forVisitors /></p>
            {/if}
          </div>
        {/if}
      </div>

      {#if video && video.type === 'short'}
        <div class="actions">
          <span class="action">
            <span class="round" aria-label={tp('count.likes', video.likes)} role="img"><Icon name="like" /></span>
            <span class="action-label" aria-hidden="true">{formatCompact(video.likes, lang)}</span>
          </span>
          {#if video.mediaAvailable && !fileMissing}
            <a
              class="action"
              href={video.downloadUrl}
              download
              aria-label={`${t('watch.download')} · ${formatBytes(video.fileSize, lang)}`}
            >
              <span class="round"><Icon name="download" /></span>
              <span class="action-label" aria-hidden="true">{formatBytes(video.fileSize, lang)}</span>
            </a>
          {/if}
          <a
            class="action"
            href={href.watch(video.id)}
            aria-label={t('shorts.openWatch')}
            title={t('shorts.openWatch')}
          >
            <span class="round"><Icon name="videos" /></span>
          </a>
        </div>
      {/if}
    </div>

    <nav class="steps" aria-label={t('shorts.navigation')}>
      <button
        type="button"
        class="round step"
        aria-label={t('shorts.previous')}
        title={t('shorts.previous')}
        disabled={!previous}
        onclick={() => go(-1)}
      >
        <Icon name="up" />
      </button>
      <button
        type="button"
        class="round step"
        aria-label={t('shorts.next')}
        title={t('shorts.next')}
        disabled={!next}
        onclick={() => go(1)}
      >
        <Icon name="down" />
      </button>
    </nav>
  {/if}
</div>

<style>
  /* A size container: the frame is fitted to what's left of its content box (100cqw × 100cqh) once the actions and
     the up / down buttons have their room. */
  .shorts-page {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: var(--space-6);
    height: calc(100dvh - var(--topbar-height) - var(--bottom-inset));
    padding: var(--space-4);
    overflow: hidden;
    overscroll-behavior: contain;
    container-type: size;
    --shorts-free-width: calc(100cqw - var(--space-6) - var(--space-4) - 2 * var(--shorts-action-size));
    --shorts-free-height: 100cqh;
  }

  .error {
    flex: 1;
  }

  .stage {
    display: flex;
    align-items: flex-end;
    gap: var(--space-4);
    min-width: 0;
  }

  /* Exactly the short's shape (9:16, 2:3, square, landscape…), as large as fits: the picture fills it with no black
     bars, so what's laid over its bottom edge sits on the picture's bottom edge. */
  .frame {
    position: relative;
    overflow: hidden;
    width: min(var(--shorts-free-width), var(--shorts-free-height) * var(--video-ratio-value, 0.5625));
    aspect-ratio: var(--video-ratio, 9 / 16);
    border-radius: var(--radius-lg);
    background: var(--color-player-bg);
  }

  .frame :global(.player) {
    border-radius: 0;
  }

  /* Over the bottom of the picture; only its links take clicks (the video stays clickable). The browser's own controls
     are drawn there too: the overlay fades out while they may be showing (.controls, or the pointer over the video). */
  .info {
    position: absolute;
    right: 0;
    bottom: 0;
    left: 0;
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
    padding: var(--space-8) var(--space-4) var(--space-3);
    background: linear-gradient(transparent, var(--color-overlay));
    color: var(--color-on-overlay);
    pointer-events: none;
    transition: opacity var(--transition-fast);
  }

  .controls .info {
    opacity: 0;
  }

  @media (hover: hover) {
    .frame:has(:global(video:hover)) .info {
      opacity: 0;
    }
  }

  .info a {
    pointer-events: auto;
  }

  .channel {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-width: 0;
  }

  .channel-name {
    overflow: hidden;
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-medium);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .title {
    display: -webkit-box;
    overflow: hidden;
    -webkit-line-clamp: 2;
    line-clamp: 2;
    -webkit-box-orient: vertical;
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
    line-height: var(--line-height-tight);
    overflow-wrap: anywhere;
  }

  .meta {
    font-size: var(--font-size-xs);
    opacity: 0.85;
  }

  .actions {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    padding-bottom: var(--shorts-controls-clearance);
  }

  .action {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-1);
    color: var(--color-text);
    font-size: var(--font-size-xs);
    font-weight: var(--font-weight-medium);
  }

  .round {
    display: grid;
    place-items: center;
    width: var(--shorts-action-size);
    height: var(--shorts-action-size);
    border: none;
    border-radius: var(--radius-round);
    background: var(--color-surface);
    color: var(--color-text);
  }

  a.action:hover .round,
  .step:hover:not(:disabled) {
    background: var(--color-surface-hover);
  }

  .steps {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .step:disabled {
    color: var(--color-text-secondary);
    opacity: 0.5;
    cursor: default;
  }

  @media (max-width: 600px) {
    /* The whole area is the screen: the actions and the buttons are laid over it. */
    .shorts-page {
      position: relative;
      padding: 0;
      --shorts-free-width: 100cqw;
      --shorts-free-height: 100cqh;
    }

    /* Black around a short that isn't as tall as the screen (square, landscape), as before. */
    .stage {
      position: relative;
      align-items: center;
      justify-content: center;
      width: 100%;
      height: 100%;
      background: var(--color-player-bg);
    }

    .frame {
      border-radius: 0;
    }

    .info {
      padding-right: calc(var(--shorts-action-size) + var(--space-6));
    }

    /* Over the video's right edge, YouTube's phone layout. */
    .actions,
    .steps {
      position: absolute;
      right: var(--space-3);
    }

    .actions {
      bottom: var(--space-4);
    }

    .steps {
      top: var(--space-4);
    }

    .action {
      color: var(--color-on-overlay);
    }

    .round {
      background: var(--color-overlay);
      color: var(--color-on-overlay);
    }
  }
</style>
