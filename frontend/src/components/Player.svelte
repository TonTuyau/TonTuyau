<script lang="ts">
  // The player area. Decides, from the API's facts and the browser's answer, between:
  // the HTML5 player, "file unavailable" (mediaAvailable: false, or the file answers 404 when played), or "this
  // browser can't play <codec>" + Download. When the browser only says "maybe", the player shows with a notice and
  // the download (under the player on the watch page, over the top of the video in the shorts player).
  // The box takes the video's own shape (lib/videoRatio.ts), so no black bars; on the watch page a button on the
  // player switches between the small and the large player (lib/playerSize.svelte.ts).
  import type { VideoDetail } from '../lib/api/types'
  import { playability, type Playability } from '../lib/codec'
  import { codecName } from '../lib/format'
  import { t } from '../lib/i18n/i18n.svelte'
  import type { PlayerSize } from '../lib/playerSize.svelte'
  import { ratioFromElement, ratioFromTracks, ratioCss, type Ratio } from '../lib/videoRatio'
  import Button from './Button.svelte'
  import Icon from './Icon.svelte'

  // `shorts`: the /shorts/ player fills the frame its page gives it, starts by itself and loops (like YouTube).
  let {
    video,
    probe = playability,
    variant = 'watch',
    size = 'small',
    onsizechange,
    onunavailable,
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

  function onloadedmetadata(event: Event & { currentTarget: HTMLVideoElement }) {
    loadedRatio = ratioFromElement(event.currentTarget)
  }

  // A decode error and a missing file look the same to the media element: ask the server about the file.
  async function onerror() {
    if (failure !== 'none') {
      return
    }
    const id = video.id
    failure = 'checking'
    let missing: boolean
    try {
      missing = (await fetch(video.mediaUrl, { method: 'HEAD' })).status === 404
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
  class="player {variant === 'shorts' ? 'shorts' : mode === 'play' ? 'fit' : 'landscape'}"
  style:--video-ratio={fitted ? ratioCss(shape).ratio : undefined}
  style:--video-ratio-value={fitted ? ratioCss(shape).value : undefined}
  data-mode={mode}
>
  {#if mode === 'play'}
    <!-- svelte-ignore a11y_media_has_caption (subtitles are out of v1, R2-12) -->
    <video
      src={video.mediaUrl}
      poster={video.thumbnailUrl ?? undefined}
      controls
      autoplay={variant === 'shorts'}
      loop={variant === 'shorts'}
      preload="metadata"
      aria-label={t('watch.player')}
      {onloadedmetadata}
      {onerror}
    ></video>
    {#if uncertain && variant === 'shorts'}{@render notice()}{/if}
    {#if onsizechange && variant === 'watch'}
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

  .size {
    position: absolute;
    top: var(--space-2);
    right: var(--space-2);
    display: grid;
    place-items: center;
    width: var(--icon-button-size);
    height: var(--icon-button-size);
    border: none;
    border-radius: var(--radius-round);
    background: var(--color-overlay);
    color: var(--color-on-overlay);
    opacity: 0;
    transition: opacity var(--transition-fast);
  }

  .player:hover .size,
  .size:focus-visible {
    opacity: 1;
  }

  /* Touch screens have no hover: always shown. */
  @media (hover: none) {
    .size {
      opacity: 1;
    }
  }

  /* Below the watch page's one-column breakpoint the two sizes look the same: no button. */
  @media (max-width: 1312px) {
    .size {
      display: none;
    }
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
