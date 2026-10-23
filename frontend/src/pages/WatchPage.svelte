<script lang="ts">
  // /watch?v=<id>: player (or the unavailable / unsupported message), title, channel + subscribers, views, date, likes,
  // download, expandable description with tags and file details, and the channel's latest uploads beside it (or below
  // it with the large player), scrolling on page after page. `&t=` (front-only routing) starts the video at that time;
  // the description's timecodes are links to their time, and a plain click seeks the player there and plays (1.1.0).
  // 1.5: the chapters (always shown in the description box, the one playing outlined) and the comments TonTuyau read.
  import { api } from '../lib/api/client'
  import type { Playability } from '../lib/codec'
  import type { VideoCard as Video, VideoDetail } from '../lib/api/types'
  import { missingResource } from '../lib/errors'
  import { codecName, formatBitrate, formatBytes, formatCompact, formatDate } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { playerSize } from '../lib/playerSize.svelte'
  import { Pager, Remote } from '../lib/remote.svelte'
  import { router } from '../lib/router/router.svelte'
  import { href, startTime } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'
  import { visitorBadge } from '../lib/youTubeStates'
  import Avatar from '../components/Avatar.svelte'
  import YouTubeStateBadge from '../components/YouTubeStateBadge.svelte'
  import Button from '../components/Button.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Chapters from '../components/Chapters.svelte'
  import Comments from '../components/Comments.svelte'
  import ExpandableText from '../components/ExpandableText.svelte'
  import LoadMore from '../components/LoadMore.svelte'
  import MissingState from '../components/MissingState.svelte'
  import Icon from '../components/Icon.svelte'
  import Player from '../components/Player.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import VideoCard from '../components/VideoCard.svelte'

  let { probe }: { probe?: (mimeType: string) => Playability } = $props()

  const id = $derived(router.query.get('v'))
  const start = $derived(startTime(router.query.get('t')))
  const remote = new Remote<VideoDetail>()
  const more = new Pager<Video>()

  $effect(() => {
    const videoId = id
    if (videoId) {
      return remote.run((signal) => api.video(videoId, signal))
    }
  })

  const video = $derived(remote.status === 'ready' && remote.data?.id === id ? remote.data : undefined)

  // The player found the file missing on the share (404 when played): no download link to a missing file.
  let fileMissing = $state(false)
  $effect(() => {
    void video?.id
    fileMissing = false
  })

  $effect(() => {
    const current = video
    if (current) {
      return more.start((cursor, signal) =>
        api.channelVideos(
          current.channel.id,
          // The API leaves the current video out (v1.1 `exclude`): no filtering here.
          { type: current.type, sort: 'latest', size: 12, exclude: current.id, cursor },
          signal,
        ),
      )
    }
  })

  const missing = $derived(remote.status === 'error' && remote.error ? missingResource(remote.error, 'video') : null)

  pageTitle(() => video?.title ?? (missing ? t(missing.title) : null))

  const lang = $derived(i18n.lang)

  let player: Player | undefined = $state()
  /** Where the video is (seconds), for the chapter playing. */
  let time = $state(0)
  let stage: HTMLElement | undefined = $state()

  // A timecode's click: seek and play, and bring the player back into view if the page was scrolled away from it.
  function seek(seconds: number) {
    if (!player?.seek(seconds) || !stage) {
      return
    }
    const box = stage.getBoundingClientRect()
    const top = Number.parseFloat(getComputedStyle(stage).scrollMarginTop) || 0
    if (box.top < top || box.bottom > window.innerHeight) {
      const smooth = !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
      stage.scrollIntoView({ block: 'start', behavior: smooth ? 'smooth' : 'auto' })
    }
  }
</script>

