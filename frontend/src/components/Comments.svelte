<script lang="ts">
  // A video's comments (1.5), as TonTuyau read them on YouTube: only for a video it read them for (none at all when
  // never read; "No comments under this video" when read and there were none). Threads in YouTube's order or the
  // newest first, 20 at a time; answers open under their comment; long ones fold; timecodes are links to their time.
  // Authors get a letter, not their picture (it would be fetched from Google).
  import type { Comment, CommentThread, Comments } from '../lib/api/types'
  import { formatAge, formatDate } from '../lib/format'
  import { i18n, t, tp } from '../lib/i18n/i18n.svelte'
  import { Pager } from '../lib/remote.svelte'
  import { api } from '../lib/api/client'
  import LinkifiedText, { type Timecodes } from './LinkifiedText.svelte'
  import LoadMore from './LoadMore.svelte'

  let { videoId, channelName, timecodes }: { videoId: string; channelName: string; timecodes: Timecodes } = $props()

  /** Past this many characters a comment folds ("Read more"). */
  const FOLD_AFTER = 300

  let sort = $state<'top' | 'newest'>('top')
  let read = $state<Comments | null>(null)
  const threads = new Pager<CommentThread>()

  $effect(() => {
    const id = videoId
    const order = sort
    return threads.start(async (cursor, signal) => {
      const answer = await api.videoComments(id, { sort: order, cursor: cursor ?? undefined }, signal)
      if (cursor === null) {
        read = answer
      }
      return answer
    })
  })

  let open = $state<Record<string, boolean>>({})
  let unfolded = $state<Record<string, boolean>>({})

  const initial = (author: string) => (author.replace(/^@/, '')[0] ?? '?').toUpperCase()
</script>

