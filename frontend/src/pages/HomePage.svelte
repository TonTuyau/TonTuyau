<script lang="ts">
  // Home: the sections of /api/home, in the API's order (video, short, stream; empty ones omitted by the API).
  import { api } from '../lib/api/client'
  import type { Home } from '../lib/api/types'
  import { t } from '../lib/i18n/i18n.svelte'
  import { Remote } from '../lib/remote.svelte'
  import { href } from '../lib/router/routes'
  import { pageTitle } from '../lib/title.svelte'
  import ErrorState from '../components/ErrorState.svelte'
  import Icon from '../components/Icon.svelte'
  import Skeleton from '../components/Skeleton.svelte'
  import StateMessage from '../components/StateMessage.svelte'
  import VideoList from '../components/VideoList.svelte'

  const home = new Remote<Home>()
  $effect(() => home.run((signal) => api.home(undefined, signal)))
  pageTitle(() => null)

  const icons = { video: 'videos', short: 'shorts', stream: 'streams' } as const
</script>

<div class="page">
  {#if home.status === 'loading'}
    <Skeleton />
  {:else if home.status === 'error' && home.error}
    <ErrorState error={home.error} onretry={() => home.retry()} />
  {:else if home.data && home.data.sections.length === 0}
    <StateMessage title={t('home.empty')} detail={t('home.emptyHint')} />
  {:else if home.data}
    {#each home.data.sections as section, sectionIndex (section.type)}
      <section class="section" aria-labelledby="home-{section.type}">
        <header class="head">
          <h2 id="home-{section.type}"><Icon name={icons[section.type]} />{t(`type.${section.type}`)}</h2>
          <a class="see-all" href={href.feed(section.type)}>{t('home.seeAll')}</a>
        </header>
        <VideoList
          videos={section.items}
          layout={section.type === 'short' ? 'shorts' : 'grid'}
          eagerCount={sectionIndex === 0 ? 4 : 0}
        />
      </section>
    {/each}
  {/if}
</div>

<style>
  .page {
    display: flex;
    flex-direction: column;
    gap: var(--space-10);
    padding: var(--space-6);
  }

  .section {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  .section + .section {
    padding-top: var(--space-6);
    border-top: var(--border-width) solid var(--color-divider);
  }

  .head {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  h2 {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--font-size-xl);
    font-weight: var(--font-weight-bold);
  }

  .see-all {
    padding: var(--space-2) var(--space-4);
    border-radius: var(--radius-pill);
    color: var(--color-link);
    font-weight: var(--font-weight-medium);
  }

  .see-all:hover {
    background: var(--color-surface);
  }

  @media (max-width: 600px) {
    .page {
      padding: var(--space-3);
    }
  }
</style>
