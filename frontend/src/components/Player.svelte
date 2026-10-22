<script lang="ts">
  // The player area. Decides, from the API's facts and the browser's answer, between:
  // the HTML5 player, "file unavailable" (mediaAvailable: false, or the file answers 404 when played), or "this
  // browser can't play <codec>" + Download. When the browser only says "maybe", the player shows with a notice and
  // the download (under the player on the watch page, over the top of the video in the shorts player).
  // The box takes the video's own shape (lib/videoRatio.ts), so no black bars; on the watch page a button on the
  // player switches between the small and the large player (lib/playerSize.svelte.ts).
  // `start` (the watch page's `t`) is where the video starts; seek() moves to a time and plays (a timecode's click).
  // Leaving a video (the next short, another page, a message in its place) empties its <video> at once, so its download
  // stops (1.1.0, the Firefox shorts fix): see release() below.
  // On the watch page the video is played in parts (2.0, C3: lib/streaming.ts), about 30 s ahead, the whole file when
  // that fails; the shorts play their file whole.
  import type { VideoDetail } from '../lib/api/types'
  import { untrack } from 'svelte'
  import { playability, type Playability } from '../lib/codec'
  import { codecName } from '../lib/format'
  import { t } from '../lib/i18n/i18n.svelte'
  import type { PlayerSize } from '../lib/playerSize.svelte'
  import { startPlayback, type Playback } from '../lib/streaming'
  import { ratioFromElement, ratioFromTracks, ratioCss, type Ratio } from '../lib/videoRatio'
  import Button from './Button.svelte'
  import Icon from './Icon.svelte'
  import PlayerControls from './PlayerControls.svelte'
  import ShortsControls from './ShortsControls.svelte'

  // `shorts`: the /shorts/ player fills the frame its page gives it, starts by itself and loops (like YouTube).
  let {
    video,
    probe = playability,
    variant = 'watch',
    size = 'small',
    onsizechange,
    onunavailable,
    start = null,
    ontime,
    // eslint-disable-next-line no-useless-assignment -- a bindable prop, written by the $effect below
    ratio = $bindable(),
  }: {
    video: VideoDetail
    probe?: (mimeType: string) => Playability
    variant?: 'watch' | 'shorts'
    /** The watch page's player size; the size button only shows when `onsizechange` is given. */
    size?: PlayerSize
    onsizechange?: (size: PlayerSize) => void
    /** The file turned out to be missing on the share (404 when played): the page hides its download links. */
    onunavailable?: () => void
    /** Where to start, in seconds (a link to a time); ignored past the video's duration. */
    start?: number | null
    /** Where the video is, in seconds, as it plays or is moved (the watch page's chapters, 1.5). */
    ontime?: (seconds: number) => void
    /** Out: the shape the box uses (API, then the loaded file, then a placeholder), for a page that frames it. */
    ratio?: Ratio
  } = $props()

  // `failed`: the browser accepted the type but couldn't play the file; `missing`: the file answers 404.
  let failure = $state<'none' | 'checking' | 'failed' | 'missing'>('none')
  let loadedRatio = $state<Ratio | null>(null)
  $effect(() => {
    void video.id
    failure = 'none'
    loadedRatio = null
  })

  const support = $derived(video.mediaAvailable ? probe(video.mimeType) : 'no')
  const mode = $derived(
    !video.mediaAvailable || failure === 'missing'
      ? 'unavailable'
      : failure === 'failed'
        ? 'failed'
        : support === 'no'
          ? 'unsupported'
          : 'play',
  )
  // canPlayType said "maybe" (M5a-16): play it, and say it may not work, with the download.
  const uncertain = $derived(mode === 'play' && support === 'maybe')
  const codecs = $derived(video.tracks.map((track) => codecName(track.codec)).join(' + ') || video.mimeType)

  // Before the file's metadata is in, a video without a sized track gets the usual shape for its type.
  const placeholder = $derived<Ratio>(video.type === 'short' ? { width: 9, height: 16 } : { width: 16, height: 9 })
  const shape = $derived(ratioFromTracks(video.tracks) ?? loadedRatio ?? placeholder)
  $effect(() => {
    ratio = shape
  })
  const fitted = $derived(variant === 'watch' && mode === 'play')

  let element: HTMLVideoElement | undefined = $state()
  let box: HTMLElement | undefined = $state()

  // A <video> taken out of the page keeps downloading, and keeps its connection, until the garbage collector frees
  // it. Scrolling through shorts in Firefox, the old ones piled up (9 open downloads for 1 video on screen) and took
  // every HTTP/1.1 connection to the server: the next short's file then waited forever, with no error. Emptying it
  // (no src, then load()) cancels its download right away.
  $effect(() => {
    const media = element
    if (media) {
      return () => release(media)
    }
  })

  function release(media: HTMLVideoElement) {
    media.pause()
    media.removeAttribute('src')
    media.load()
  }

  // A start time within the video (the API's duration): applied once the file's metadata is in, and again when the
  // link changes to another time of the same video.
  const startAt = $derived(start !== null && start > 0 && start <= video.durationSeconds ? start : null)
  $effect(() => {
    const at = startAt
    if (at !== null && element && element.readyState >= HTMLMediaElement.HAVE_METADATA) {
      element.currentTime = at
    }
  })

  // The watch page's playback: in parts, or the whole file (its address set here, not in the markup).
  let playback: Playback | undefined
  $effect(() => {
    const media = element
    const url = video.mediaUrl
    if (!media || variant !== 'watch') {
      return
    }
    const started = startPlayback(media, url, { start: untrack(() => startAt) })
    playback = started
    return () => {
      started.stop()
      if (playback === started) {
        playback = undefined
      }
    }
  })

  function onloadedmetadata(event: Event & { currentTarget: HTMLVideoElement }) {
    loadedRatio = ratioFromElement(event.currentTarget)
    if (startAt !== null) {
      event.currentTarget.currentTime = startAt
    }
  }

  /** Moves to `seconds` and plays (YouTube's timecode click). False when there's no video to seek (a message). */
  export function seek(seconds: number): boolean {
    if (!element) {
      return false
    }
    element.currentTime = seconds
    // A refused play() (autoplay rules) leaves the video paused at that time, which is fine.
    element.play()?.catch(() => {})
    return true
  }

  const HEAD_TIMEOUT_MS = 10_000

  // A decode error and a missing file look the same to the media element: ask the server about the file.
  async function onerror() {
    // In parts, an error is the parts': the playback falls back to the whole file by itself.
    if (failure !== 'none' || (playback && playback.mode !== 'whole')) {
      return
    }
    const id = video.id
    failure = 'checking'
    let missing: boolean
    try {
      // Bounded: a question that never gets an answer mustn't leave the player waiting with no message.
      missing =
        (await fetch(video.mediaUrl, { method: 'HEAD', signal: AbortSignal.timeout(HEAD_TIMEOUT_MS) })).status === 404
    } catch {
      missing = false
    }
    if (video.id !== id) {
      return
    }
    failure = missing ? 'missing' : 'failed'
    if (missing) {
      onunavailable?.()
    }
  }