{#snippet comment(item: Comment, reply: boolean)}
  <article class="comment" class:reply>
    <span class="avatar" aria-hidden="true">{initial(item.author)}</span>
    <div class="body">
      {#if item.pinned}
        <p class="pinned">{t('comments.pinned', { channel: channelName })}</p>
      {/if}
      <p class="meta">
        <span class="author" class:uploader={item.byUploader}>{item.author}</span>
        {#if item.published}
          <time datetime={item.published} title={formatDate(item.published, i18n.lang)}
            >{formatAge(item.published, i18n.lang)}</time
          >
        {/if}
      </p>
      <div class="text" class:folded={item.text.length > FOLD_AFTER && !unfolded[item.id]}>
        <LinkifiedText text={item.text} {timecodes} />
      </div>
      {#if item.text.length > FOLD_AFTER}
        <button type="button" class="more" onclick={() => (unfolded[item.id] = !unfolded[item.id])}>
          {unfolded[item.id] ? t('watch.less') : t('watch.more')}
        </button>
      {/if}
      <p class="acts">
        {#if item.likes}<span>{tp('count.likes', item.likes)}</span>{/if}
        {#if item.hearted}<span class="heart">{t('comments.hearted', { channel: channelName })}</span>{/if}
      </p>
    </div>
  </article>
{/snippet}

{#if read && read.readAt}
  {@const readAt = read.readAt}
  <section class="comments" aria-labelledby="comments-title">
    <header>
      <h2 id="comments-title">{t('comments.title')}</h2>
      {#if read.youTubeCount !== null}
        <span class="count">{tp('comments.onYouTube', read.youTubeCount)}</span>
      {/if}
      {#if read.total > 0}
        <div class="sort" role="group" aria-label={t('comments.sort')}>
          <button type="button" aria-pressed={sort === 'top'} onclick={() => (sort = 'top')}>
            {t('comments.top')}
          </button>
          <button type="button" aria-pressed={sort === 'newest'} onclick={() => (sort = 'newest')}>
            {t('comments.newest')}
          </button>
        </div>
      {/if}
      <p class="source">
        {tp('comments.kept', read.kept, undefined, { date: formatDate(readAt, i18n.lang) })}
      </p>
    </header>

    {#if read.total === 0}
      <p class="empty">{t('comments.none')}</p>
    {:else}
      <div class="threads">
        {#each threads.items as thread (thread.comment.id)}
          <div class="thread">
            {@render comment(thread.comment, false)}
            {#if thread.replies.length > 0}
              <button
                type="button"
                class="replies-toggle"
                aria-expanded={open[thread.comment.id] === true}
                onclick={() => (open[thread.comment.id] = !open[thread.comment.id])}
              >
                {tp(open[thread.comment.id] ? 'comments.hideReplies' : 'comments.replies', thread.replies.length)}
              </button>
              {#if open[thread.comment.id]}
                <div class="replies">
                  {#each thread.replies as reply (reply.id)}
                    {@render comment(reply, true)}
                  {/each}
                </div>
              {/if}
            {/if}
          </div>
        {/each}
      </div>
      <LoadMore pager={threads} />
    {/if}
  </section>
{/if}

<style>
  .comments {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
    margin-top: var(--space-4);
  }

  header {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-2) var(--space-4);
  }

  h2 {
    font-size: var(--font-size-lg);
    font-weight: var(--font-weight-medium);
  }

  .count,
  .source,
  time,
  .acts,
  .pinned {
    color: var(--color-text-secondary);
    font-size: var(--font-size-xs);
  }

  .source {
    width: 100%;
    margin: 0;
  }

  .sort {
    display: inline-flex;
    gap: var(--space-1);
  }

  .sort button {
    height: var(--chip-height);
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-md);
    background: var(--color-surface);
    color: var(--color-text);
    cursor: pointer;
  }

  .sort button[aria-pressed='true'] {
    background: var(--color-surface-selected);
    color: var(--color-on-surface-selected);
  }

  .empty {
    color: var(--color-text-secondary);
  }

  .threads {
    display: flex;
    flex-direction: column;
    gap: var(--space-5);
  }

  .thread {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  .comment {
    display: grid;
    grid-template-columns: 2.5rem minmax(0, 1fr);
    gap: var(--space-3);
  }

  .comment.reply {
    grid-template-columns: 1.5rem minmax(0, 1fr);
  }

  .avatar {
    display: grid;
    place-items: center;
    width: 2.5rem;
    height: 2.5rem;
    border-radius: 50%;
    background: var(--color-surface);
    color: var(--color-text-secondary);
    font-weight: var(--font-weight-medium);
  }

  .reply .avatar {
    width: 1.5rem;
    height: 1.5rem;
    font-size: var(--font-size-xs);
  }

  .body {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    min-width: 0;
  }

  .body p {
    margin: 0;
  }

  .meta {
    display: flex;
    flex-wrap: wrap;
    align-items: baseline;
    gap: var(--space-1) var(--space-2);
  }

  .author {
    font-size: var(--font-size-sm);
    font-weight: var(--font-weight-medium);
  }

  .author.uploader {
    padding: 0 var(--space-2);
    border-radius: var(--radius-pill);
    background: var(--color-surface-hover);
  }

  .text {
    white-space: pre-line;
    overflow-wrap: anywhere;
  }

  .text.folded {
    display: -webkit-box;
    overflow: hidden;
    -webkit-line-clamp: 4;
    line-clamp: 4;
    -webkit-box-orient: vertical;
  }

  .more {
    align-self: flex-start;
    padding: 0;
    border: none;
    background: none;
    color: var(--color-text-secondary);
    font-weight: var(--font-weight-medium);
    cursor: pointer;
  }

  .acts {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1) var(--space-3);
  }

  .replies-toggle {
    align-self: flex-start;
    height: var(--chip-height);
    margin-left: calc(2.5rem + var(--space-3));
    padding: 0 var(--space-3);
    border: none;
    border-radius: var(--radius-pill);
    background: none;
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
    cursor: pointer;
  }

  .replies-toggle:hover {
    background: var(--color-surface);
  }

  .replies {
    display: flex;
    flex-direction: column;
    gap: var(--space-3);
    margin-left: calc(2.5rem + var(--space-3));
  }
</style>