{#if !id}
  <StateMessage icon="videos" title={t('watch.missingId')}>
    <Button href={href.home()}>{t('notFound.home')}</Button>
  </StateMessage>
{:else if missing}
  <MissingState title={t(missing.title)} detail={t(missing.hint)} />
{:else if remote.status === 'error' && remote.error}
  <ErrorState error={remote.error} onretry={() => remote.retry()} />
{:else if !video}
  <div class="watch"><div class="primary"><Skeleton layout="page" /></div></div>
{:else}
  <div class="watch" class:large={playerSize.size === 'large'} data-player-size={playerSize.size}>
    <div class="stage" bind:this={stage}>
      <Player
        bind:this={player}
        {video}
        {probe}
        {start}
        size={playerSize.size}
        onsizechange={(size) => playerSize.set(size)}
        onunavailable={() => (fileMissing = true)}
        ontime={(seconds) => (time = seconds)}
      />
    </div>
    <div class="primary">
      <h1 class="title">{video.title}</h1>
      {#if visitorBadge(video.youTubeState)}
        <p class="inactive">
          <YouTubeStateBadge state={video.youTubeState} forVisitors />
          <span class="hint">{visitorBadge(video.youTubeState)?.hint}</span>
        </p>
      {/if}

      <div class="bar">
        <a class="channel" href={href.channel(video.channel.id)}>
          <Avatar src={video.channel.thumbnailUrl} versions={video.channel.thumbnails} />
          <span class="channel-text">
            <span class="channel-name">{video.channel.name}</span>
            <span class="subscribers"
              >{tp(
                'count.subscribers',
                video.channel.subscribers,
                formatCompact(video.channel.subscribers, lang),
              )}</span
            >
          </span>
        </a>
        <div class="actions">
          <span class="pill" aria-label={tp('count.likes', video.likes)}>
            <Icon name="like" /><span aria-hidden="true">{formatCompact(video.likes, lang)}</span>
          </span>
          {#if video.mediaAvailable && !fileMissing}
            <Button href={video.downloadUrl} download>
              <Icon name="download" />{t('watch.download')} · {formatBytes(video.fileSize, lang)}
            </Button>
          {/if}
        </div>
      </div>

      <section class="description">
        <ExpandableText
          text={video.description || t('watch.noDescription')}
          more={t('watch.more')}
          less={t('watch.less')}
          timecodes={video.description
            ? {
                duration: video.durationSeconds,
                href: (seconds) => href.watch(video.id, seconds),
                onseek: seek,
              }
            : undefined}
        >
          {#snippet header()}
            <p class="stats">
              {tp('count.views', video.views)} ·
              <time datetime={video.published}>{formatDate(video.published, lang)}</time>
            </p>
            {#if video.tags.length > 0}
              <ul class="tags" aria-label={t('watch.tags')}>
                {#each video.tags as tag (tag)}
                  <li><a href={href.results({ q: tag })}>#{tag}</a></li>
                {/each}
              </ul>
            {/if}
          {/snippet}
          {#snippet extra()}
            <dl class="facts">
              {#if video.categories.length > 0}
                <dt>{t('watch.categories')}</dt>
                <dd>{video.categories.join(', ')}</dd>
              {/if}
              <dt>{t('watch.file')}</dt>
              <dd>
                <ul class="tracks">
                  <li>{formatBytes(video.fileSize, lang)} · {video.mimeType}</li>
                  {#each video.tracks as track, index (index)}
                    <li>
                      {codecName(track.codec)}{track.width && track.height ? ` · ${track.width}×${track.height}` : ''} · {formatBitrate(
                        track.bitrate,
                        lang,
                      )}
                    </li>
                  {/each}
                  <li>{t('watch.downloaded', { date: formatDate(video.downloaded, lang) })}</li>
                </ul>
              </dd>
            </dl>
          {/snippet}
        </ExpandableText>
        {#if video.chapters.length > 0}
          <Chapters
            chapters={video.chapters}
            {time}
            hrefFor={(seconds) => href.watch(video.id, seconds)}
            onseek={seek}
          />
        {/if}
      </section>

      <Comments
        videoId={video.id}
        channelName={video.channel.name}
        timecodes={{ duration: video.durationSeconds, href: (seconds) => href.watch(video.id, seconds), onseek: seek }}
      />
    </div>

    <aside class="secondary" aria-label={t('watch.moreFrom', { channel: video.channel.name })}>
      <h2 class="side-title">{t('watch.moreFrom', { channel: video.channel.name })}</h2>
      {#if more.status === 'loading'}
        <Skeleton layout="rows" count={4} />
      {:else if more.status === 'error' && more.error}
        <ErrorState error={more.error} onretry={() => more.retry()} />
      {:else if more.items.length === 0}
        <p class="side-empty">{t('watch.noMore')}</p>
      {:else}
        <div class="side-list">
          {#each more.items as item (item.id)}
            <VideoCard video={item} variant="compact" />
          {/each}
        </div>
        <!-- Infinite scroll like YouTube's: the next page loads before the end of the list comes into view. -->
        <LoadMore pager={more} />
      {/if}
    </aside>
  </div>
{/if}

<style>
  /* Small player (YouTube's default view): the player and the details in the main column, the channel's other
     videos beside both. Large player (theater view, 2.1.1): the player edge to edge on black, from the top bar
     down (no padding, no round corners), the rest below in the usual centred column: the outer columns are the
     margins, the middle ones the details (as wide as beside the small player) and the side column.
     The last row takes any extra height of the side column, so the player and the title stay together. */
  .watch {
    display: grid;
    grid-template-areas:
      'stage secondary'
      'primary secondary';
    grid-template-columns: minmax(0, 1fr) var(--watch-side-width);
    grid-template-rows: auto 1fr;
    gap: var(--space-3) var(--space-6);
    max-width: var(--watch-max-width);
    margin: 0 auto;
    padding: var(--space-6);
  }

  @media (min-width: 1313px) {
    .watch.large {
      grid-template-areas:
        'stage stage stage stage'
        '. primary secondary .';
      grid-template-columns:
        minmax(var(--space-6), 1fr)
        minmax(0, calc(var(--watch-max-width) - 3 * var(--space-6) - var(--watch-side-width)))
        var(--watch-side-width)
        minmax(var(--space-6), 1fr);
      column-gap: 0;
      max-width: none;
      padding: 0 0 var(--space-6);
    }

    .watch.large .secondary {
      margin-left: var(--space-6);
    }
  }

  .stage {
    grid-area: stage;
    /* Below the sticky top bar when a timecode brings the player back into view. */
    scroll-margin-top: calc(var(--topbar-height) + var(--space-3));
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .secondary {
    grid-area: secondary;
    min-width: 0;
  }

  .primary {
    grid-area: primary;
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    min-width: 0;
  }

  .title {
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-bold);
    line-height: var(--line-height-tight);
    overflow-wrap: anywhere;
  }

  .inactive {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-2);
  }

  .hint {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .bar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3);
  }

  .channel {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-width: 0;
  }

  .channel-text {
    display: flex;
    flex-direction: column;
    min-width: 0;
  }

  .channel-name {
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .subscribers {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
  }

  .pill {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    height: var(--button-height);
    padding: 0 var(--space-4);
    border-radius: var(--radius-pill);
    background: var(--color-surface);
    font-weight: var(--font-weight-medium);
  }

  .description {
    padding: var(--space-3);
    border-radius: var(--radius-lg);
    background: var(--color-surface);
  }

  .stats {
    font-weight: var(--font-weight-medium);
  }

  .tags {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-2);
    margin: 0;
    padding: 0;
    list-style: none;
  }

  /* Real videos carry up to 76 tags: one line of them while the description is collapsed, all once expanded. */
  :global(.expandable:not(.expanded)) .tags {
    max-height: 1lh;
    overflow: hidden;
  }

  .tags a {
    color: var(--color-tag);
  }

  .facts {
    display: grid;
    grid-template-columns: max-content 1fr;
    gap: var(--space-1) var(--space-4);
    margin: var(--space-2) 0 0;
  }

  dt {
    font-weight: var(--font-weight-medium);
  }

  dd {
    margin: 0;
    overflow-wrap: anywhere;
  }

  .tracks {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .side-title {
    margin-bottom: var(--space-3);
    font-size: var(--font-size-md);
    font-weight: var(--font-weight-medium);
  }

  .side-empty {
    color: var(--color-text-secondary);
    font-size: var(--font-size-sm);
  }

  .side-list {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  /* One column: player, details, then the side list; both sizes look the same here. */
  @media (max-width: 1312px) {
    .watch,
    .watch.large {
      grid-template-areas:
        'stage'
        'primary'
        'secondary';
      grid-template-columns: minmax(0, 1fr);
      grid-template-rows: auto;
      row-gap: var(--space-6);
      --player-max-height: var(--player-max-height-small);
    }
  }

  @media (max-width: 600px) {
    .watch,
    .watch.large {
      padding: 0 0 var(--space-6);
    }

    .stage > :not(:global(.player)),
    .primary,
    .secondary {
      margin: 0 var(--space-3);
    }
  }
</style>