</script>

{#snippet notice()}
  <div class="notice" role="note" data-testid="maybe-notice">
    <Icon name="warning" />
    <p>{t('watch.maybeNotice')}</p>
    <Button href={video.downloadUrl} download><Icon name="download" />{t('watch.download')}</Button>
  </div>
{/snippet}

<div
  bind:this={box}
  class="player {variant === 'shorts' ? 'shorts' : mode === 'play' ? 'fit' : 'landscape'}"
  style:--video-ratio={fitted ? ratioCss(shape).ratio : undefined}
  style:--video-ratio-value={fitted ? ratioCss(shape).value : undefined}
  data-mode={mode}
>
  {#if mode === 'play'}
    <!-- svelte-ignore a11y_media_has_caption (subtitles are out of v1, R2-12) -->
    <video
      bind:this={element}
      src={variant === 'shorts' ? video.mediaUrl : undefined}
      poster={video.thumbnailUrl ?? undefined}
      disablepictureinpicture
      autoplay
      loop={variant === 'shorts'}
      preload="metadata"
      aria-label={t('watch.player')}
      {onloadedmetadata}
      {onerror}
      ontimeupdate={(event) => ontime?.(event.currentTarget.currentTime)}
    ></video>
    {#if uncertain && variant === 'shorts'}{@render notice()}{/if}
    <!-- TonTuyau's own controls: the watch page's (2.0, C1), the shorts' (2.0.1, as YouTube's Shorts). No
         picture-in-picture (the user's choice): Firefox's own "Pop out this video" button, over the video, is off too. -->
    {#if variant === 'shorts' && element && box}
      <ShortsControls media={element} {box} duration={video.durationSeconds} />
    {:else if element && box}
      <PlayerControls
        media={element}
        {box}
        chapters={video.chapters}
        duration={video.durationSeconds}
        {size}
        {onsizechange}
      />
    {/if}
  {:else}
    <div class="message" role="status">
      <Icon name={mode === 'unavailable' ? 'broken' : 'warning'} />
      {#if mode === 'unavailable'}
        <p class="title">{t('watch.unavailable')}</p>
        <p class="hint">{video.mediaAvailable ? t('watch.missingHint') : t('watch.unavailableHint')}</p>
      {:else}
        <p class="title">
          {mode === 'failed' ? t('watch.playbackFailed') : t('watch.unsupported', { codec: codecs })}
        </p>
        <p class="hint">{t('watch.unsupportedHint')}</p>
        <Button href={video.downloadUrl} download variant="filled"><Icon name="download" />{t('watch.download')}</Button
        >
      {/if}
    </div>
  {/if}
</div>
{#if uncertain && variant !== 'shorts'}{@render notice()}{/if}

<style>
  .player {
    position: relative;
    overflow: hidden;
    width: 100%;
    border-radius: var(--radius-lg);
    background: var(--color-player-bg);
    color: var(--color-on-player);
  }

  /* Messages (no video to fit): the usual landscape box. */
  .landscape {
    aspect-ratio: 16 / 9;
  }

  /* The video's own shape, as wide as the column allows without growing taller than the page's height cap
     (--player-max-height, set by the watch page for the small and large player). */
  .fit {
    aspect-ratio: var(--video-ratio);
    width: min(
      100%,
      max(var(--player-min-height), var(--player-max-height, var(--player-max-height-small))) * var(--video-ratio-value)
    );
    margin-inline: auto;
  }

  .shorts {
    height: 100%;
  }

  /* Full screen (the controls' button, F): the whole screen, the video in the middle of it. */
  .player:fullscreen {
    width: 100%;
    height: 100%;
    max-width: none;
    aspect-ratio: auto;
    border-radius: 0;
  }

  .player:fullscreen video {
    object-fit: contain;
  }

  .notice {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2) var(--space-3);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    background: var(--color-warning-bg);
    color: var(--color-on-warning);
    font-size: var(--font-size-sm);
  }

  .notice p {
    flex: 1;
    min-width: min(100%, 20ch);
  }

  .shorts .notice {
    position: absolute;
    top: var(--space-2);
    right: var(--space-2);
    left: var(--space-2);
  }

  video {
    display: block;
    width: 100%;
    height: 100%;
    background: var(--color-player-bg);
  }

  .message {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-3);
    height: 100%;
    padding: var(--space-6);
    text-align: center;
    --icon-size: var(--space-12);
    --color-button: var(--color-on-player);
    --color-on-button: var(--color-player-bg);
  }

  .title {
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-medium);
  }

  .hint {
    max-width: 50ch;
    opacity: 0.8;
  }
</style>
